using System.Globalization;
using MelTime.Core.Agenda;
using MelTime.Core.Modeles;
using Microsoft.Data.Sqlite;

namespace MelTime.Service.Donnees;

/// <summary>
/// Base de donnees propre a un client : ses feuilles de temps.
/// Un fichier par client, isole des autres : la sauvegarde et la suppression
/// d'un client se ramenent a une operation sur un seul fichier.
/// Seuls les horaires saisis sont stockes : les totaux sont toujours recalcules.
/// </summary>
public sealed class BaseClient
{
    private const string FormatHeure = "HH:mm";

    /// <summary>
    /// Seuil legal hebdomadaire en France : au-dela de 35 h, les heures
    /// accomplies sont des heures supplementaires, quel que soit le contrat.
    /// Un contrat de 36 h 45 en produit donc 1 h 45 chaque semaine.
    /// </summary>
    private static readonly TimeSpan SeuilLegal = TimeSpan.FromHours(35);

    private readonly string _chaineConnexion;

    public BaseClient(string cheminFichier)
    {
        if (string.IsNullOrWhiteSpace(cheminFichier))
        {
            throw new ArgumentException("Le chemin de la base est obligatoire.", nameof(cheminFichier));
        }

        CheminFichier = cheminFichier;

        var dossier = Path.GetDirectoryName(cheminFichier);
        if (!string.IsNullOrEmpty(dossier))
        {
            Directory.CreateDirectory(dossier);
        }

        _chaineConnexion = new SqliteConnectionStringBuilder
        {
            DataSource = cheminFichier,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    public string CheminFichier { get; }

    /// <summary>Cree les tables si necessaire.</summary>
    public void Initialiser()
    {
        using var connexion = Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = """
            CREATE TABLE IF NOT EXISTS semaines (
                annee_iso       INTEGER NOT NULL,
                numero          INTEGER NOT NULL,
                contrat_minutes INTEGER NOT NULL DEFAULT 2100,
                PRIMARY KEY (annee_iso, numero)
            );

            CREATE TABLE IF NOT EXISTS journees (
                annee_iso   INTEGER NOT NULL,
                numero      INTEGER NOT NULL,
                jour        INTEGER NOT NULL,
                arrivee     TEXT    NULL,
                pause_debut TEXT    NULL,
                pause_fin   TEXT    NULL,
                depart      TEXT    NULL,
                maladie     INTEGER NOT NULL DEFAULT 0,
                conge       INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (annee_iso, numero, jour),
                FOREIGN KEY (annee_iso, numero) REFERENCES semaines (annee_iso, numero) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS horaires_defaut (
                jour        INTEGER PRIMARY KEY,
                arrivee     TEXT    NULL,
                pause_debut TEXT    NULL,
                pause_fin   TEXT    NULL,
                depart      TEXT    NULL
            );

            CREATE TABLE IF NOT EXISTS reglages (
                cle    TEXT PRIMARY KEY,
                valeur TEXT NULL
            );
            """;
        commande.ExecuteNonQuery();

        MigrerSchema(connexion);
    }

    /// <summary>
    /// Ajoute les colonnes apparues apres la premiere version.
    /// Chaque client possede sa propre base : sans cette etape, une base deja
    /// creee resterait sans les nouvelles colonnes, et les requetes echoueraient.
    /// </summary>
    private static void MigrerSchema(SqliteConnection connexion)
    {
        using var inventaire = connexion.CreateCommand();
        inventaire.CommandText = "PRAGMA table_info(journees);";

        var colonnes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (var lecteur = inventaire.ExecuteReader())
        {
            while (lecteur.Read())
            {
                colonnes.Add(lecteur.GetString(1));
            }
        }

        if (!colonnes.Contains("maladie"))
        {
            using var migration = connexion.CreateCommand();
            migration.CommandText = "ALTER TABLE journees ADD COLUMN maladie INTEGER NOT NULL DEFAULT 0;";
            migration.ExecuteNonQuery();
        }

        if (!colonnes.Contains("conge"))
        {
            using var migration = connexion.CreateCommand();
            migration.CommandText = "ALTER TABLE journees ADD COLUMN conge INTEGER NOT NULL DEFAULT 0;";
            migration.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Charge une semaine. Retourne null si elle n'a jamais ete saisie.
    /// </summary>
    public SemaineTravail? Charger(int anneeIso, int numero)
    {
        using var connexion = Ouvrir();

        int contrat;

        using (var commande = connexion.CreateCommand())
        {
            commande.CommandText = """
                SELECT contrat_minutes FROM semaines
                WHERE annee_iso = $annee AND numero = $numero;
                """;
            commande.Parameters.AddWithValue("$annee", anneeIso);
            commande.Parameters.AddWithValue("$numero", numero);

            var valeur = commande.ExecuteScalar();

            if (valeur is null or DBNull)
            {
                return null;
            }

            contrat = Convert.ToInt32(valeur, CultureInfo.InvariantCulture);
        }

        var semaine = SemaineTravail.Creer(anneeIso, numero);
        semaine.ContratHebdomadaireMinutes = contrat;

        using (var commande = connexion.CreateCommand())
        {
            commande.CommandText = """
                SELECT jour, arrivee, pause_debut, pause_fin, depart, maladie, conge FROM journees
                WHERE annee_iso = $annee AND numero = $numero;
                """;
            commande.Parameters.AddWithValue("$annee", anneeIso);
            commande.Parameters.AddWithValue("$numero", numero);

            using var lecteur = commande.ExecuteReader();

            while (lecteur.Read())
            {
                var index = lecteur.GetInt32(0);

                if (index < 0 || index >= semaine.Jours.Count)
                {
                    continue;
                }

                var jour = semaine.Jours[index];
                jour.Arrivee = LireHeure(lecteur, 1);
                jour.PauseDebut = LireHeure(lecteur, 2);
                jour.PauseFin = LireHeure(lecteur, 3);
                jour.Depart = LireHeure(lecteur, 4);
                jour.Maladie = lecteur.GetInt32(5) != 0;
                jour.Conge = lecteur.GetInt32(6) != 0;
            }
        }

        // Une journee d'arret ou de conges est payee au volume prevu : on lui
        // donne les horaires du modele, faute de quoi elle apparaitrait comme
        // un manque par rapport au contrat.
        if (semaine.Jours.Any(j => j.Maladie || j.Conge))
        {
            var modele = ChargerHorairesDefaut();

            for (var index = 0; index < semaine.Jours.Count && index < modele.Length; index++)
            {
                var jour = semaine.Jours[index];

                if ((!jour.Maladie && !jour.Conge) || modele[index].EstVide)
                {
                    continue;
                }

                jour.Arrivee = modele[index].Arrivee;
                jour.PauseDebut = modele[index].PauseDebut;
                jour.PauseFin = modele[index].PauseFin;
                jour.Depart = modele[index].Depart;
            }
        }

        return semaine;
    }

    /// <summary>
    /// Enregistre une semaine complete : le contrat effectif et les sept journees.
    /// Les donnees existantes sont remplacees, dans une seule transaction.
    /// </summary>
    public void Enregistrer(SemaineTravail semaine)
    {
        ArgumentNullException.ThrowIfNull(semaine);

        using var connexion = Ouvrir();
        using var transaction = connexion.BeginTransaction();

        using (var commande = connexion.CreateCommand())
        {
            commande.Transaction = transaction;
            commande.CommandText = """
                INSERT INTO semaines (annee_iso, numero, contrat_minutes)
                VALUES ($annee, $numero, $contrat)
                ON CONFLICT (annee_iso, numero)
                DO UPDATE SET contrat_minutes = excluded.contrat_minutes;
                """;
            commande.Parameters.AddWithValue("$annee", semaine.AnneeIso);
            commande.Parameters.AddWithValue("$numero", semaine.Numero);
            commande.Parameters.AddWithValue("$contrat", semaine.ContratHebdomadaireMinutes);
            commande.ExecuteNonQuery();
        }

        for (var index = 0; index < semaine.Jours.Count; index++)
        {
            var jour = semaine.Jours[index];

            using var commande = connexion.CreateCommand();
            commande.Transaction = transaction;
            commande.CommandText = """
                INSERT INTO journees (annee_iso, numero, jour, arrivee, pause_debut, pause_fin, depart, maladie, conge)
                VALUES ($annee, $numero, $jour, $arrivee, $pauseDebut, $pauseFin, $depart, $maladie, $conge)
                ON CONFLICT (annee_iso, numero, jour)
                DO UPDATE SET
                    arrivee = excluded.arrivee,
                    pause_debut = excluded.pause_debut,
                    pause_fin = excluded.pause_fin,
                    depart = excluded.depart,
                    maladie = excluded.maladie,
                    conge = excluded.conge;
                """;
            commande.Parameters.AddWithValue("$annee", semaine.AnneeIso);
            commande.Parameters.AddWithValue("$numero", semaine.Numero);
            commande.Parameters.AddWithValue("$jour", index);
            commande.Parameters.AddWithValue("$arrivee", EnParametre(jour.Arrivee));
            commande.Parameters.AddWithValue("$pauseDebut", EnParametre(jour.PauseDebut));
            commande.Parameters.AddWithValue("$pauseFin", EnParametre(jour.PauseFin));
            commande.Parameters.AddWithValue("$depart", EnParametre(jour.Depart));
            commande.Parameters.AddWithValue("$maladie", jour.Maladie ? 1 : 0);
            commande.Parameters.AddWithValue("$conge", jour.Conge ? 1 : 0);
            commande.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    /// <summary>
    /// Nombre de jours de conges payes poses sur les 52 dernieres semaines.
    /// Seules les semaines enregistrees peuvent en contenir : une semaine
    /// jamais saisie n'a pas de conge.
    /// </summary>
    public int CompterConges(int nombreDeSemaines = 52)
    {
        var (anneeCourante, numeroCourant) = SemaineTravail.SemaineIsoDe(DateOnly.FromDateTime(DateTime.Today));
        var lundiCourant = SemaineTravail.LundiDeLaSemaine(anneeCourante, numeroCourant);

        var jours = 0;

        for (var decalage = nombreDeSemaines - 1; decalage >= 0; decalage--)
        {
            var lundi = lundiCourant.AddDays(-7 * decalage);
            var (anneeIso, numero) = SemaineTravail.SemaineIsoDe(lundi);

            var semaine = Charger(anneeIso, numero);

            if (semaine is null)
            {
                continue;
            }

            jours += semaine.Jours.Count(j => j.Conge);
        }

        return jours;
    }

    /// <summary>Liste des semaines enregistrees, de la plus recente a la plus ancienne.</summary>
    public IReadOnlyList<(int AnneeIso, int Numero)> ListerSemaines()
    {
        using var connexion = Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = """
            SELECT annee_iso, numero FROM semaines
            ORDER BY annee_iso DESC, numero DESC;
            """;

        using var lecteur = commande.ExecuteReader();
        var liste = new List<(int, int)>();

        while (lecteur.Read())
        {
            liste.Add((lecteur.GetInt32(0), lecteur.GetInt32(1)));
        }

        return liste;
    }

    /// <summary>
    /// Total des heures travaillees sur un mois civil.
    ///
    /// Toutes les semaines du mois sont prises en compte, y compris celles
    /// qui n'ont jamais ete enregistrees : elles sont alors remplies avec le
    /// modele d'horaires, exactement comme le fait la feuille de temps.
    /// Le total affiche correspond donc bien a ce que le client voit.
    /// </summary>
    public TimeSpan TotalDuMois(int annee, int mois)
    {
        var premier = new DateOnly(annee, mois, 1);
        var dernier = premier.AddMonths(1).AddDays(-1);

        // Semaines ISO qui touchent le mois : au plus six.
        var semaines = new HashSet<(int AnneeIso, int Numero)>();

        for (var jour = premier; jour <= dernier; jour = jour.AddDays(1))
        {
            semaines.Add(SemaineTravail.SemaineIsoDe(jour));
        }

        var modele = ChargerHorairesDefaut();
        var total = TimeSpan.Zero;

        foreach (var (anneeIso, numero) in semaines)
        {
            var semaine = Charger(anneeIso, numero);

            if (semaine is null)
            {
                semaine = SemaineTravail.Creer(anneeIso, numero);
                AppliquerModele(semaine, modele);
            }

            foreach (var jour in semaine.Jours)
            {
                if (jour.Date.Year == annee && jour.Date.Month == mois)
                {
                    total += jour.DureeTravaillee ?? TimeSpan.Zero;
                }
            }
        }

        return total;
    }

    /// <summary>
    /// Solde des heures sur les 52 dernieres semaines, semaine courante incluse.
    ///
    /// Retourne deux valeurs distinctes :
    ///   - Supplementaires : cumul de ce qui depasse le seuil legal de 35 h.
    ///     Un contrat de 36 h 45 en produit 1 h 45 par semaine, meme lorsque
    ///     le client ne fait ni plus ni moins que son contrat.
    ///   - Ecart : difference avec le contrat du client. Negatif en cas de
    ///     manque, positif en cas de surplus.
    ///
    /// Les semaines jamais saisies comptent avec le modele d'horaires, et
    /// avec le contrat de la fiche client, comme pour le total du mois.
    /// </summary>
    public (TimeSpan Supplementaires, TimeSpan Manquantes, int Conges, int Maladie) SoldeCumule(int contratParDefautMinutes, DateOnly? debutContrat = null, int nombreDeSemaines = 52)
    {
        var (anneeCourante, numeroCourant) = SemaineTravail.SemaineIsoDe(DateOnly.FromDateTime(DateTime.Today));

        var lundiCourant = SemaineTravail.LundiDeLaSemaine(anneeCourante, numeroCourant);
        var modele = ChargerHorairesDefaut();

        var supplementaires = TimeSpan.Zero;
        var manquantes = TimeSpan.Zero;
        var conges = 0;
        var maladie = 0;

        for (var decalage = nombreDeSemaines - 1; decalage >= 0; decalage--)
        {
            var lundi = lundiCourant.AddDays(-7 * decalage);

            // Semaine entierement anterieure au contrat : elle ne compte pas.
            // Sans cela, un client embauche en mars trainerait un manque pour
            // janvier et fevrier.
            if (debutContrat is { } debut && lundi.AddDays(6) < debut)
            {
                continue;
            }

            var (anneeIso, numero) = SemaineTravail.SemaineIsoDe(lundi);

            var semaine = Charger(anneeIso, numero);

            if (semaine is null)
            {
                // Modele d'horaires vide : la semaine ne prevoit aucune heure.
                // La compter creerait un manque artificiel de tout le contrat,
                // repete sur cinquante semaines.
                if (modele.All(j => j.EstVide))
                {
                    continue;
                }

                semaine = SemaineTravail.Creer(anneeIso, numero);
                semaine.ContratHebdomadaireMinutes = contratParDefautMinutes;
                AppliquerModele(semaine, modele);
            }

            Cumuler(semaine, ref supplementaires, ref manquantes);

            conges += semaine.Jours.Count(j => j.Conge);
            maladie += semaine.Jours.Count(j => j.Maladie);
        }

        return (supplementaires, manquantes, conges, maladie);
    }

    /// <summary>
    /// Bilan d'un mois civil : heures au-dela de 35 h d'un cote, heures
    /// manquantes par rapport au contrat de l'autre.
    /// </summary>
    public (TimeSpan Supplementaires, TimeSpan Manquantes) BilanDuMois(int annee, int mois, int contratParDefautMinutes)
    {
        var premier = new DateOnly(annee, mois, 1);
        var dernier = premier.AddMonths(1).AddDays(-1);

        var semaines = new HashSet<(int AnneeIso, int Numero)>();

        for (var jour = premier; jour <= dernier; jour = jour.AddDays(1))
        {
            semaines.Add(SemaineTravail.SemaineIsoDe(jour));
        }

        var modele = ChargerHorairesDefaut();
        var supplementaires = TimeSpan.Zero;
        var manquantes = TimeSpan.Zero;

        foreach (var (anneeIso, numero) in semaines)
        {
            var semaine = Charger(anneeIso, numero);

            if (semaine is null)
            {
                if (modele.All(j => j.EstVide))
                {
                    continue;
                }

                semaine = SemaineTravail.Creer(anneeIso, numero);
                semaine.ContratHebdomadaireMinutes = contratParDefautMinutes;
                AppliquerModele(semaine, modele);
            }

            Cumuler(semaine, ref supplementaires, ref manquantes);
        }

        return (supplementaires, manquantes);
    }

    /// <summary>
    /// Ajoute le bilan d'une semaine : au-dela de 35 h ce sont des heures
    /// supplementaires ; en dessous du contrat, des heures manquantes.
    /// Les deux se cumulent separement, sans se compenser.
    /// </summary>
    private static void Cumuler(SemaineTravail semaine, ref TimeSpan supplementaires, ref TimeSpan manquantes)
    {
        var travaille = semaine.TotalTravaille;
        var contrat = TimeSpan.FromMinutes(semaine.ContratHebdomadaireMinutes);

        if (travaille > SeuilLegal)
        {
            supplementaires += travaille - SeuilLegal;
        }

        if (travaille < contrat)
        {
            manquantes += contrat - travaille;
        }
    }

    /// <summary>Recopie le modele d'horaires sur une semaine.</summary>
    private static void AppliquerModele(SemaineTravail semaine, IReadOnlyList<HorairesJour> modele)
    {
        for (var index = 0; index < semaine.Jours.Count && index < modele.Count; index++)
        {
            var defaut = modele[index];

            if (defaut.EstVide)
            {
                continue;
            }

            var jour = semaine.Jours[index];
            jour.Arrivee = defaut.Arrivee;
            jour.PauseDebut = defaut.PauseDebut;
            jour.PauseFin = defaut.PauseFin;
            jour.Depart = defaut.Depart;
        }
    }

    /// <summary>
    /// Modele d'horaires du client : sept journees, du lundi au dimanche.
    /// Retourne un tableau vide si aucun modele n'a ete enregistre.
    /// </summary>
    public HorairesJour[] ChargerHorairesDefaut()
    {
        var jours = new HorairesJour[7];

        for (var index = 0; index < jours.Length; index++)
        {
            jours[index] = new HorairesJour();
        }

        using var connexion = Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = "SELECT jour, arrivee, pause_debut, pause_fin, depart FROM horaires_defaut;";

        using var lecteur = commande.ExecuteReader();

        while (lecteur.Read())
        {
            var index = lecteur.GetInt32(0);

            if (index < 0 || index >= jours.Length)
            {
                continue;
            }

            jours[index] = new HorairesJour
            {
                Arrivee = LireHeure(lecteur, 1),
                PauseDebut = LireHeure(lecteur, 2),
                PauseFin = LireHeure(lecteur, 3),
                Depart = LireHeure(lecteur, 4)
            };
        }

        return jours;
    }

    /// <summary>Enregistre le modele d'horaires, en remplacant le precedent.</summary>
    public void EnregistrerHorairesDefaut(IReadOnlyList<HorairesJour> jours)
    {
        ArgumentNullException.ThrowIfNull(jours);

        using var connexion = Ouvrir();
        using var transaction = connexion.BeginTransaction();

        using (var purge = connexion.CreateCommand())
        {
            purge.Transaction = transaction;
            purge.CommandText = "DELETE FROM horaires_defaut;";
            purge.ExecuteNonQuery();
        }

        for (var index = 0; index < jours.Count && index < 7; index++)
        {
            var jour = jours[index];

            if (jour.EstVide)
            {
                continue;
            }

            using var insertion = connexion.CreateCommand();
            insertion.Transaction = transaction;
            insertion.CommandText = """
                INSERT INTO horaires_defaut (jour, arrivee, pause_debut, pause_fin, depart)
                VALUES ($jour, $arrivee, $pauseDebut, $pauseFin, $depart);
                """;
            insertion.Parameters.AddWithValue("$jour", index);
            insertion.Parameters.AddWithValue("$arrivee", EnParametre(jour.Arrivee));
            insertion.Parameters.AddWithValue("$pauseDebut", EnParametre(jour.PauseDebut));
            insertion.Parameters.AddWithValue("$pauseFin", EnParametre(jour.PauseFin));
            insertion.Parameters.AddWithValue("$depart", EnParametre(jour.Depart));
            insertion.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    /// <summary>Lit un reglage du client, ou null s'il n'existe pas.</summary>
    public string? LireReglage(string cle)
    {
        using var connexion = Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = "SELECT valeur FROM reglages WHERE cle = $cle;";
        commande.Parameters.AddWithValue("$cle", cle);

        var valeur = commande.ExecuteScalar();
        return valeur is null or DBNull ? null : (string)valeur;
    }

    /// <summary>Ecrit un reglage, ou le supprime si la valeur est nulle ou vide.</summary>
    public void EcrireReglage(string cle, string? valeur)
    {
        using var connexion = Ouvrir();
        using var commande = connexion.CreateCommand();

        if (string.IsNullOrWhiteSpace(valeur))
        {
            commande.CommandText = "DELETE FROM reglages WHERE cle = $cle;";
            commande.Parameters.AddWithValue("$cle", cle);
            commande.ExecuteNonQuery();
            return;
        }

        commande.CommandText = """
            INSERT INTO reglages (cle, valeur) VALUES ($cle, $valeur)
            ON CONFLICT (cle) DO UPDATE SET valeur = excluded.valeur;
            """;
        commande.Parameters.AddWithValue("$cle", cle);
        commande.Parameters.AddWithValue("$valeur", valeur);
        commande.ExecuteNonQuery();
    }

    private SqliteConnection Ouvrir()
    {
        var connexion = new SqliteConnection(_chaineConnexion);
        connexion.Open();

        // Les cles etrangeres ne sont pas actives par defaut dans SQLite.
        using var pragma = connexion.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();

        return connexion;
    }

    private static object EnParametre(TimeOnly? heure) =>
        heure is TimeOnly valeur
            ? valeur.ToString(FormatHeure, CultureInfo.InvariantCulture)
            : DBNull.Value;

    private static TimeOnly? LireHeure(SqliteDataReader lecteur, int index) =>
        lecteur.IsDBNull(index)
            ? null
            : TimeOnly.ParseExact(lecteur.GetString(index), FormatHeure, CultureInfo.InvariantCulture);
}
