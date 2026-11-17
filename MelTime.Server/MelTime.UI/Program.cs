namespace MelTime.UI;

/// <summary>
/// Point d'entree de l'interface d'administration du serveur.
/// Etape 1 : fenetre vide. La gestion des clients viendra ensuite.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
