using Android.App;
using Android.Content;

namespace MelTimeClientAndroid;

/// <summary>
/// Recoit les alertes planifiees par AlarmManager et les affiche.
///
/// Un receveur separe de l'application est indispensable : Android le reveille
/// meme quand MelTime est ferme, ce qui est tout l'interet de ces rappels.
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
public class ReceveurAlerte : BroadcastReceiver
{
    public const string CleTexte = "texte";

    public override void OnReceive(Context? contexte, Intent? intention)
    {
        var texte = intention?.GetStringExtra(CleTexte);

        if (string.IsNullOrWhiteSpace(texte))
        {
            return;
        }

        NotificationsMelTime.CreerCanal();
        NotificationsMelTime.Afficher(texte);
    }
}
