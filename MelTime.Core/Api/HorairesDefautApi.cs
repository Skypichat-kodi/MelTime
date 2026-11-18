namespace MelTime.Core.Api;

/// <summary>
/// Modele d'horaires d'un client : sept journees types,
/// du lundi (index 0) au dimanche (index 6).
/// </summary>
public sealed class HorairesDefautApi
{
    public List<JourDefautApi> Jours { get; set; } = new();
}

/// <summary>Une journee type dans les echanges JSON.</summary>
public sealed class JourDefautApi
{
    /// <summary>0 = lundi, 6 = dimanche.</summary>
    public int Index { get; set; }

    public string? Arrivee { get; set; }

    public string? PauseDebut { get; set; }

    public string? PauseFin { get; set; }

    public string? Depart { get; set; }
}
