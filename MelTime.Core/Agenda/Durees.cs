namespace MelTime.Core.Agenda;

/// <summary>
/// Mise en forme des durees pour l'affichage.
/// Format « hh:mm », les heures pouvant depasser 24 pour un total hebdomadaire.
/// </summary>
public static class Durees
{
    public static string Formater(TimeSpan duree)
    {
        var negatif = duree < TimeSpan.Zero;

        if (negatif)
        {
            duree = duree.Negate();
        }

        var texte = $"{(int)duree.TotalHours:00}:{duree.Minutes:00}";
        return negatif ? $"-{texte}" : texte;
    }

    /// <summary>Meme mise en forme, mais « — » lorsque la duree n'est pas calculable.</summary>
    public static string Formater(TimeSpan? duree) => duree is null ? "—" : Formater(duree.Value);
}
