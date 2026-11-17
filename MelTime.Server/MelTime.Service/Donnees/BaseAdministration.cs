using System.Globalization;
using System.Security.Cryptography;
using MelTime.Core.Modeles;
using MelTime.Core.Securite;
using Microsoft.Data.Sqlite;

namespace MelTime.Service.Donnees;

/// <summary>
/// Base d'administration du serveur : elle contient les comptes clients
/// (identifiant, mot de passe hache, identite, etat).
/// Les donnees d'agenda viendront dans des fichiers distincts.
/// </summary>
public sealed class BaseAdministration
{
    private readonly string _chaineConnexion;

    public BaseAdministration(string cheminFichier)
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

    /// <summary>Cree la base et la table des clients si necessaire. Appele au demarrage du service.</summary>
    public void Initialiser()
    {
        using var connexion = Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = """
            CREATE TABLE IF NOT EXISTS clients (
                id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                identifiant         TEXT    NOT NULL COLLATE NOCASE UNIQUE,
                nom                 TEXT    NOT NULL,
                prenom              TEXT    NOT NULL,
                actif               INTEGER NOT NULL DEFAULT 1,
                contrat_minutes     INTEGER NOT NULL DEFAULT 2100,
                mot_de_passe_hache  TEXT    NOT NULL,
                cree_le             TEXT    NOT NULL,
                derniere_connexion  TEXT    NULL
            );

            CREATE TABLE IF NOT EXISTS sessions (
                jeton     TEXT    PRIMARY KEY,
                id_client INTEGER NOT NULL,
                cree_le   TEXT    NOT NULL,
                expire_le TEXT    NOT NULL
            );
            """;
        commande.ExecuteNonQuery();

        MigrerSchema(connexion);
    }

    /// <summary>
    /// Applique les evolutions de structure sur une base existante.
    /// Sans cela, une base creee par une version anterieure resterait incomplète.
    /// </summary>
    private static void MigrerSchema(SqliteConnection connexion)
    {
        var colonnes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (var inventaire = connexion.CreateCommand())
        {
            inventaire.CommandText = "PRAGMA table_info(clients);";
            using var lecteur = inventaire.ExecuteReader();
            while (lecteur.Read())
            {
                colonnes.Add(lecteur.GetString(1));
            }
        }

        if (!colonnes.Contains("contrat_minutes"))
        {
            using var migration = connexion.CreateCommand();
            migration.CommandText = "ALTER TABLE clients ADD COLUMN contrat_minutes INTEGER NOT NULL DEFAULT 2100;";
            migration.ExecuteNonQuery();
        }
    }

    public IReadOnlyList<Client> Lister()
    {
        using var connexion = Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = """
            SELECT id, identifiant, nom, prenom, actif, contrat_minutes, cree_le, derniere_connexion
            FROM clients
            ORDER BY nom COLLATE NOCASE, prenom COLLATE NOCASE, identifiant COLLATE NOCASE;
            """;

        using var lecteur = commande.ExecuteReader();
        var liste = new List<Client>();
        while (lecteur.Read())
        {
            liste.Add(Lire(lecteur));
        }

        return liste;
    }

    public Client? Obtenir(int id)
    {
        using var connexion = Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = """
            SELECT id, identifiant, nom, prenom, actif, contrat_minutes, cree_le, derniere_connexion
            FROM clients
            WHERE id = $id;
            """;
        commande.Parameters.AddWithValue("$id", id);

        using var lecteur = commande.ExecuteReader();
        return lecteur.Read() ? Lire(lecteur) : null;
    }

    public bool IdentifiantExiste(string identifiant, int idAExclure = 0)
    {
        using var connexion = Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = """
            SELECT COUNT(1) FROM clients
            WHERE identifiant = $identifiant COLLATE NOCASE AND id <> $id;
            """;
        commande.Parameters.AddWithValue("$identifiant", identifiant.Trim());
        commande.Parameters.AddWithValue("$id", idAExclure);

        return Convert.ToInt64(commande.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
    }

    /// <summary>Cree un client et retourne son identifiant technique.</summary>
    /// <exception cref="InvalidOperationException">Donnees invalides ou identifiant deja utilise.</exception>
    public int Creer(Client client, string motDePasse)
    {
        ArgumentNullException.ThrowIfNull(client);

        var erreurs = client.Valider().ToList();

        if (string.IsNullOrEmpty(motDePasse))
        {
            erreurs.Add("Le mot de passe est obligatoire.");
        }
        else if (motDePasse.Length < MotDePasse.LongueurMinimale)
        {
            erreurs.Add($"Le mot de passe doit compter au moins {MotDePasse.LongueurMinimale} caracteres.");
        }

        if (erreurs.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, erreurs));
        }

        if (IdentifiantExiste(client.Identifiant))
        {
            throw new InvalidOperationException($"L'identifiant « {client.Identifiant.Trim()} » est deja utilise.");
        }

        using var connexion = Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = """
            INSERT INTO clients (identifiant, nom, prenom, actif, contrat_minutes, mot_de_passe_hache, cree_le, derniere_connexion)
            VALUES ($identifiant, $nom, $prenom, $actif, $contrat, $hache, $creeLe, NULL);
            SELECT last_insert_rowid();
            """;
        commande.Parameters.AddWithValue("$identifiant", client.Identifiant.Trim());
        commande.Parameters.AddWithValue("$nom", client.Nom.Trim());
        commande.Parameters.AddWithValue("$prenom", client.Prenom.Trim());
        commande.Parameters.AddWithValue("$actif", client.Actif ? 1 : 0);
        commande.Parameters.AddWithValue("$contrat", client.ContratHebdomadaireMinutes);
        commande.Parameters.AddWithValue("$hache", MotDePasse.Hacher(motDePasse));
        commande.Parameters.AddWithValue("$creeLe", EnTexte(DateTimeOffset.UtcNow));

        return Convert.ToInt32(commande.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>Met a jour l'identite et l'etat. Ne touche pas au mot de passe.</summary>
    public void Modifier(Client client)
    {
        ArgumentNullException.ThrowIfNull(client);

        var erreurs = client.Valider();
        if (erreurs.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, erreurs));
        }

        if (IdentifiantExiste(client.Identifiant, client.Id))
        {
            throw new InvalidOperationException($"L'identifiant « {client.Identifiant.Trim()} » est deja utilise.");
        }

        using var connexion = Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = """
            UPDATE clients
            SET identifiant = $identifiant,
                nom = $nom,
                prenom = $prenom,
                actif = $actif,
                contrat_minutes = $contrat
            WHERE id = $id;
            """;
        commande.Parameters.AddWithValue("$identifiant", client.Identifiant.Trim());
        commande.Parameters.AddWithValue("$nom", client.Nom.Trim());
        commande.Parameters.AddWithValue("$prenom", client.Prenom.Trim());
        commande.Parameters.AddWithValue("$actif", client.Actif ? 1 : 0);
        commande.Parameters.AddWithValue("$contrat", client.ContratHebdomadaireMinutes);
        commande.Parameters.AddWithValue("$id", client.Id);

        if (commande.ExecuteNonQuery() == 0)
        {
            throw new InvalidOperationException("Ce client n'existe plus.");
        }
    }

    public void ChangerMotDePasse(int id, string motDePasse)
    {
        if (string.IsNullOrEmpty(motDePasse) || motDePasse.Length < MotDePasse.LongueurMinimale)
        {
            throw new InvalidOperationException(
                $"Le mot de passe doit compter au moins {MotDePasse.LongueurMinimale} caracteres.");
        }

        using var connexion = Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = "UPDATE clients SET mot_de_passe_hache = $hache WHERE id = $id;";
        commande.Parameters.AddWithValue("$hache", MotDePasse.Hacher(motDePasse));
        commande.Parameters.AddWithValue("$id", id);

        if (commande.ExecuteNonQuery() == 0)
        {
            throw new InvalidOperationException("Ce client n'existe plus.");
        }
    }

    public void Supprimer(int id)
    {
        using var connexion = Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = "DELETE FROM clients WHERE id = $id;";
        commande.Parameters.AddWithValue("$id", id);
        commande.ExecuteNonQuery();
    }

    /// <summary>
    /// Verifie un couple identifiant / mot de passe.
    /// Retourne null si l'identifiant est inconnu, si le compte est inactif ou si le mot de passe est faux.
    /// </summary>
    public Client? VerifierIdentifiants(string identifiant, string motDePasse)
    {
        if (string.IsNullOrWhiteSpace(identifiant) || string.IsNullOrEmpty(motDePasse))
        {
            return null;
        }

        using var connexion = Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = """
            SELECT id, identifiant, nom, prenom, actif, contrat_minutes, cree_le, derniere_connexion, mot_de_passe_hache
            FROM clients
            WHERE identifiant = $identifiant COLLATE NOCASE;
            """;
        commande.Parameters.AddWithValue("$identifiant", identifiant.Trim());

        using var lecteur = commande.ExecuteReader();
        if (!lecteur.Read())
        {
            return null;
        }

        var client = Lire(lecteur);
        var hache = lecteur.GetString(8);

        if (!client.Actif || !MotDePasse.Verifier(motDePasse, hache))
        {
            return null;
        }

        return client;
    }

    /// <summary>Note l'horodatage de la derniere connexion reussie.</summary>
    public void EnregistrerConnexion(int id)
    {
        using var connexion = Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = "UPDATE clients SET derniere_connexion = $date WHERE id = $id;";
        commande.Parameters.AddWithValue("$date", EnTexte(DateTimeOffset.UtcNow));
        commande.Parameters.AddWithValue("$id", id);
        commande.ExecuteNonQuery();
    }

    // ------------------------------------------------------------------
    // Sessions : jetons remis aux clients apres authentification.
    // Ils sont conserves en base pour survivre a un redemarrage du service,
    // sans quoi chaque redemarrage obligerait tout le monde a se reconnecter.
    // ------------------------------------------------------------------

    /// <summary>Cree un jeton de session et retourne sa valeur.</summary>
    public string CreerSession(int idClient, TimeSpan duree)
    {
        var jeton = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var maintenant = DateTimeOffset.UtcNow;

        using var connexion = Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = """
            INSERT INTO sessions (jeton, id_client, cree_le, expire_le)
            VALUES ($jeton, $id, $cree, $expire);
            """;
        commande.Parameters.AddWithValue("$jeton", jeton);
        commande.Parameters.AddWithValue("$id", idClient);
        commande.Parameters.AddWithValue("$cree", EnTexte(maintenant));
        commande.Parameters.AddWithValue("$expire", EnTexte(maintenant.Add(duree)));
        commande.ExecuteNonQuery();

        return jeton;
    }

    /// <summary>
    /// Retourne le client associe a un jeton valide, ou null si le jeton
    /// est inconnu, expire, ou si le compte a ete desactive depuis.
    /// </summary>
    public Client? ClientDeSession(string jeton)
    {
        if (string.IsNullOrWhiteSpace(jeton))
        {
            return null;
        }

        using var connexion = Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = """
            SELECT c.id, c.identifiant, c.nom, c.prenom, c.actif, c.contrat_minutes, c.cree_le, c.derniere_connexion
            FROM sessions s
            JOIN clients c ON c.id = s.id_client
            WHERE s.jeton = $jeton
              AND s.expire_le > $maintenant
              AND c.actif = 1;
            """;
        commande.Parameters.AddWithValue("$jeton", jeton);
        commande.Parameters.AddWithValue("$maintenant", EnTexte(DateTimeOffset.UtcNow));

        using var lecteur = commande.ExecuteReader();
        return lecteur.Read() ? Lire(lecteur) : null;
    }

    public void SupprimerSession(string jeton)
    {
        if (string.IsNullOrWhiteSpace(jeton))
        {
            return;
        }

        using var connexion = Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = "DELETE FROM sessions WHERE jeton = $jeton;";
        commande.Parameters.AddWithValue("$jeton", jeton);
        commande.ExecuteNonQuery();
    }

    /// <summary>Supprime toutes les sessions des comptes d'un client (deconnexion forcee).</summary>
    public void SupprimerSessionsDuClient(int idClient)
    {
        using var connexion = Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = "DELETE FROM sessions WHERE id_client = $id;";
        commande.Parameters.AddWithValue("$id", idClient);
        commande.ExecuteNonQuery();
    }

    /// <summary>Retire les jetons arrives a echeance. Retourne le nombre supprime.</summary>
    public int PurgerSessionsExpirees()
    {
        using var connexion = Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = "DELETE FROM sessions WHERE expire_le <= $maintenant;";
        commande.Parameters.AddWithValue("$maintenant", EnTexte(DateTimeOffset.UtcNow));
        return commande.ExecuteNonQuery();
    }

    private SqliteConnection Ouvrir()
    {
        var connexion = new SqliteConnection(_chaineConnexion);
        connexion.Open();
        return connexion;
    }

    private static Client Lire(SqliteDataReader lecteur) => new()
    {
        Id = lecteur.GetInt32(0),
        Identifiant = lecteur.GetString(1),
        Nom = lecteur.GetString(2),
        Prenom = lecteur.GetString(3),
        Actif = lecteur.GetBoolean(4),
        ContratHebdomadaireMinutes = lecteur.GetInt32(5),
        CreeLe = EnDate(lecteur.GetString(6)),
        DerniereConnexion = lecteur.IsDBNull(7) ? null : EnDate(lecteur.GetString(7))
    };

    private static string EnTexte(DateTimeOffset valeur) =>
        valeur.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset EnDate(string valeur) =>
        DateTimeOffset.Parse(valeur, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
