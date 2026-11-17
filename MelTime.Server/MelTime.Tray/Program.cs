namespace MelTime.Tray;

/// <summary>
/// Point d'entree du Tray. Aucune fenetre principale : l'application
/// ne vit que par son icone dans la zone de notification.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new ContexteTray());
    }
}
