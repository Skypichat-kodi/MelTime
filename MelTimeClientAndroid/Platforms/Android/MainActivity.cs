using Android.App;
using Android.Content.PM;
using Android.OS;

namespace MelTimeClientAndroid;

// La feuille de temps presente sept lignes et cinq colonnes : en portrait,
// elle serait illisible sur un telephone. L'application est donc verrouillee
// en paysage, ce qui laisse la largeur necessaire aux saisies.
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ScreenOrientation = ScreenOrientation.Landscape, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}
