namespace MelTime.Core.Agenda;

/// <summary>
/// Une journée de la feuille de temps : quatre horaires saisis, et le calcul qui en decoule.
/// Regles appliquees :
///  - duree = (depart - arrivee) - (fin de pause - debut de pause)
///  - si le depart precede l'arrivee, il est considere comme le lendemain (travail de nuit)
///  - une pause incomplete (debut ou fin manquant) n'est pas deduite
///  - une journee sans arrivee ou sans depart ne produit aucun total
/// </summary>
public sealed class JourneeTravail
{
    private static readonly TimeSpan UneJournee = TimeSpan.FromHours(24);

    public DateOnly Date { get; set; }

    public TimeOnly? Arrivee { get; set; }

    public TimeOnly? PauseDebut { get; set; }

    public TimeOnly? PauseFin { get; set; }

    public TimeOnly? Depart { get; set; }

    /// <summary>Vrai lorsque l'arrivee et le depart sont tous deux saisis.</summary>
    public bool EstComplete => Arrivee is not null && Depart is not null;

    /// <summary>Vrai lorsque le debut et la fin de pause sont tous deux saisis.</summary>
    public bool PauseComplete => PauseDebut is not null && PauseFin is not null;

    /// <summary>Duree de la pause. Zero si elle n'est pas entierement saisie.</summary>
    public TimeSpan DureePause
    {
        get
        {
            if (PauseDebut is not TimeOnly debut || PauseFin is not TimeOnly fin)
            {
                return TimeSpan.Zero;
            }

            if (fin == debut)
            {
                return TimeSpan.Zero;
            }

            return fin > debut ? fin - debut : UneJournee - (debut - fin);
        }
    }

    /// <summary>
    /// Duree effectivement travaillee.
    /// Null si l'arrivee ou le depart manque : la journee n'est pas encore saisie.
    /// </summary>
    public TimeSpan? DureeTravaillee
    {
        get
        {
            if (Arrivee is not TimeOnly arrivee || Depart is not TimeOnly depart)
            {
                return null;
            }

            if (depart == arrivee)
            {
                return TimeSpan.Zero;
            }

            // Depart anterieur a l'arrivee : la journee franchit minuit.
            var amplitude = depart > arrivee
                ? depart - arrivee
                : UneJournee - (arrivee - depart);

            var duree = amplitude - DureePause;
            return duree < TimeSpan.Zero ? TimeSpan.Zero : duree;
        }
    }

    /// <summary>Vide tous les horaires de la journee.</summary>
    public void Effacer()
    {
        Arrivee = null;
        PauseDebut = null;
        PauseFin = null;
        Depart = null;
    }
}
