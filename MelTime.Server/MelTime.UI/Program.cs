using MelTime.Core.Exploitation;

namespace MelTime.UI;

/// <summary>
/// Point d'entree de l'interface d'administration du serveur.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Une seule fenetre d'administration a la fois.
        using var verrou = InstanceUnique.Acquerir("MelTime.UI", out var dejaLance);

        if (dejaLance)
        {
            MessageBox.Show(
                "L'administration MelTime est déjà ouverte.",
                "MelTime",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
