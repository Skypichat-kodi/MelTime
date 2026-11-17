using System.Text.Json;

namespace MelTime.Service;

/// <summary>
/// Configuration du service, lue depuis C:\ProgramData\MelTime\meltime.config.json.
/// Le fichier est cree avec les valeurs par defaut au premier demarrage,
/// puis reste modifiable a la main entre deux lancements.
/// </summary>
public sealed class Configuration
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    /// <summary>Port d'ecoute HTTP.</summary>
    public int Port { get; set; } = 5080;

    /// <summary>
    /// Hote d'ecoute : « + » pour toutes les cartes reseau (necessite des droits eleves),
    /// « localhost » pour restreindre a la machine elle-meme.
    /// </summary>
    public string Hote { get; set; } = "+";

    /// <summary>Duree de validite d'un jeton de session, en jours.</summary>
    public int DureeSessionJours { get; set; } = 30;

    public static string CheminFichier => Path.Combine(CheminDonnees.DossierRacine, "meltime.config.json");

    /// <summary>Prefixe d'ecoute derive de l'hote et du port.</summary>
    public string Prefixe => $"http://{Hote}:{Port}/";

    /// <summary>
    /// Charge la configuration. Si le fichier n'existe pas, il est cree
    /// avec les valeurs par defaut ; s'il est illisible, les valeurs par defaut s'appliquent
    /// et l'incident est signale.
    /// </summary>
    public static Configuration Charger(out string? avertissement)
    {
        avertissement = null;

        try
        {
            CheminDonnees.Preparer();

            if (!File.Exists(CheminFichier))
            {
                var parDefaut = new Configuration();
                parDefaut.Enregistrer();
                return parDefaut;
            }

            var texte = File.ReadAllText(CheminFichier);
            var configuration = JsonSerializer.Deserialize<Configuration>(texte, Options);

            if (configuration is null)
            {
                avertissement = "Fichier de configuration vide : valeurs par defaut appliquees.";
                return new Configuration();
            }

            if (configuration.Port is < 1 or > 65535)
            {
                avertissement = $"Port {configuration.Port} invalide : retour au port 5080.";
                configuration.Port = 5080;
            }

            if (string.IsNullOrWhiteSpace(configuration.Hote))
            {
                configuration.Hote = "+";
            }

            if (configuration.DureeSessionJours is < 1 or > 365)
            {
                configuration.DureeSessionJours = 30;
            }

            return configuration;
        }
        catch (Exception ex)
        {
            avertissement = $"Configuration illisible ({ex.Message}) : valeurs par defaut appliquees.";
            return new Configuration();
        }
    }

    public void Enregistrer()
    {
        try
        {
            CheminDonnees.Preparer();
            File.WriteAllText(CheminFichier, JsonSerializer.Serialize(this, Options));
        }
        catch (Exception)
        {
            // Une configuration non enregistree ne doit pas empecher le service de demarrer.
        }
    }
}
