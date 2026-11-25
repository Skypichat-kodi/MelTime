using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace MelTimeClientAndroid;

/// <summary>
/// Affichage des notifications MelTime.
///
/// Elles passent par un canal dedie : Android permet ainsi au client de
/// couper les alertes d'horaires sans couper le reste, depuis ses reglages.
/// </summary>
public static class NotificationsMelTime
{
    public const string Canal = "meltime.alertes";

    /// <summary>Cree le canal. Sans appel, rien ne s'affiche sur Android 8 et suivants.</summary>
    public static void CreerCanal()
    {
        // Les canaux de notification sont apparus avec Android 8 (API 26).
        // OperatingSystem.IsAndroidVersionAtLeast est reconnu par l'analyseur,
        // contrairement a un simple test sur Build.VERSION.
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        if (Platform.AppContext.GetSystemService(Context.NotificationService) is not NotificationManager gestionnaire)
        {
            return;
        }

        var canal = new NotificationChannel(Canal, "Alertes d'horaires", NotificationImportance.High)
        {
            Description = "Rappels avant et au moment des changements de votre journée"
        };

        gestionnaire.CreateNotificationChannel(canal);
    }

    /// <summary>Demande l'autorisation d'afficher des notifications (Android 13 et suivants).</summary>
    public static async Task DemanderAutorisationAsync()
    {
        try
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                return;
            }

            await Permissions.RequestAsync<Permissions.PostNotifications>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Autorisation notifications : " + ex.Message);
        }
    }

    /// <summary>Affiche une alerte. Le clic ouvre l'application.</summary>
    public static void Afficher(string texte)
    {
        try
        {
            var contexte = Platform.AppContext;

            var ouverture = contexte.PackageManager?.GetLaunchIntentForPackage(contexte.PackageName ?? string.Empty);
            var intention = ouverture is null
                ? null
                : PendingIntent.GetActivity(contexte, 0, ouverture, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            // Les methodes du constructeur AndroidX sont declarees nullables :
            // les appeler enchainees declenche un avertissement a chaque maillon.
            // En instructions separees, seul l'objet initial compte.
            var constructeur = new NotificationCompat.Builder(contexte, Canal)!;

            constructeur.SetContentTitle("MelTime");
            constructeur.SetContentText(texte);
            constructeur.SetStyle(new NotificationCompat.BigTextStyle().BigText(texte));
            constructeur.SetSmallIcon(Resource.Mipmap.appicon);
            constructeur.SetPriority(NotificationCompat.PriorityHigh);
            constructeur.SetAutoCancel(true);

            // Le clic n'ouvre l'application que si l'intention a pu etre creee.
            if (intention is not null)
            {
                constructeur.SetContentIntent(intention);
            }

            if (constructeur.Build() is not { } notification)
            {
                return;
            }

            if (contexte.GetSystemService(Context.NotificationService) is NotificationManager gestionnaire)
            {
                // Un identifiant par texte : deux alertes proches ne s'ecrasent pas.
                gestionnaire.Notify(texte.GetHashCode() & 0xFFFF, notification);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Notification impossible : " + ex.Message);
        }
    }
}
