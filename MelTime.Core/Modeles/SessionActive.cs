namespace MelTime.Core.Modeles;

/// <summary>
/// Une session ouverte : un client connecte, sur un appareil donne.
/// Un meme client peut avoir plusieurs sessions simultanees (Windows et Android),
/// c'est pourquoi l'heure d'ouverture fait partie de l'identite de la session.
/// </summary>
public sealed class SessionActive
{
    public int IdClient { get; set; }

    public string Identifiant { get; set; } = string.Empty;

    public string NomComplet { get; set; } = string.Empty;

    /// <summary>Moment d'ouverture de la session (UTC).</summary>
    public DateTimeOffset Depuis { get; set; }

    /// <summary>Vrai si la session ouvre sur un appareil different de celui-ci.</summary>
    public override string ToString() => $"{Identifiant} ({NomComplet}) depuis {Depuis:HH:mm}";
}
