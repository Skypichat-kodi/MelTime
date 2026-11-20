namespace MelTime.Core.Api;

/// <summary>
/// Bilan des heures sur une periode glissante de 52 semaines.
///
/// Deux references coexistent, et elles ne mesurent pas la meme chose :
///
///   - le seuil legal de 35 h par semaine : tout ce qui le depasse constitue
///     une heure supplementaire. Un contrat de 36 h 45 produit donc 1 h 45
///     d'heures supplementaires chaque semaine, mecaniquement ;
///   - le contrat du client : les heures manquantes mesurent ce qui n'a pas
///     ete fait par rapport a lui.
///
/// Les deux se cumulent separement : une semaine de deficit ne retire rien
/// aux heures supplementaires des autres semaines.
/// </summary>
public sealed class SoldeApi
{
    /// <summary>Nombre de semaines couvertes par le calcul.</summary>
    public int Semaines { get; set; }

    /// <summary>Cumul des heures faites au-dela de 35 h, par exemple « 91:00 ».</summary>
    public string Supplementaires { get; set; } = "00:00";

    /// <summary>Cumul des heures manquantes par rapport au contrat, par exemple « 05:00 ».</summary>
    public string Manquantes { get; set; } = "00:00";

    /// <summary>Jours de conges payes pris sur la periode.</summary>
    public int CongesPris { get; set; }

    /// <summary>Jours de conges payes acquis, soit cinq semaines ouvrees.</summary>
    public int CongesAcquis { get; set; } = 25;

    /// <summary>Jours d'arret maladie sur la periode.</summary>
    public int JoursMaladie { get; set; }
}
