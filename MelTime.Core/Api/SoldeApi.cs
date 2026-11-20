namespace MelTime.Core.Api;

/// <summary>
/// Solde des heures sur une periode glissante de 52 semaines.
///
/// Deux references coexistent, et elles ne mesurent pas la meme chose :
///
///   - le seuil legal de 35 h par semaine : tout ce qui le depasse constitue
///     une heure supplementaire. Un contrat de 36 h 45 produit donc 1 h 45
///     d'heures supplementaires chaque semaine, mecaniquement ;
///   - le contrat du client : l'ecart a cette valeur indique seulement s'il
///     a travaille plus ou moins que prevu.
///
/// Les deux sont fournies separement : additionnees, elles feraient
/// disparaitre les heures supplementaires structurelles du contrat.
/// </summary>
public sealed class SoldeApi
{
    /// <summary>Nombre de semaines couvertes par le calcul.</summary>
    public int Semaines { get; set; }

    /// <summary>
    /// Cumul des heures faites au-dela de 35 h par semaine, par exemple
    /// « 91:00 » pour un contrat de 36 h 45 sur 52 semaines.
    /// </summary>
    public string Supplementaires { get; set; } = "00:00";

    /// <summary>
    /// Ecart au contrat : positif si le client a travaille plus que prevu,
    /// negatif s'il manque des heures, par exemple « -05:00 ».
    /// </summary>
    public string Ecart { get; set; } = "00:00";

    /// <summary>Vrai si l'ecart au contrat est positif ou nul.</summary>
    public bool EcartPositif { get; set; }
}
