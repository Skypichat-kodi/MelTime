using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MelTime.ClientWindows;

/// <summary>
/// Applique le rendu sombre a la barre de titre native d'une fenetre.
/// Sans cela, Windows conserve une barre blanche au-dessus d'une interface sombre.
/// </summary>
internal static class ThemeSombre
{
    private const int AttributModeSombre = 20;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr fenetre, int attribut, ref int valeur, int taille);

    /// <summary>A appeler une fois la fenetre dotee d'un handle (evenement SourceInitialized).</summary>
    public static void Appliquer(Window fenetre)
    {
        if (fenetre is null)
        {
            return;
        }

        try
        {
            var handle = new WindowInteropHelper(fenetre).Handle;

            if (handle == IntPtr.Zero)
            {
                return;
            }

            var actif = 1;
            DwmSetWindowAttribute(handle, AttributModeSombre, ref actif, sizeof(int));
        }
        catch (Exception)
        {
            // Windows 10 ancien ne connait pas cet attribut : la barre reste claire,
            // ce n'est pas une raison pour empecher la fenetre de s'afficher.
        }
    }
}
