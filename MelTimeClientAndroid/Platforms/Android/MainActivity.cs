using Android.App;
using Android.Content.PM;
using Android.OS;

namespace MelTimeClientAndroid;

// La connexion s'affiche en premier et tient en trois champs : l'activite
// demarre donc en portrait. Les pages basculent ensuite en paysage si besoin
// (OrientationEcran), pour eviter une rotation visible au lancement.
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ScreenOrientation = ScreenOrientation.Portrait, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}
