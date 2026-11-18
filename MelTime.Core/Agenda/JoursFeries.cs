using MelTime.Core.Modeles;

namespace MelTime.Core.Agenda;

/// <summary>
/// Jours feries francais.
///
/// Huit dates fixes, plus quatre dates mobiles toutes derivees de Paques.
/// Le calcul est entierement local : aucune connexion, aucune dependance,
/// et il reste valable pour n'importe quelle annee.
///
/// Le vendredi saint et le 26 decembre, propres a l'Alsace-Moselle,
/// ne sont volontairement pas inclus : ils ne concernent pas l'ensemble
/// du territoire et fausseraient l'affichage pour la majorite des clients.
/// </summary>
public static class JoursFeries
{
    /// <summary>
    /// Date de Paques pour une annee donnee, selon le comput gregorien
    /// (algorithme de Meeus, Jones et Butcher).
    /// </summary>
    public static DateOnly Paques(int annee)
    {
        var a = annee % 19;
        var b = annee / 100;
        var c = annee % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;

        var mois = (h + l - 7 * m + 114) / 31;
        var jour = ((h + l - 7 * m + 114) % 31) + 1;

        return new DateOnly(annee, mois, jour);
    }

    /// <summary>Tous les jours feries legaux d'une annee, dans l'ordre chronologique.</summary>
    public static IReadOnlyList<DateOnly> DeLAnnee(int annee)
    {
        var paques = Paques(annee);

        var feries = new List<DateOnly>
        {
            new(annee, 1, 1),           // Jour de l'an
            paques.AddDays(1),          // Lundi de Paques
            new(annee, 5, 1),           // Fete du Travail
            new(annee, 5, 8),           // Victoire 1945
            paques.AddDays(39),         // Ascension
            paques.AddDays(50),         // Lundi de Pentecote
            new(annee, 7, 14),          // Fete nationale
            new(annee, 8, 15),          // Assomption
            new(annee, 11, 1),          // Toussaint
            new(annee, 11, 11),         // Armistice 1918
            new(annee, 12, 25)          // Noel
        };

        feries.Sort();
        return feries;
    }

    /// <summary>Vrai si la date correspond a un jour ferie legal.</summary>
    public static bool EstFerie(DateOnly date) => DeLAnnee(date.Year).Contains(date);

    // ------------------------------------------------------------------
    // Regle par pays
    // ------------------------------------------------------------------

    /// <summary>
    /// Jours feries applicables a un pays, pour une annee donnee.
    ///
    /// C'est ici que se brancheront les futurs pays : soit par un calcul
    /// local comme la France, soit par un appel a un service externe.
    /// Le reste de l'application n'a pas a le savoir.
    /// </summary>
    public static IReadOnlyList<DateOnly> PourPays(Pays pays, int annee) => pays switch
    {
        Pays.France => DeLAnnee(annee),

        // Les bank holidays anglaises ne sont pas des dates fixes : lorsqu'un
        // ferie tombe un week-end, il est reporte au lundi suivant. La regle
        // reste a ecrire. En attendant, aucun jour n'est signale.
        Pays.RoyaumeUni => Array.Empty<DateOnly>(),

        _ => Array.Empty<DateOnly>()
    };

    /// <summary>Vrai si la date est ferie dans le pays indique.</summary>
    public static bool EstFerie(Pays pays, DateOnly date) =>
        PourPays(pays, date.Year).Contains(date);
}
