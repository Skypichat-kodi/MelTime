using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MelTime.Tray;

/// <summary>
/// Icone de la zone de notification.
///
/// Les deux etats sont dessines par l'utilisateur et embarques dans
/// l'executable : un point vert quand le service repond, un rouge sinon.
/// Si une ressource venait a manquer, un rond est dessine par code en secours,
/// pour que le Tray ne reste jamais sans icone.
/// </summary>
internal sealed class IconeEtat : IDisposable
{
    private const string EspaceRessources = "MelTime.Tray.Resources.Icons.";

    private IntPtr _handle;

    public IconeEtat(string nomFichier, Color couleurSecours)
    {
        Icone = Charger(nomFichier) ?? Dessiner(couleurSecours);

        if (_handle == IntPtr.Zero && Icone is not null)
        {
            // Icone issue d'une ressource : le handle appartient a l'Icon,
            // qui le liberera lui-meme.
            _handle = IntPtr.Zero;
        }
    }

    public Icon? Icone { get; }

    /// <summary>Charge une icone embarquee, ou retourne null si elle est absente.</summary>
    private static Icon? Charger(string nomFichier)
    {
        try
        {
            var nom = EspaceRessources + nomFichier;
            using var flux = Assembly.GetExecutingAssembly().GetManifestResourceStream(nom);

            return flux is null ? null : new Icon(flux);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Dessine un rond par code, utilise uniquement si la ressource manque.</summary>
    private Icon Dessiner(Color couleur)
    {
        const int taille = 32;

        using var bitmap = new Bitmap(taille, taille);

        using (var dessin = Graphics.FromImage(bitmap))
        {
            dessin.SmoothingMode = SmoothingMode.AntiAlias;
            dessin.Clear(Color.Transparent);

            const int marge = 3;
            var diametre = taille - (marge * 2);

            using var remplissage = new SolidBrush(couleur);
            dessin.FillEllipse(remplissage, marge, marge, diametre, diametre);

            using var contour = new Pen(Color.FromArgb(90, 0, 0, 0));
            dessin.DrawEllipse(contour, marge, marge, diametre, diametre);
        }

        _handle = bitmap.GetHicon();
        return Icon.FromHandle(_handle);
    }

    public void Dispose()
    {
        Icone?.Dispose();

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
