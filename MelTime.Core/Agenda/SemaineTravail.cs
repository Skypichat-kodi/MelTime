using System.Globalization;

namespace MelTime.Core.Agenda;

/// <summary>
/// Une feuille de temps hebdomadaire : sept journees, du lundi au dimanche,
/// et les totaux qui en decoulent.
/// La numerotation suit la norme ISO 8601 (semaine du lundi, 1 a 53).
/// </summary>
public sealed class SemaineTravail
{
    private readonly JourneeTravail[] _jours;

    private SemaineTravail(int anneeIso, int numero, DateOnly lundi, JourneeTravail[] jours)
    {
        AnneeIso = anneeIso;
        Numero = numero;
        Lundi = lundi;
        _jours = jours;
    }

    /// <summary>Annee ISO de la semaine (peut differer de l'annee civile en debut et fin d'annee).</summary>
    public int AnneeIso { get; }

    /// <summary>Numero de semaine ISO, de 1 a 53.</summary>
    public int Numero { get; }

    /// <summary>Date du lundi de la semaine.</summary>
    public DateOnly Lundi { get; }

    /// <summary>
    /// Contrat effectif de cette semaine, en minutes.
    /// Initialise a partir de la fiche client, puis conserve tel quel :
    /// modifier le contrat d'un client ne reecrit pas l'historique.
    /// </summary>
    public int ContratHebdomadaireMinutes { get; set; } = 2100;

    /// <summary>Les sept journees, du lundi (index 0) au dimanche (index 6).</summary>
    public IReadOnlyList<JourneeTravail> Jours => _jours;

    /// <summary>Cree une semaine vide a partir d'une annee et d'un numero ISO.</summary>
    public static SemaineTravail Creer(int anneeIso, int numero)
    {
        var total = SemainesDansLAnnee(anneeIso);

        if (numero < 1 || numero > total)
        {
            throw new ArgumentOutOfRangeException(
                nameof(numero),
                $"L'annee {anneeIso} ne compte pas de semaine {numero} (1 a {total}).");
        }

        var lundi = LundiDeLaSemaine(anneeIso, numero);
        var jours = new JourneeTravail[7];

        for (var index = 0; index < jours.Length; index++)
        {
            jours[index] = new JourneeTravail { Date = lundi.AddDays(index) };
        }

        return new SemaineTravail(anneeIso, numero, lundi, jours);
    }

    /// <summary>Cree une semaine vide contenant la date indiquee.</summary>
    public static SemaineTravail CreerAutourDe(DateOnly date)
    {
        var (anneeIso, numero) = SemaineIsoDe(date);
        return Creer(anneeIso, numero);
    }

    /// <summary>Total des heures travaillees de la semaine.</summary>
    public TimeSpan TotalTravaille
    {
        get
        {
            var total = TimeSpan.Zero;

            foreach (var jour in _jours)
            {
                total += jour.DureeTravaillee ?? TimeSpan.Zero;
            }

            return total;
        }
    }

    /// <summary>
    /// Heures effectuees au-dela du contrat hebdomadaire.
    /// Zero si le contrat n'est pas atteint.
    /// </summary>
    public TimeSpan HeuresSupplementaires(TimeSpan contratHebdomadaire)
    {
        var excedent = TotalTravaille - contratHebdomadaire;
        return excedent > TimeSpan.Zero ? excedent : TimeSpan.Zero;
    }

    /// <summary>Heures supplementaires calculees avec le contrat propre a cette semaine.</summary>
    public TimeSpan HeuresSupplementaires() =>
        HeuresSupplementaires(TimeSpan.FromMinutes(ContratHebdomadaireMinutes));

    // ------------------------------------------------------------------
    // Conversions ISO 8601
    // ------------------------------------------------------------------

    /// <summary>Date du lundi d'une semaine ISO donnee.</summary>
    public static DateOnly LundiDeLaSemaine(int anneeIso, int numero) =>
        DateOnly.FromDateTime(ISOWeek.ToDateTime(anneeIso, numero, DayOfWeek.Monday));

    /// <summary>Annee ISO et numero de semaine d'une date donnee.</summary>
    public static (int AnneeIso, int Numero) SemaineIsoDe(DateOnly date)
    {
        var valeur = date.ToDateTime(TimeOnly.MinValue);
        return (ISOWeek.GetYear(valeur), ISOWeek.GetWeekOfYear(valeur));
    }

    /// <summary>Nombre de semaines ISO d'une annee : 52, ou 53 les annees longues.</summary>
    public static int SemainesDansLAnnee(int anneeIso) => ISOWeek.GetWeeksInYear(anneeIso);
}
