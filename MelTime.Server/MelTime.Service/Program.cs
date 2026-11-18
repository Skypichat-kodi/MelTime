using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using MelTime.Core.Agenda;
using MelTime.Core.Api;
using MelTime.Core.Exploitation;
using MelTime.Core.Modeles;
using MelTime.Core.Protocole;
using MelTime.Service.Donnees;
using MelTime.Service.Pipe;
using MelTime.Service.Reseau;
using Microsoft.Data.Sqlite;

namespace MelTime.Service;

/// <summary>
/// Point d'entree du service MelTime.
/// Le Planificateur de taches le lance ; Ctrl+C permet un arret propre en manuel.
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        // Journaux lisibles meme avec des caracteres accentues ou des guillemets francais.
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (IOException)
        {
            // Sortie redirigee ou console particuliere : on continue sans y toucher.
        }

        Console.Title = "MelTime.Service";

        // Outil de verification du socle de donnees (developpement).
        if (args.Length > 0 && args[0].Equals("--verifier-donnees", StringComparison.OrdinalIgnoreCase))
        {
            return VerifierDonnees();
        }

        // Outil de verification du dialogue par pipe (developpement).
        if (args.Length > 0 && args[0].Equals("--verifier-pipe", StringComparison.OrdinalIgnoreCase))
        {
            return await VerifierPipeAsync();
        }

        // Outil de verification du calcul des heures (developpement).
        if (args.Length > 0 && args[0].Equals("--verifier-calcul", StringComparison.OrdinalIgnoreCase))
        {
            return VerifierCalcul();
        }

        // Outil de verification du stockage des feuilles de temps (developpement).
        if (args.Length > 0 && args[0].Equals("--verifier-agenda", StringComparison.OrdinalIgnoreCase))
        {
            return VerifierAgenda();
        }

        // Outil de verification de l'API HTTP (developpement).
        if (args.Length > 0 && args[0].Equals("--verifier-http", StringComparison.OrdinalIgnoreCase))
        {
            return await VerifierHttpAsync();
        }

        // Une seule instance du service : deux exemplaires se disputeraient
        // le port reseau et le pipe.
        using var verrouService = InstanceUnique.Acquerir("MelTime.Service", out var dejaLance);

        if (dejaLance)
        {
            Journaliser("Une autre instance de MelTime.Service est deja en cours d'execution.");
            return 1;
        }

        using var arretDemande = new CancellationTokenSource();

        // L'arret est differe de quelques centaines de millisecondes pour que
        // la reponse ait le temps de partir vers l'interface qui l'a demande.
        void DemanderArret()
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(300).ConfigureAwait(false);
                arretDemande.Cancel();
            });
        }

        Console.CancelKeyPress += (_, e) =>
        {
            // On intercepte Ctrl+C pour sortir proprement au lieu de tuer le processus.
            e.Cancel = true;
            arretDemande.Cancel();
        };

        Journaliser("MelTime.Service demarre.");

        BaseAdministration baseAdministration;
        try
        {
            CheminDonnees.Preparer();
            baseAdministration = new BaseAdministration(CheminDonnees.BaseAdministration);
            baseAdministration.Initialiser();
            Journaliser($"Base d'administration prete : {CheminDonnees.BaseAdministration}");
        }
        catch (Exception ex)
        {
            Journaliser($"ERREUR a l'initialisation des donnees : {ex.Message}");
            return 1;
        }

        var configuration = Configuration.Charger(out var avertissement);

        if (avertissement is not null)
        {
            Journaliser($"Configuration : {avertissement}");
        }

        Journaliser($"Configuration : port {configuration.Port}, hote « {configuration.Hote} », session {configuration.DureeSessionJours} jour(s)");

        var serveurPipe = new ServeurPipe(new TraitementRequetes(baseAdministration, Journaliser, DemanderArret, configuration), Journaliser);
        var tachePipe = serveurPipe.ExecuterAsync(arretDemande.Token);

        var serveurHttp = new ServeurHttp(configuration, baseAdministration, Journaliser);
        var tacheHttp = serveurHttp.ExecuterAsync(arretDemande.Token);

        Journaliser("En attente. Ctrl+C pour arreter.");

        try
        {
            await Task.Delay(Timeout.Infinite, arretDemande.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Arret normal demande par l'utilisateur.
        }

        try
        {
            await Task.WhenAll(tachePipe, tacheHttp).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Arret normal des ecoutes.
        }

        Journaliser("MelTime.Service arrete.");
        return 0;
    }

    /// <summary>
    /// Exerce le couple serveur / client de pipe dans un seul processus.
    /// Valide le code de communication utilise par l'UI, sans avoir a lancer l'interface.
    /// </summary>
    private static async Task<int> VerifierPipeAsync()
    {
        CheminDonnees.Preparer();
        var baseAdmin = new BaseAdministration(CheminDonnees.BaseAdministration);
        baseAdmin.Initialiser();

        using var arret = new CancellationTokenSource();
        var serveur = new ServeurPipe(new TraitementRequetes(baseAdmin, Journaliser), Journaliser);
        var tacheServeur = serveur.ExecuterAsync(arret.Token);

        // Laisse le serveur s'installer sur le pipe.
        await Task.Delay(400).ConfigureAwait(false);

        var client = new ClientPipe();
        var echecs = 0;

        var vivant = await client.EnvoyerAsync(new Requete { Commande = Commandes.ServiceVivant }).ConfigureAwait(false);
        echecs += vivant.Succes ? 0 : 1;
        Console.WriteLine($"service.vivant    : {(vivant.Succes ? "OK" : "ECHEC")} — {vivant.Message}");

        var liste = await client.EnvoyerAsync(new Requete { Commande = Commandes.ClientsLister }).ConfigureAwait(false);
        echecs += liste.Succes ? 0 : 1;
        Console.WriteLine($"clients.lister    : {(liste.Succes ? "OK" : "ECHEC")} — {liste.Clients?.Count ?? 0} client(s)");

        // Nettoyage d'un essai precedent.
        foreach (var residu in baseAdmin.Lister().Where(c => c.Identifiant == "pipe.interne"))
        {
            baseAdmin.Supprimer(residu.Id);
        }

        var creation = await client.EnvoyerAsync(new Requete
        {
            Commande = Commandes.ClientsCreer,
            Client = new Client { Identifiant = "pipe.interne", Nom = "Test", Prenom = "Interne" },
            MotDePasse = "MotDePasse123!"
        }).ConfigureAwait(false);
        echecs += creation.Succes ? 0 : 1;
        Console.WriteLine($"clients.creer     : {(creation.Succes ? "OK" : "ECHEC")} — {creation.Erreur ?? creation.Client?.Identifiant}");

        if (creation.Client is { Id: > 0 } cree)
        {
            var modifier = await client.EnvoyerAsync(new Requete
            {
                Commande = Commandes.ClientsModifier,
                Client = new Client
                {
                    Id = cree.Id,
                    Identifiant = cree.Identifiant,
                    Nom = "Modifie",
                    Prenom = "Interne",
                    Actif = false
                }
            }).ConfigureAwait(false);
            echecs += modifier.Succes ? 0 : 1;
            Console.WriteLine($"clients.modifier  : {(modifier.Succes ? "OK" : "ECHEC")} — actif={modifier.Client?.Actif}");

            var mdp = await client.EnvoyerAsync(new Requete
            {
                Commande = Commandes.ClientsChangerMotDePasse,
                Id = cree.Id,
                MotDePasse = "AutreMotDePasse123!"
            }).ConfigureAwait(false);
            echecs += mdp.Succes ? 0 : 1;
            Console.WriteLine($"changerMotDePasse : {(mdp.Succes ? "OK" : "ECHEC")}");

            // Prepare une base de donnees pour ce client, afin d'exercer l'archivage.
            var baseClient = new BaseClient(CheminDonnees.BaseClient(cree.Id));
            baseClient.Initialiser();
            SqliteConnection.ClearAllPools();

            var suppression = await client.EnvoyerAsync(new Requete
            {
                Commande = Commandes.ClientsSupprimer,
                Id = cree.Id
            }).ConfigureAwait(false);
            echecs += suppression.Succes ? 0 : 1;
            Console.WriteLine($"clients.supprimer : {(suppression.Succes ? "OK" : "ECHEC")}");

            var archives = Directory.Exists(CheminDonnees.DossierArchives)
                ? Directory.GetFiles(CheminDonnees.DossierArchives, $"client-{cree.Id}-*.db")
                : Array.Empty<string>();

            echecs += archives.Length == 1 ? 0 : 1;
            Console.WriteLine($"archivage         : {(archives.Length == 1 ? "OK" : "ECHEC")} — {archives.Length} archive(s)");

            if (archives.Length == 1)
            {
                Console.WriteLine($"                    {Path.GetFileName(archives[0])}");
                File.Delete(archives[0]);
            }
        }

        arret.Cancel();
        try
        {
            await tacheServeur.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Arret normal.
        }

        Console.WriteLine(echecs == 0 ? "Reseau local : conforme." : $"Reseau local : {echecs} echec(s).");
        return echecs;
    }

    /// <summary>
    /// Exerce l'API HTTP sur un port de test en local : etat, connexion, refus, deconnexion.
    /// Force l'hote « localhost » pour ne pas exiger de droits eleves.
    /// </summary>
    private static async Task<int> VerifierHttpAsync()
    {
        var echecs = 0;

        void Controle(string libelle, object? attendu, object? obtenu)
        {
            var ok = Equals(attendu, obtenu);
            if (!ok)
            {
                echecs++;
            }

            Console.WriteLine($"  {(ok ? "OK  " : "ECHEC")}  {libelle,-38} attendu={Afficher(attendu),-9} obtenu={Afficher(obtenu)}");
        }

        CheminDonnees.Preparer();
        var baseAdmin = new BaseAdministration(CheminDonnees.BaseAdministration);
        baseAdmin.Initialiser();

        foreach (var residu in baseAdmin.Lister().Where(c => c.Identifiant == "http.test"))
        {
            baseAdmin.Supprimer(residu.Id);
        }

        var id = baseAdmin.Creer(
            new Client { Identifiant = "http.test", Nom = "Reseau", Prenom = "Test" },
            "Reseau123!");

        var configuration = new Configuration { Port = 5099, Hote = "localhost", DureeSessionJours = 1 };

        using var arret = new CancellationTokenSource();
        var serveur = new ServeurHttp(configuration, baseAdmin, Journaliser, "localhost");
        var tache = serveur.ExecuterAsync(arret.Token);

        await Task.Delay(700).ConfigureAwait(false);

        using var http = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5099"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        Controle("GET /api/etat", 200, (int)(await http.GetAsync("/api/etat").ConfigureAwait(false)).StatusCode);

        var refus = await http
            .PostAsJsonAsync("/api/connexion", new DemandeConnexion { Identifiant = "http.test", MotDePasse = "faux" }, ProtocoleJson.Options)
            .ConfigureAwait(false);
        Controle("connexion, mot de passe faux", 401, (int)refus.StatusCode);

        var accepte = await http
            .PostAsJsonAsync("/api/connexion", new DemandeConnexion { Identifiant = "http.test", MotDePasse = "Reseau123!" }, ProtocoleJson.Options)
            .ConfigureAwait(false);
        var reponse = await accepte.Content.ReadFromJsonAsync<ReponseConnexion>(ProtocoleJson.Options).ConfigureAwait(false);

        Controle("connexion valide", 200, (int)accepte.StatusCode);
        Controle("jeton remis", true, !string.IsNullOrWhiteSpace(reponse?.Jeton));
        Controle("client identifie", "http.test", reponse?.Client?.Identifiant);

        var jeton = reponse?.Jeton ?? string.Empty;
        Controle("session retrouvee en base", "http.test", baseAdmin.ClientDeSession(jeton)?.Identifiant);

        // --- Feuille de temps : lecture d'une semaine jamais saisie ---

        using (var requete = new HttpRequestMessage(HttpMethod.Get, "/api/semaine/2026/47"))
        {
            requete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jeton);
            var lecture = await http.SendAsync(requete).ConfigureAwait(false);
            var semaine = await lecture.Content.ReadFromJsonAsync<SemaineApi>(ProtocoleJson.Options).ConfigureAwait(false);

            Controle("semaine vierge (GET)", 200, (int)lecture.StatusCode);
            Controle("contrat du client applique", 35, (semaine?.ContratHebdomadaireMinutes ?? 0) / 60);
            Controle("sept jours renvoyes", 7, semaine?.Jours.Count);
            Controle("total d'une semaine vide", "00:00", semaine?.TotalTravaille);
        }

        // --- Feuille de temps : enregistrement ---

        var aEnregistrer = new SemaineApi { ContratHebdomadaireMinutes = 35 * 60 };

        for (var index = 0; index < 5; index++)
        {
            aEnregistrer.Jours.Add(new JourApi
            {
                Index = index,
                Arrivee = "08:00",
                PauseDebut = "12:00",
                PauseFin = "13:00",
                Depart = "17:00"
            });
        }

        using (var requete = new HttpRequestMessage(HttpMethod.Put, "/api/semaine/2026/47"))
        {
            requete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jeton);
            requete.Content = JsonContent.Create(aEnregistrer, options: ProtocoleJson.Options);

            var ecriture = await http.SendAsync(requete).ConfigureAwait(false);
            var resultat = await ecriture.Content.ReadFromJsonAsync<SemaineApi>(ProtocoleJson.Options).ConfigureAwait(false);

            Controle("enregistrement (PUT)", 200, (int)ecriture.StatusCode);
            Controle("total recalcule par le service", "40:00", resultat?.TotalTravaille);
            Controle("heures supplementaires (35h)", "05:00", resultat?.HeuresSupplementaires);
        }

        // --- Feuille de temps : relecture apres enregistrement ---

        using (var requete = new HttpRequestMessage(HttpMethod.Get, "/api/semaine/2026/47"))
        {
            requete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jeton);
            var lecture = await http.SendAsync(requete).ConfigureAwait(false);
            var semaine = await lecture.Content.ReadFromJsonAsync<SemaineApi>(ProtocoleJson.Options).ConfigureAwait(false);

            Controle("total relu", "40:00", semaine?.TotalTravaille);
            Controle("arrivee du lundi conservee", "08:00", semaine?.Jours[0].Arrivee);
            Controle("journee du lundi calculee", "08:00", semaine?.Jours[0].Travaille);
            Controle("dimanche inchange", null, semaine?.Jours[6].Arrivee);
        }

        using (var requete = new HttpRequestMessage(HttpMethod.Get, "/api/semaines"))
        {
            requete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jeton);
            var liste = await http.SendAsync(requete).ConfigureAwait(false);
            var texte = await liste.Content.ReadAsStringAsync().ConfigureAwait(false);
            Controle("liste des semaines (1 entree)", true, texte.Contains("\"numero\":47", StringComparison.Ordinal));
        }

        // --- Le serveur doit refuser une feuille incoherente ---

        var incoherente = new SemaineApi { ContratHebdomadaireMinutes = 35 * 60 };
        incoherente.Jours.Add(new JourApi { Index = 0, Arrivee = "18:00", Depart = "08:00" });

        using (var requete = new HttpRequestMessage(HttpMethod.Put, "/api/semaine/2026/48"))
        {
            requete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jeton);
            requete.Content = JsonContent.Create(incoherente, options: ProtocoleJson.Options);

            var refusSemaine = await http.SendAsync(requete).ConfigureAwait(false);
            Controle("semaine incohérente refusée", 400, (int)refusSemaine.StatusCode);
        }

        // --- Controles d'acces ---

        Controle("semaine sans jeton", 401, (int)(await http.GetAsync("/api/semaine/2026/47").ConfigureAwait(false)).StatusCode);

        using (var requete = new HttpRequestMessage(HttpMethod.Get, "/api/semaine/2026/60"))
        {
            requete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jeton);
            var horsLimites = await http.SendAsync(requete).ConfigureAwait(false);
            Controle("semaine inexistante (2026 en compte 53)", 400, (int)horsLimites.StatusCode);
        }

        using (var requete = new HttpRequestMessage(HttpMethod.Get, "/api/route/inexistante"))
        {
            requete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jeton);
            var inconnue = await http.SendAsync(requete).ConfigureAwait(false);
            Controle("route inconnue, authentifie", 404, (int)inconnue.StatusCode);
        }

        // --- Le client HTTP, exactement celui qu'utilise le WPF, contre le vrai serveur ---

        using (var api = new ClientApi("http://localhost:5099"))
        {
            Controle("ClientApi : serveur joignable", true, await api.EtatAsync().ConfigureAwait(false));

            var connexion = await api.ConnecterAsync("http.test", "Reseau123!").ConfigureAwait(false);
            Controle("ClientApi : connexion", true, connexion.Succes);

            api.Jeton = connexion.Jeton;

            var profil = await api.ProfilAsync().ConfigureAwait(false);
            Controle("ClientApi : profil", "http.test", profil.Identifiant);

            var semaine = await api.ChargerSemaineAsync(2026, 47).ConfigureAwait(false);
            Controle("ClientApi : chargement de la semaine", "40:00", semaine.TotalTravaille);

            semaine.Jours[5].Arrivee = "09:00";
            semaine.Jours[5].Depart = "12:00";

            var enregistree = await api.EnregistrerSemaineAsync(semaine).ConfigureAwait(false);
            Controle("ClientApi : enregistrement", "43:00", enregistree.TotalTravaille);
            Controle("ClientApi : samedi calcule", "03:00", enregistree.Jours[5].Travaille);

            await api.DeconnecterAsync().ConfigureAwait(false);
        }

        using (var requete = new HttpRequestMessage(HttpMethod.Post, "/api/deconnexion"))
        {
            requete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jeton);
            var deconnexion = await http.SendAsync(requete).ConfigureAwait(false);
            Controle("deconnexion", 200, (int)deconnexion.StatusCode);
        }

        Controle("session supprimee", null, baseAdmin.ClientDeSession(jeton));

        arret.Cancel();

        try
        {
            await tache.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Arret normal.
        }

        baseAdmin.Supprimer(id);

        // Le client de test n'a pas ete supprime par l'API : on nettoie son fichier de donnees.
        SqliteConnection.ClearAllPools();
        var baseDeTest = CheminDonnees.BaseClient(id);
        if (File.Exists(baseDeTest))
        {
            File.Delete(baseDeTest);
        }

        Console.WriteLine();
        Console.WriteLine(echecs == 0 ? "API HTTP : conforme." : $"API HTTP : {echecs} echec(s).");

        return echecs;
    }

    /// <summary>
    /// Exerce le stockage des feuilles de temps sur une base jetable :
    /// enregistrement, relecture, calcul, independence entre semaines.
    /// </summary>
    private static int VerifierAgenda()
    {
        var echecs = 0;

        void Controle(string libelle, object? attendu, object? obtenu)
        {
            var ok = Equals(attendu, obtenu);
            if (!ok)
            {
                echecs++;
            }

            Console.WriteLine($"  {(ok ? "OK  " : "ECHEC")}  {libelle,-42} attendu={Afficher(attendu),-9} obtenu={Afficher(obtenu)}");
        }

        CheminDonnees.Preparer();
        var chemin = Path.Combine(CheminDonnees.DossierDonnees, "verif-agenda.db");

        SqliteConnection.ClearAllPools();
        if (File.Exists(chemin))
        {
            File.Delete(chemin);
        }

        Console.WriteLine($"Base jetable : {chemin}");
        Console.WriteLine();

        var baseClient = new BaseClient(chemin);
        baseClient.Initialiser();

        Controle("semaine jamais saisie", null, baseClient.Charger(2026, 47));

        var semaine = SemaineTravail.Creer(2026, 47);
        semaine.ContratHebdomadaireMinutes = 39 * 60;

        for (var index = 0; index < 3; index++)
        {
            semaine.Jours[index].Arrivee = new TimeOnly(8, 0);
            semaine.Jours[index].PauseDebut = new TimeOnly(12, 0);
            semaine.Jours[index].PauseFin = new TimeOnly(13, 0);
            semaine.Jours[index].Depart = new TimeOnly(17, 0);
        }

        baseClient.Enregistrer(semaine);

        var relue = baseClient.Charger(2026, 47);
        Controle("semaine relue", true, relue is not null);
        Controle("contrat conserve", 39 * 60, relue?.ContratHebdomadaireMinutes);
        Controle("total de 3 jours a 8h", TimeSpan.FromHours(24), relue?.TotalTravaille);
        Controle("heure d'arrivee du lundi", new TimeOnly(8, 0), relue?.Jours[0].Arrivee);
        Controle("pause du mardi", new TimeOnly(13, 0), relue?.Jours[1].PauseFin);
        Controle("dimanche reste vide", null, relue?.Jours[6].Depart);
        Controle("heures supp. de la semaine", TimeSpan.Zero,
            relue is null ? null : relue.HeuresSupplementaires());

        // Effacer un jour puis reenregistrer : la modification doit se propager.
        relue!.Jours[2].Effacer();
        baseClient.Enregistrer(relue);

        var apresEffacement = baseClient.Charger(2026, 47);
        Controle("total apres effacement", TimeSpan.FromHours(16), apresEffacement?.TotalTravaille);
        Controle("mercredi efface", null, apresEffacement?.Jours[2].Arrivee);

        Controle("semaine voisine independante", null, baseClient.Charger(2026, 48));
        Controle("nombre de semaines stockees", 1, baseClient.ListerSemaines().Count);

        SqliteConnection.ClearAllPools();
        File.Delete(chemin);

        Console.WriteLine();
        Console.WriteLine(echecs == 0 ? "Stockage des feuilles de temps : conforme." : $"Stockage des feuilles de temps : {echecs} echec(s).");

        return echecs;
    }

    /// <summary>
    /// Exerce les regles de calcul des heures : journee, semaine, heures supplementaires, ISO 8601.
    /// Aucun acces disque, aucune base : uniquement le coeur metier.
    /// </summary>
    private static int VerifierCalcul()
    {
        var echecs = 0;

        void Controle(string libelle, object? attendu, object? obtenu)
        {
            var ok = Equals(attendu, obtenu);
            if (!ok)
            {
                echecs++;
            }

            Console.WriteLine($"  {(ok ? "OK  " : "ECHEC")}  {libelle,-44} attendu={Afficher(attendu),-9} obtenu={Afficher(obtenu)}");
        }

        Console.WriteLine("--- Journee ---");

        var journeeNormale = new JourneeTravail
        {
            Date = new DateOnly(2026, 11, 16),
            Arrivee = new TimeOnly(8, 0),
            PauseDebut = new TimeOnly(12, 0),
            PauseFin = new TimeOnly(13, 0),
            Depart = new TimeOnly(17, 0)
        };
        Controle("8h-17h avec pause 12h-13h", TimeSpan.FromHours(8), journeeNormale.DureeTravaillee);

        var sansPause = new JourneeTravail
        {
            Arrivee = new TimeOnly(8, 0),
            Depart = new TimeOnly(16, 30)
        };
        Controle("8h-16h30 sans pause", new TimeSpan(8, 30, 0), sansPause.DureeTravaillee);

        var pauseIncomplete = new JourneeTravail
        {
            Arrivee = new TimeOnly(8, 0),
            PauseDebut = new TimeOnly(12, 0),
            Depart = new TimeOnly(17, 0)
        };
        Controle("pause sans fin de pause : non deduite", TimeSpan.FromHours(9), pauseIncomplete.DureeTravaillee);

        var nuit = new JourneeTravail
        {
            Arrivee = new TimeOnly(22, 0),
            Depart = new TimeOnly(6, 0)
        };
        Controle("départ avant l'arrivée : aucun total", null, nuit.DureeTravaillee);
        Controle("départ avant l'arrivée : incohérent", false, nuit.EstChronologique);

        var pauseApresMinuit = new JourneeTravail
        {
            Arrivee = new TimeOnly(22, 0),
            PauseDebut = new TimeOnly(2, 0),
            PauseFin = new TimeOnly(2, 30),
            Depart = new TimeOnly(6, 0)
        };
        Controle("pause après minuit : incohérent", false, pauseApresMinuit.EstChronologique);

        var pauseAvantArrivee = new JourneeTravail
        {
            Arrivee = new TimeOnly(8, 0),
            PauseDebut = new TimeOnly(7, 0),
            PauseFin = new TimeOnly(9, 0),
            Depart = new TimeOnly(17, 0)
        };
        Controle("pause avant l'arrivée : incohérent", false, pauseAvantArrivee.EstChronologique);
        Controle("pause avant l'arrivée : aucun total", null, pauseAvantArrivee.DureeTravaillee);

        var pauseInversee = new JourneeTravail
        {
            Arrivee = new TimeOnly(8, 0),
            PauseDebut = new TimeOnly(13, 0),
            PauseFin = new TimeOnly(12, 0),
            Depart = new TimeOnly(17, 0)
        };
        Controle("fin de pause avant son début", false, pauseInversee.EstChronologique);

        Controle("journée ordinaire : cohérente", true, journeeNormale.EstChronologique);
        Controle("journée ordinaire : aucune anomalie", 0, journeeNormale.Valider().Count);

        var incomplete = new JourneeTravail { Arrivee = new TimeOnly(8, 0) };
        Controle("arrivee seule : aucun total", null, incomplete.DureeTravaillee);

        var memeHeure = new JourneeTravail
        {
            Arrivee = new TimeOnly(8, 0),
            Depart = new TimeOnly(8, 0)
        };
        Controle("arrivee = depart", TimeSpan.Zero, memeHeure.DureeTravaillee);

        Console.WriteLine();
        Console.WriteLine("--- Semaine ISO 8601 ---");

        var (anneeIso, numero) = SemaineTravail.SemaineIsoDe(new DateOnly(2026, 11, 17));
        Controle("semaine ISO du 17/11/2026", "2026-W47", $"{anneeIso}-W{numero:00}");

        Controle("lundi de 2026-W47", new DateOnly(2026, 11, 16), SemaineTravail.LundiDeLaSemaine(2026, 47));
        Controle("lundi de 2025-W01", new DateOnly(2024, 12, 30), SemaineTravail.LundiDeLaSemaine(2025, 1));
        Controle("nombre de semaines ISO 2026", 53, SemaineTravail.SemainesDansLAnnee(2026));
        Controle("nombre de semaines ISO 2025", 52, SemaineTravail.SemainesDansLAnnee(2025));

        Console.WriteLine();
        Console.WriteLine("--- Total et heures supplementaires ---");

        var semaine = SemaineTravail.Creer(2026, 47);
        var debut = new TimeOnly(8, 0);
        var fin = new TimeOnly(17, 0);

        for (var index = 0; index < 5; index++)
        {
            semaine.Jours[index].Arrivee = debut;
            semaine.Jours[index].PauseDebut = new TimeOnly(12, 0);
            semaine.Jours[index].PauseFin = new TimeOnly(13, 0);
            semaine.Jours[index].Depart = fin;
        }

        Controle("5 jours de 8h : total semaine", TimeSpan.FromHours(40), semaine.TotalTravaille);
        Controle("contrat 35h : heures supplementaires", TimeSpan.FromHours(5), semaine.HeuresSupplementaires(TimeSpan.FromHours(35)));
        Controle("contrat 39h : heures supplementaires", TimeSpan.FromHours(1), semaine.HeuresSupplementaires(TimeSpan.FromHours(39)));
        Controle("contrat 43h : aucune heure supplementaire", TimeSpan.Zero, semaine.HeuresSupplementaires(TimeSpan.FromHours(43)));

        Console.WriteLine();
        Console.WriteLine("--- Mise en forme ---");

        Controle("formatage 8h30", "08:30", Durees.Formater(new TimeSpan(8, 30, 0)));
        Controle("formatage 40h (total semaine)", "40:00", Durees.Formater(TimeSpan.FromHours(40)));
        Controle("formatage d'une duree absente", "—", Durees.Formater((TimeSpan?)null));

        Console.WriteLine();
        Console.WriteLine(echecs == 0 ? "Calcul des heures : conforme." : $"Calcul des heures : {echecs} echec(s).");

        return echecs;
    }

    private static string Afficher(object? valeur) => valeur switch
    {
        null => "null",
        TimeSpan duree => Durees.Formater(duree),
        _ => valeur.ToString() ?? "null"
    };

    /// <summary>
    /// Exerce la base d'administration de bout en bout et affiche le resultat.
    /// Sert a prouver le bon fonctionnement du socle tant que l'UI n'existe pas.
    /// </summary>
    private static int VerifierDonnees()
    {
        const string identifiant = "verif.test";
        var motDePasse = "Preuve!1234";

        CheminDonnees.Preparer();
        var baseAdmin = new BaseAdministration(CheminDonnees.BaseAdministration);
        baseAdmin.Initialiser();

        Console.WriteLine($"Base    : {baseAdmin.CheminFichier}");

        // Repartir d'un etat propre si un essai precedent a ete interrompu.
        foreach (var existant in baseAdmin.Lister().Where(c => c.Identifiant == identifiant))
        {
            baseAdmin.Supprimer(existant.Id);
        }

        var id = baseAdmin.Creer(
            new Client { Identifiant = identifiant, Nom = "Dupont", Prenom = "Marie" },
            motDePasse);
        Console.WriteLine($"Creation           : id={id}");

        var relu = baseAdmin.Obtenir(id);
        Console.WriteLine($"Relecture          : {relu?.Identifiant} / {relu?.NomComplet} / actif={relu?.Actif}");
        Console.WriteLine($"Contrat par defaut : {relu?.ContratAffiche} ({relu?.ContratHebdomadaireMinutes} min)");

        Console.WriteLine($"Mot de passe juste : {(baseAdmin.VerifierIdentifiants(identifiant, motDePasse) is not null ? "accepte" : "REFUSE")}");
        Console.WriteLine($"Mot de passe faux  : {(baseAdmin.VerifierIdentifiants(identifiant, "mauvais") is not null ? "accepte" : "refuse")}");
        Console.WriteLine($"Identifiant inconnu: {(baseAdmin.VerifierIdentifiants("absent.du.systeme", motDePasse) is not null ? "accepte" : "refuse")}");

        // Preuve que le mot de passe n'est pas stocke en clair : on le cherche dans le fichier.
        // Microsoft.Data.Sqlite conserve ses connexions dans un pool : il faut le vider
        // pour que le fichier ne soit plus verrouille par le processus lui-meme.
        SqliteConnection.ClearAllPools();
        var contientClair = FichierContient(baseAdmin.CheminFichier, motDePasse);
        Console.WriteLine($"En clair en base   : {(contientClair ? "OUI (PROBLEME)" : "non")}");

        relu!.Nom = "Durand";
        relu.Actif = false;
        relu.ContratHebdomadaireMinutes = 39 * 60;
        baseAdmin.Modifier(relu);
        var apresModif = baseAdmin.Obtenir(id);
        Console.WriteLine($"Modification       : {apresModif?.NomComplet} / actif={apresModif?.Actif} / contrat={apresModif?.ContratAffiche}");
        Console.WriteLine($"Inactif => connexion : {(baseAdmin.VerifierIdentifiants(identifiant, motDePasse) is not null ? "acceptee (PROBLEME)" : "refusee")}");

        baseAdmin.Supprimer(id);
        Console.WriteLine($"Suppression        : reste {baseAdmin.Lister().Count(c => c.Identifiant == identifiant)} occurrence(s)");

        return contientClair ? 1 : 0;
    }

    /// <summary>Recherche une chaine ASCII dans un fichier binaire.</summary>
    private static bool FichierContient(string chemin, string texte)
    {
        if (!File.Exists(chemin))
        {
            return false;
        }

        // Ouverture tolerante : le fichier peut encore etre ouvert par le pool de connexions SQLite.
        using var flux = new FileStream(chemin, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var memoire = new MemoryStream();
        flux.CopyTo(memoire);

        var octets = memoire.ToArray();
        var motif = Encoding.ASCII.GetBytes(texte);
        return octets.AsSpan().IndexOf(motif) >= 0;
    }

    /// <summary>
    /// Journalisation : console si elle existe, et toujours un fichier
    /// dans C:\ProgramData\MelTime\logs. Un service lance sans console
    /// doit malgre tout laisser une trace exploitable.
    /// </summary>
    private static void Journaliser(string message)
    {
        var ligne = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";

        try
        {
            Console.WriteLine(ligne);
        }
        catch (Exception)
        {
            // Aucune console attachee : le fichier suffit.
        }

        try
        {
            var dossier = Path.Combine(CheminDonnees.DossierRacine, "logs");
            Directory.CreateDirectory(dossier);

            var fichier = Path.Combine(dossier, $"service-{DateTime.Now:yyyyMMdd}.log");

            lock (VerrouJournal)
            {
                File.AppendAllText(fichier, ligne + Environment.NewLine);
            }
        }
        catch (Exception)
        {
            // Un journal impossible a ecrire ne doit pas empecher le service de tourner.
        }
    }

    private static readonly object VerrouJournal = new();
}
