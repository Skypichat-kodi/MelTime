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

    /// <summary>DWMWA_WINDOW_CORNER_PREFERENCE : coins arrondis (Windows 11).</summary>
    private const int AttributCoins = 33;

    /// <summary>DWMWCP_ROUND : arrondi standard du systeme.</summary>
    private const int CoinsArrondis = 2;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr fenetre, int attribut, ref int valeur, int taille);

    /// <summary>A appeler une fois la fenetre dotee d'un handle (evenement SourceInitialized).</summary>
    public static void Appliquer(Window fenetre)    {
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

    /// <summary>
    /// Arrondit les angles de la fenetre. WindowChrome ne sait pas le faire :
    /// c'est le gestionnaire de fenetres de Windows 11 qui dessine l'arrondi,
    /// sur demande. Sans effet sur Windows 10, ou les angles restent carres.
    /// </summary>
    public static void ArrondirCoins(Window fenetre)
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

            var arrondi = CoinsArrondis;
            DwmSetWindowAttribute(handle, AttributCoins, ref arrondi, sizeof(int));
        }
        catch (Exception)
        {
            // Windows 10 ne connait pas cet attribut : les angles restent carres.
        }
    }
}
