namespace MelTime.Core.Api;

/// <summary>
/// Total des heures d'un mois civil, calcule par le serveur.
/// Une semaine ISO pouvant chevaucher deux mois, ce total ne peut pas
/// etre obtenu en additionnant simplement les semaines affichees.
/// </summary>
public sealed class TotalMoisApi
{
    public int Annee { get; set; }

    public int Mois { get; set; }

    /// <summary>Total mis en forme, par exemple « 151:30 ».</summary>
    public string TotalTravaille { get; set; } = "00:00";
}
