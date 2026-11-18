using Android.Content.PM;

namespace MelTimeClientAndroid;

/// <summary>
/// Orientation de l'ecran, reglee par page plutot que globalement.
///
/// La connexion tient en trois champs : elle est plus naturelle en portrait.
/// La feuille de temps, elle, aligne sept lignes et cinq colonnes : elle exige
/// la largeur du paysage. L'orientation bascule donc d'un ecran a l'autre.
/// </summary>
public static class OrientationEcran
{
    public static void Portrait() => Forcer(ScreenOrientation.Portrait);

    public static void Paysage() => Forcer(ScreenOrientation.Landscape);

    private static void Forcer(ScreenOrientation orientation)
    {
        try
        {
            if (Platform.CurrentActivity is Android.App.Activity activite)
            {
                activite.RequestedOrientation = orientation;
            }
        }
        catch (Exception)
        {
            // L'orientation est un confort : son echec ne doit pas interrompre.
        }
    }
}
