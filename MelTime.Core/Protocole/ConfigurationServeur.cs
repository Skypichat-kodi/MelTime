namespace MelTime.Core.Protocole;

/// <summary>
/// Configuration reseau du service, telle qu'elle circule entre le service
/// et ses interfaces. L'UI peut la lire et la modifier sans jamais toucher
/// au fichier elle-meme : c'est le service qui ecrit.
/// </summary>
public sealed class ConfigurationServeur
{
    /// <summary>Port d'ecoute HTTP.</summary>
    public int Port { get; set; } = 5080;

    /// <summary>
    /// Hote d'ecoute : « + » pour toutes les cartes reseau,
    /// « localhost » pour restreindre a la machine elle-meme.
    /// </summary>
    public string Hote { get; set; } = "+";

    /// <summary>Duree de validite d'un jeton de session, en jours.</summary>
    public int DureeSessionJours { get; set; } = 30;
}
