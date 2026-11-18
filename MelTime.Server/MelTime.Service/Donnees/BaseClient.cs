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
            """;
        commande.ExecuteNonQuery();
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
                SELECT jour, arrivee, pause_debut, pause_fin, depart FROM journees
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
                INSERT INTO journees (annee_iso, numero, jour, arrivee, pause_debut, pause_fin, depart)
                VALUES ($annee, $numero, $jour, $arrivee, $pauseDebut, $pauseFin, $depart)
                ON CONFLICT (annee_iso, numero, jour)
                DO UPDATE SET
                    arrivee = excluded.arrivee,
                    pause_debut = excluded.pause_debut,
                    pause_fin = excluded.pause_fin,
                    depart = excluded.depart;
                """;
            commande.Parameters.AddWithValue("$annee", semaine.AnneeIso);
            commande.Parameters.AddWithValue("$numero", semaine.Numero);
            commande.Parameters.AddWithValue("$jour", index);
            commande.Parameters.AddWithValue("$arrivee", EnParametre(jour.Arrivee));
            commande.Parameters.AddWithValue("$pauseDebut", EnParametre(jour.PauseDebut));
            commande.Parameters.AddWithValue("$pauseFin", EnParametre(jour.PauseFin));
            commande.Parameters.AddWithValue("$depart", EnParametre(jour.Depart));
            commande.ExecuteNonQuery();
        }

        transaction.Commit();
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
