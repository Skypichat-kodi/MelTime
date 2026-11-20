using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using MelTime.Core.Agenda;
using MelTime.Core.Api;
using MelTime.Core.Modeles;
using MelTime.Core.Protocole;
using MelTime.Service.Donnees;

namespace MelTime.Service.Reseau;

/// <summary>
/// API HTTP du service, destinee aux clients Windows et Android.
/// Ecrite sur HttpListener : aucune bibliotheque supplementaire, aucun
/// runtime additionnel a installer sur la machine.
/// </summary>
public sealed class ServeurHttp
{
    private readonly Configuration _configuration;
    private readonly BaseAdministration _baseAdministration;
    private readonly Action<string> _journaliser;
    private readonly string? _hoteForce;
    private readonly HttpListener _ecouteur = new();

    public ServeurHttp(
        Configuration configuration,
        BaseAdministration baseAdministration,
        Action<string> journaliser,
        string? hoteForce = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _baseAdministration = baseAdministration ?? throw new ArgumentNullException(nameof(baseAdministration));
        _journaliser = journaliser ?? throw new ArgumentNullException(nameof(journaliser));
        _hoteForce = hoteForce;
    }

    /// <summary>Prefixe effectivement ecoute.</summary>
    public string Prefixe => $"http://{_hoteForce ?? _configuration.Hote}:{_configuration.Port}/";

    /// <summary>Boucle d'ecoute HTTP. Se termine quand le jeton d'arret est annule.</summary>
    public async Task ExecuterAsync(CancellationToken arret)
    {
        _ecouteur.Prefixes.Add(Prefixe);

        try
        {
            _ecouteur.Start();
            _journaliser($"Ecoute HTTP : {Prefixe}");
        }
        catch (Exception ex)
        {
            _journaliser($"ERREUR : impossible d'ouvrir {Prefixe} ({ex.Message}).");
            return;
        }

        using var inscription = arret.Register(() =>
        {
            try
            {
                _ecouteur.Stop();
            }
            catch (ObjectDisposedException)
            {
                // Deja arrete.
            }
        });

        while (!arret.IsCancellationRequested)
        {
            HttpListenerContext contexte;

            try
            {
                contexte = await _ecouteur.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                break;
            }

            _ = TraiterAsync(contexte);
        }

        _journaliser("Ecoute HTTP arretee.");
    }

    // ------------------------------------------------------------------
    // Routage
    // ------------------------------------------------------------------

    private async Task TraiterAsync(HttpListenerContext contexte)
    {
        try
        {
            var chemin = (contexte.Request.Url?.AbsolutePath ?? "/").TrimEnd('/').ToLowerInvariant();
            var methode = contexte.Request.HttpMethod;

            // Racine : point de controle immediat depuis un navigateur,
            // sans connaitre les routes de l'API.
            if (methode == "GET" && chemin.Length == 0)
            {
                await RepondreAsync(contexte, 200, new
                {
                    service = "MelTime.Service",
                    etat = "actif",
                    heure = DateTimeOffset.Now
                });
                return;
            }

            // --- Routes publiques ---

            if (methode == "GET" && chemin == "/api/etat")
            {
                await RepondreAsync(contexte, 200, new { succes = true, message = "MelTime.Service actif." });
                return;
            }

            if (methode == "POST" && chemin == "/api/connexion")
            {
                await TraiterConnexionAsync(contexte);
                return;
            }

            if (methode == "POST" && chemin == "/api/deconnexion")
            {
                await TraiterDeconnexionAsync(contexte);
                return;
            }

            // --- Routes protegees : un jeton valide est obligatoire ---

            if (chemin.StartsWith("/api/", StringComparison.Ordinal))
            {
                var client = ClientAuthentifie(contexte.Request);

                if (client is null)
                {
                    await RepondreAsync(contexte, 401, new ReponseErreur
                    {
                        Succes = false,
                        Erreur = "Session invalide ou expirée."
                    });
                    return;
                }

                if (methode == "GET" && chemin == "/api/profil")
                {
                    await RepondreAsync(contexte, 200, client);
                    return;
                }

                if (methode == "GET" && chemin == "/api/semaines")
                {
                    var semaines = BaseDuClient(client.Id)
                        .ListerSemaines()
                        .Select(s => new { anneeIso = s.AnneeIso, numero = s.Numero });

                    await RepondreAsync(contexte, 200, semaines);
                    return;
                }

                if (chemin == "/api/horaires-defaut")
                {
                    await TraiterHorairesDefautAsync(contexte, client, methode);
                    return;
                }

                if (chemin == "/api/reglages")
                {
                    await TraiterReglagesAsync(contexte, client, methode);
                    return;
                }

                if (chemin.StartsWith("/api/semaine/", StringComparison.Ordinal))
                {
                    await TraiterSemaineAsync(contexte, client, chemin, methode);
                    return;
                }

                if (methode == "GET" && chemin.StartsWith("/api/mois/", StringComparison.Ordinal))
                {
                    await TraiterMoisAsync(contexte, client, chemin);
                    return;
                }

                if (methode == "GET" && chemin == "/api/solde")
                {
                    var (supplementaires, manquantes, conges, maladie) = BaseDuClient(client.Id).SoldeCumule(client.ContratHebdomadaireMinutes, client.DebutContrat);

                    await RepondreAsync(contexte, 200, new SoldeApi
                    {
                        Semaines = 52,
                        Supplementaires = Durees.Formater(supplementaires),
                        Manquantes = Durees.Formater(manquantes),
                        CongesPris = conges,
                        CongesAcquis = 25,
                        JoursMaladie = maladie
                    });
                    return;
                }
            }

            await RepondreAsync(contexte, 404, new ReponseErreur { Succes = false, Erreur = "Route inconnue." });
            return;
        }
        catch (Exception ex)
        {
            _journaliser($"Erreur HTTP : {ex.Message}");

            try
            {
                await RepondreAsync(contexte, 500, new ReponseErreur { Succes = false, Erreur = "Erreur interne du service." });
            }
            catch (Exception)
            {
                // Le client a pu se deconnecter entre-temps : rien a faire.
            }
        }
        finally
        {
            try
            {
                contexte.Response.Close();
            }
            catch (Exception)
            {
                // Deja ferme.
            }
        }
    }

    private async Task TraiterConnexionAsync(HttpListenerContext contexte)
    {
        var demande = await LireCorpsAsync<DemandeConnexion>(contexte.Request);

        if (demande is null
            || string.IsNullOrWhiteSpace(demande.Identifiant)
            || string.IsNullOrEmpty(demande.MotDePasse))
        {
            await RepondreAsync(contexte, 400, new ReponseConnexion
            {
                Succes = false,
                Erreur = "Identifiant et mot de passe obligatoires."
            });
            return;
        }

        var client = _baseAdministration.VerifierIdentifiants(demande.Identifiant, demande.MotDePasse);

        if (client is null)
        {
            _journaliser($"Connexion refusee pour « {demande.Identifiant} ».");
            await RepondreAsync(contexte, 401, new ReponseConnexion
            {
                Succes = false,
                Erreur = "Identifiant ou mot de passe incorrect."
            });
            return;
        }

        var duree = TimeSpan.FromDays(_configuration.DureeSessionJours);
        var jeton = _baseAdministration.CreerSession(client.Id, duree);
        _baseAdministration.EnregistrerConnexion(client.Id);
        _baseAdministration.PurgerSessionsExpirees();

        _journaliser($"Client connecte : {client.Identifiant} ({client.NomComplet})");

        await RepondreAsync(contexte, 200, new ReponseConnexion
        {
            Succes = true,
            Jeton = jeton,
            ExpireLe = DateTimeOffset.UtcNow.Add(duree),
            Client = client
        });
    }

    private async Task TraiterDeconnexionAsync(HttpListenerContext contexte)
    {
        var jeton = ExtraireJeton(contexte.Request);

        if (!string.IsNullOrEmpty(jeton))
        {
            _baseAdministration.SupprimerSession(jeton);
        }

        await RepondreAsync(contexte, 200, new { succes = true });
    }

    /// <summary>
    /// Lecture et ecriture d'une feuille de temps : /api/semaine/{annee}/{numero}.
    /// Le service reste seul a toucher la base du client.
    /// </summary>
    private async Task TraiterSemaineAsync(HttpListenerContext contexte, Client client, string chemin, string methode)
    {
        var segments = chemin.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length != 4
            || !int.TryParse(segments[2], NumberStyles.None, CultureInfo.InvariantCulture, out var anneeIso)
            || !int.TryParse(segments[3], NumberStyles.None, CultureInfo.InvariantCulture, out var numero))
        {
            await RepondreAsync(contexte, 400, new ReponseErreur
            {
                Succes = false,
                Erreur = "Adresse de semaine invalide."
            });
            return;
        }

        var total = SemaineTravail.SemainesDansLAnnee(anneeIso);

        if (numero < 1 || numero > total)
        {
            await RepondreAsync(contexte, 400, new ReponseErreur
            {
                Succes = false,
                Erreur = $"L'année {anneeIso} ne compte pas de semaine {numero}."
            });
            return;
        }

        var baseClient = BaseDuClient(client.Id);

        if (methode == "GET")
        {
            var semaine = baseClient.Charger(anneeIso, numero);

            if (semaine is null)
            {
                // Semaine jamais saisie : on renvoie une grille vierge au contrat
                // du client, deja remplie avec son modele d'horaires s'il en a un.
                semaine = SemaineTravail.Creer(anneeIso, numero);
                semaine.ContratHebdomadaireMinutes = client.ContratHebdomadaireMinutes;

                AppliquerHorairesDefaut(semaine, baseClient.ChargerHorairesTypes());
            }

            await RepondreAsync(contexte, 200, SemaineApi.Depuis(semaine, client.Pays));
            return;
        }

        if (methode == "PUT")
        {
            var recue = await LireCorpsAsync<SemaineApi>(contexte.Request);

            if (recue is null)
            {
                await RepondreAsync(contexte, 400, new ReponseErreur
                {
                    Succes = false,
                    Erreur = "Contenu de la semaine manquant."
                });
                return;
            }

            // L'adresse fait foi : elle protege d'un corps incoherent.
            recue.AnneeIso = anneeIso;
            recue.Numero = numero;

            if (recue.ContratHebdomadaireMinutes <= 0)
            {
                recue.ContratHebdomadaireMinutes = client.ContratHebdomadaireMinutes;
            }

            var semaine = recue.VersModele();

            // Le serveur refuse une feuille incoherente, meme si le client l'a laissee passer :
            // les donnees enregistrees doivent toujours etre exploitables.
            var anomalies = semaine.Valider();

            if (anomalies.Count > 0)
            {
                await RepondreAsync(contexte, 400, new ReponseErreur
                {
                    Succes = false,
                    Erreur = string.Join(" ", anomalies)
                });
                return;
            }

            baseClient.Enregistrer(semaine);

            _journaliser($"Feuille de temps enregistree : {client.Identifiant}, {anneeIso}-W{numero:00}");

            await RepondreAsync(contexte, 200, SemaineApi.Depuis(semaine, client.Pays));
            return;
        }

        await RepondreAsync(contexte, 405, new ReponseErreur
        {
            Succes = false,
            Erreur = "Seules les methodes GET et PUT sont acceptees sur cette adresse."
        });
    }

    private static BaseClient BaseDuClient(int idClient)
    {
        var baseClient = new BaseClient(CheminDonnees.BaseClient(idClient));
        baseClient.Initialiser();
        return baseClient;
    }

    /// <summary>Recopie le modele d'horaires sur une semaine vierge.</summary>
    private static void AppliquerHorairesDefaut(SemaineTravail semaine, HorairesTypes types)
    {
        var modele = types.ModelePour(semaine.AnneeIso, semaine.Numero);

        if (modele is null)
        {
            return;
        }

        for (var index = 0; index < semaine.Jours.Count && index < modele.Length; index++)
        {
            var jour = semaine.Jours[index];
            var defaut = modele[index];

            if (defaut.EstVide)
            {
                continue;
            }

            jour.Arrivee = defaut.Arrivee;
            jour.PauseDebut = defaut.PauseDebut;
            jour.PauseFin = defaut.PauseFin;
            jour.Depart = defaut.Depart;
        }
    }

    /// <summary>
    /// Modele d'horaires du client : /api/horaires-defaut.
    /// Chaque client regle le sien depuis son propre client Windows.
    /// </summary>
    private async Task TraiterHorairesDefautAsync(HttpListenerContext contexte, Client client, string methode)
    {
        var baseClient = BaseDuClient(client.Id);

        if (methode == "GET")
        {
            await RepondreAsync(contexte, 200, VersApi(baseClient.ChargerHorairesTypes()));
            return;
        }

        if (methode != "PUT")
        {
            await RepondreAsync(contexte, 405, new ReponseErreur
            {
                Succes = false,
                Erreur = "Seules les methodes GET et PUT sont acceptees sur cette adresse."
            });
            return;
        }

        var recu = await LireCorpsAsync<HorairesDefautApi>(contexte.Request);

        if (recu is null)
        {
            await RepondreAsync(contexte, 400, new ReponseErreur
            {
                Succes = false,
                Erreur = "Contenu du modele manquant."
            });
            return;
        }

        var types = new HorairesTypes();

        if (DateOnly.TryParseExact(
                recu.DebutCycle ?? string.Empty,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var debut))
        {
            types.DebutCycle = debut;
        }

        foreach (var modeleRecu in recu.Modeles)
        {
            if (modeleRecu.Index < 0 || modeleRecu.Index >= HorairesTypes.NombreDeModeles)
            {
                continue;
            }

            types.Actifs[modeleRecu.Index] = modeleRecu.Actif;

            foreach (var jourRecu in modeleRecu.Jours)
            {
                if (jourRecu.Index < 0 || jourRecu.Index > 6)
                {
                    continue;
                }

                types.Modeles[modeleRecu.Index][jourRecu.Index] = new HorairesJour
                {
                    Arrivee = EnHeure(jourRecu.Arrivee),
                    PauseDebut = EnHeure(jourRecu.PauseDebut),
                    PauseFin = EnHeure(jourRecu.PauseFin),
                    Depart = EnHeure(jourRecu.Depart)
                };
            }
        }

        baseClient.EnregistrerHorairesTypes(types);
        _journaliser($"Horaires types enregistres : {client.Identifiant}");

        await RepondreAsync(contexte, 200, VersApi(baseClient.ChargerHorairesTypes()));
    }

    /// <summary>
    /// Reglages personnels du client : /api/reglages.
    /// Ils servent au calcul du prix de l'heure, cote client.
    /// </summary>
    private async Task TraiterReglagesAsync(HttpListenerContext contexte, Client client, string methode)
    {
        var baseClient = BaseDuClient(client.Id);

        if (methode == "GET")
        {
            await RepondreAsync(contexte, 200, LireReglages(baseClient));
            return;
        }

        if (methode != "PUT")
        {
            await RepondreAsync(contexte, 405, new ReponseErreur
            {
                Succes = false,
                Erreur = "Seules les methodes GET et PUT sont acceptees sur cette adresse."
            });
            return;
        }

        var recu = await LireCorpsAsync<ReglagesApi>(contexte.Request);

        if (recu is null)
        {
            await RepondreAsync(contexte, 400, new ReponseErreur
            {
                Succes = false,
                Erreur = "Contenu des reglages manquant."
            });
            return;
        }

        baseClient.EcrireReglage("salaire_brut", recu.SalaireBrut?.ToString(CultureInfo.InvariantCulture));
        baseClient.EcrireReglage("salaire_net", recu.SalaireNet?.ToString(CultureInfo.InvariantCulture));

        await RepondreAsync(contexte, 200, LireReglages(baseClient));
    }

    private static ReglagesApi LireReglages(BaseClient baseClient) => new()
    {
        SalaireBrut = EnDecimal(baseClient.LireReglage("salaire_brut")),
        SalaireNet = EnDecimal(baseClient.LireReglage("salaire_net"))
    };

    private static decimal? EnDecimal(string? texte) =>
        decimal.TryParse(texte, NumberStyles.Any, CultureInfo.InvariantCulture, out var valeur)
            ? valeur
            : null;

    private static HorairesDefautApi VersApi(HorairesTypes types)
    {
        var api = new HorairesDefautApi
        {
            DebutCycle = types.DebutCycle?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };

        for (var modele = 0; modele < HorairesTypes.NombreDeModeles; modele++)
        {
            var entree = new ModeleApi
            {
                Index = modele,
                Actif = types.Actifs[modele]
            };

            for (var jour = 0; jour < 7; jour++)
            {
                var journee = types.Modeles[modele][jour];

                entree.Jours.Add(new JourDefautApi
                {
                    Index = jour,
                    Arrivee = EnTexte(journee.Arrivee),
                    PauseDebut = EnTexte(journee.PauseDebut),
                    PauseFin = EnTexte(journee.PauseFin),
                    Depart = EnTexte(journee.Depart)
                });
            }

            api.Modeles.Add(entree);
        }

        return api;
    }

    private static string? EnTexte(TimeOnly? heure) =>
        heure?.ToString(SemaineApi.FormatHeure, CultureInfo.InvariantCulture);

    private static TimeOnly? EnHeure(string? texte) =>
        string.IsNullOrWhiteSpace(texte)
            ? null
            : TimeOnly.TryParseExact(texte, SemaineApi.FormatHeure, CultureInfo.InvariantCulture, DateTimeStyles.None, out var heure)
                ? heure
                : null;

    /// <summary>
    /// Total des heures d'un mois : /api/mois/{annee}/{mois}.
    /// Calcule par le service, car une semaine ISO peut chevaucher deux mois
    /// et le client ne charge qu'une semaine a la fois.
    /// </summary>
    private async Task TraiterMoisAsync(HttpListenerContext contexte, Client client, string chemin)
    {
        var segments = chemin.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length != 4
            || !int.TryParse(segments[2], NumberStyles.None, CultureInfo.InvariantCulture, out var annee)
            || !int.TryParse(segments[3], NumberStyles.None, CultureInfo.InvariantCulture, out var mois)
            || mois < 1 || mois > 12
            || annee < 1900 || annee > 9999)
        {
            await RepondreAsync(contexte, 400, new ReponseErreur
            {
                Succes = false,
                Erreur = "Adresse de mois invalide."
            });
            return;
        }

        var total = BaseDuClient(client.Id).TotalDuMois(annee, mois);
        var (moisSup, moisManque) = BaseDuClient(client.Id).BilanDuMois(annee, mois, client.ContratHebdomadaireMinutes);

        await RepondreAsync(contexte, 200, new TotalMoisApi
        {
            Annee = annee,
            Mois = mois,
            TotalTravaille = Durees.Formater(total),
            Supplementaires = Durees.Formater(moisSup),
            Manquantes = Durees.Formater(moisManque)
        });
    }

    // ------------------------------------------------------------------
    // Outils
    // ------------------------------------------------------------------

    /// <summary>Lit le jeton de l'en-tete « Authorization: Bearer xxx ».</summary>
    private static string? ExtraireJeton(HttpListenerRequest requete)
    {
        var entete = requete.Headers["Authorization"];

        if (string.IsNullOrWhiteSpace(entete))
        {
            return null;
        }

        const string prefixe = "Bearer ";

        return entete.StartsWith(prefixe, StringComparison.OrdinalIgnoreCase)
            ? entete[prefixe.Length..].Trim()
            : null;
    }

    /// <summary>Retourne le client authentifie, ou null si le jeton est absent ou invalide.</summary>
    private Client? ClientAuthentifie(HttpListenerRequest requete) =>
        _baseAdministration.ClientDeSession(ExtraireJeton(requete) ?? string.Empty);

    private static async Task<T?> LireCorpsAsync<T>(HttpListenerRequest requete)
    {
        using var lecteur = new StreamReader(requete.InputStream, Encoding.UTF8);
        var texte = await lecteur.ReadToEndAsync().ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(texte)
            ? default
            : JsonSerializer.Deserialize<T>(texte, ProtocoleJson.Options);
    }

    private static async Task RepondreAsync(HttpListenerContext contexte, int code, object corps)
    {
        var texte = JsonSerializer.Serialize(corps, ProtocoleJson.Options);
        var octets = Encoding.UTF8.GetBytes(texte);

        contexte.Response.StatusCode = code;
        contexte.Response.ContentType = "application/json; charset=utf-8";
        contexte.Response.ContentLength64 = octets.Length;

        await contexte.Response.OutputStream.WriteAsync(octets).ConfigureAwait(false);
        await contexte.Response.OutputStream.FlushAsync().ConfigureAwait(false);
    }
}
