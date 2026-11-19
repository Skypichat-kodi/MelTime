using System.IO;
using System.Text.Json;

namespace MelTime.ClientWindows;

/// <summary>
/// Informations conservees d'une session a l'autre, dans %AppData%\MelTime\session.json.
/// C'est ce qui permet de rester connecte entre deux lancements.
/// </summary>
public sealed class SessionLocale
{
    public const string AdresseParDefaut = "http://localhost:5080";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    /// <summary>Adresse du serveur, par exemple http://192.168.1.10:5080.</summary>
    public string AdresseServeur { get; set; } = AdresseParDefaut;

    /// <summary>
    /// Vrai tant qu'aucune adresse reelle n'a ete enregistree. La fenetre de
    /// connexion laisse alors le champ vide plutot que d'afficher « localhost »,
    /// qui n'a de sens que pour une installation locale.
    /// </summary>
    public bool AdresseParDefautNonRenseignee =>
        string.Equals(AdresseServeur, AdresseParDefaut, StringComparison.OrdinalIgnoreCase);

    /// <summary>Dernier identifiant utilise, pour ne pas le ressaisir.</summary>
    public string? Identifiant { get; set; }

    /// <summary>Jeton de session, ou null si le client s'est deconnecte.</summary>
    public string? Jeton { get; set; }

    public static string Dossier =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MelTime");

    public static string CheminFichier => Path.Combine(Dossier, "session.json");

    public static SessionLocale Charger()
    {
        try
        {
            if (!File.Exists(CheminFichier))
            {
                return new SessionLocale();
            }

            var texte = File.ReadAllText(CheminFichier);
            var session = JsonSerializer.Deserialize<SessionLocale>(texte, Options);

            if (session is null || string.IsNullOrWhiteSpace(session.AdresseServeur))
            {
                return new SessionLocale();
            }

            return session;
        }
        catch (Exception)
        {
            return new SessionLocale();
        }
    }

    public void Enregistrer()
    {
        try
        {
            Directory.CreateDirectory(Dossier);
            File.WriteAllText(CheminFichier, JsonSerializer.Serialize(this, Options));
        }
        catch (Exception)
        {
            // Une preference non enregistree ne doit pas interrompre l'utilisateur.
        }
    }

    public void EffacerJeton()
    {
        Jeton = null;
        Enregistrer();
    }
}
