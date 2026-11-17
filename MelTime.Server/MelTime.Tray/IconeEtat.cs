using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace MelTime.Tray;

/// <summary>
/// Icone de la zone de notification, dessinee par code pour eviter
/// d'embarquer des fichiers .ico binaires dans le projet.
/// Le handle natif doit etre detruit explicitement : Icon.FromHandle ne le fait pas.
/// </summary>
internal sealed class IconeEtat : IDisposable
{
    private const int Taille = 32;

    private IntPtr _handle;

    public IconeEtat(Color couleur)
    {
        using var bitmap = new Bitmap(Taille, Taille);

        using (var dessin = Graphics.FromImage(bitmap))
        {
            dessin.SmoothingMode = SmoothingMode.AntiAlias;
            dessin.Clear(Color.Transparent);

            var marge = 3;
            var diametre = Taille - (marge * 2);

            using var remplissage = new SolidBrush(couleur);
            dessin.FillEllipse(remplissage, marge, marge, diametre, diametre);

            using var contour = new Pen(Color.FromArgb(90, 0, 0, 0));
            dessin.DrawEllipse(contour, marge, marge, diametre, diametre);
        }

        _handle = bitmap.GetHicon();
        Icone = Icon.FromHandle(_handle);
    }

    public Icon Icone { get; }

    public void Dispose()
    {
        Icone.Dispose();

        if (_handle != IntPtr.Zero)
        {
            DestroyIcon(_handle);
            _handle = IntPtr.Zero;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
