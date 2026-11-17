using MelTime.Core.Exploitation;

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
        // Une seule icone : un second lancement s'arrete immediatement.
        using var verrou = InstanceUnique.Acquerir("MelTime.Tray", out var dejaLance);

        if (dejaLance)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new ContexteTray());
    }
}
