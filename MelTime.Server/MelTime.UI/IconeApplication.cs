namespace MelTime.UI;

/// <summary>
/// Icone de l'application, reprise de l'executable lui-meme.
///
/// Declarer ApplicationIcon dans le .csproj donne une icone au fichier .exe,
/// mais une fenetre WinForms n'en herite pas : sans Icon explicite, elle
/// affiche l'icone generique de Windows.
/// </summary>
internal static class IconeApplication
{
    private static Icon? _icone;

    public static Icon? Obtenir()
    {
        if (_icone is not null)
        {
            return _icone;
        }

        try
        {
            var chemin = Environment.ProcessPath;

            if (!string.IsNullOrEmpty(chemin))
            {
                _icone = Icon.ExtractAssociatedIcon(chemin);
            }
        }
        catch (Exception)
        {
            // Sans icone, la fenetre s'affiche normalement : ce n'est pas bloquant.
        }

        return _icone;
    }
}
