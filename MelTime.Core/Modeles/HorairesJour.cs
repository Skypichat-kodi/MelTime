namespace MelTime.Core.Modeles;

/// <summary>
/// Horaires d'une journee type : ceux qu'un client saisit une fois
/// dans son modele, et qui prereplissent ensuite ses semaines vides.
/// </summary>
public sealed class HorairesJour
{
    public TimeOnly? Arrivee { get; set; }

    public TimeOnly? PauseDebut { get; set; }

    public TimeOnly? PauseFin { get; set; }

    public TimeOnly? Depart { get; set; }

    /// <summary>Vrai si aucun horaire n'est renseigne pour cette journee.</summary>
    public bool EstVide =>
        Arrivee is null && PauseDebut is null && PauseFin is null && Depart is null;

    public HorairesJour Copier() => new()
    {
        Arrivee = Arrivee,
        PauseDebut = PauseDebut,
        PauseFin = PauseFin,
        Depart = Depart
    };
}
