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
        if ((int)Build.VERSION.SdkInt < 26)
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
            if ((int)Build.VERSION.SdkInt < 33)
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

            var notification = new NotificationCompat.Builder(contexte, Canal)
                .SetContentTitle("MelTime")
                .SetContentText(texte)
                .SetStyle(new NotificationCompat.BigTextStyle().BigText(texte))
                .SetSmallIcon(Resource.Mipmap.appicon)
                .SetPriority(NotificationCompat.PriorityHigh)
                .SetAutoCancel(true)
                .SetContentIntent(intention)
                .Build();

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
