namespace MelTime.Core.Api;

/// <summary>
/// Solde des heures sur une periode glissante de 52 semaines.
///
/// Un solde positif signifie que le client a travaille au-dela de son
/// contrat : ce sont des heures a recuperer. Un solde negatif signale au
/// contraire des heures manquantes.
/// </summary>
public sealed class SoldeApi
{
    /// <summary>Nombre de semaines couvertes par le calcul.</summary>
    public int Semaines { get; set; }

    /// <summary>Solde net, par exemple « 12:30 » ou « -05:00 ».</summary>
    public string Solde { get; set; } = "00:00";

    /// <summary>Vrai si le solde est positif ou nul.</summary>
    public bool Positif { get; set; }
}
