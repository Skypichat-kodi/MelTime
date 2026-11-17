namespace MelTime.Core.Agenda;

/// <summary>
/// Une journee de la feuille de temps : quatre horaires saisis, et le calcul qui en decoule.
///
/// Regles appliquees :
///  - les horaires doivent se succeder dans l'ordre du jour :
///    arrivee &lt;= debut de pause &lt;= fin de pause &lt;= depart ;
///  - duree = (depart - arrivee) - (fin de pause - debut de pause) ;
///  - une pause incomplete (debut ou fin manquant) n'est pas deduite ;
///  - une journee sans arrivee ou sans depart ne produit aucun total.
///
/// Le travail de nuit n'est pas accepte : un depart anterieur a l'arrivee
/// rend la journee incoherente, et elle ne produit alors aucun total.
/// </summary>
public sealed class JourneeTravail
{
    public DateOnly Date { get; set; }

    public TimeOnly? Arrivee { get; set; }

    public TimeOnly? PauseDebut { get; set; }

    public TimeOnly? PauseFin { get; set; }

    public TimeOnly? Depart { get; set; }

    /// <summary>Vrai lorsque l'arrivee et le depart sont tous deux saisis.</summary>
    public bool EstComplete => Arrivee is not null && Depart is not null;

    /// <summary>Vrai lorsque le debut et la fin de pause sont tous deux saisis.</summary>
    public bool PauseComplete => PauseDebut is not null && PauseFin is not null;

    /// <summary>Vrai si les horaires renseignes se succedent dans l'ordre attendu.</summary>
    public bool EstChronologique => PremiereIncoherence() is null;

    /// <summary>
    /// Duree de la pause. Zero si elle n'est pas entierement saisie
    /// ou si elle est incoherente.
    /// </summary>
    public TimeSpan DureePause
    {
        get
        {
            if (PauseDebut is not TimeOnly debut || PauseFin is not TimeOnly fin)
            {
                return TimeSpan.Zero;
            }

            return fin > debut ? fin - debut : TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Duree effectivement travaillee.
    /// Null si l'arrivee ou le depart manque, ou si les horaires sont incoherents.
    /// </summary>
    public TimeSpan? DureeTravaillee
    {
        get
        {
            if (Arrivee is not TimeOnly arrivee || Depart is not TimeOnly depart)
            {
                return null;
            }

            if (!EstChronologique)
            {
                return null;
            }

            var duree = depart - arrivee - DureePause;
            return duree < TimeSpan.Zero ? TimeSpan.Zero : duree;
        }
    }

    /// <summary>
    /// Liste les anomalies de la journee. Vide si tout est coherent.
    /// Les messages sont destines a l'utilisateur.
    /// </summary>
    public IReadOnlyList<string> Valider()
    {
        var anomalie = PremiereIncoherence();
        return anomalie is null ? Array.Empty<string>() : new[] { anomalie };
    }

    /// <summary>Vide tous les horaires de la journee.</summary>
    public void Effacer()
    {
        Arrivee = null;
        PauseDebut = null;
        PauseFin = null;
        Depart = null;
    }

    /// <summary>Les quatre horaires dans l'ordre chronologique attendu.</summary>
    private IEnumerable<(string Libelle, TimeOnly? Heure)> Horaires()
    {
        yield return ("arrivée", Arrivee);
        yield return ("début de pause", PauseDebut);
        yield return ("fin de pause", PauseFin);
        yield return ("départ", Depart);
    }

    private string? PremiereIncoherence()
    {
        (string Libelle, TimeOnly Heure)? precedente = null;

        foreach (var (libelle, heure) in Horaires())
        {
            if (heure is not TimeOnly valeur)
            {
                continue;
            }

            if (precedente is { } avant && valeur < avant.Heure)
            {
                return $"L'heure de {libelle} ({valeur:HH:mm}) ne peut pas précéder celle de {avant.Libelle} ({avant.Heure:HH:mm}).";
            }

            precedente = (libelle, valeur);
        }

        return null;
    }
}
