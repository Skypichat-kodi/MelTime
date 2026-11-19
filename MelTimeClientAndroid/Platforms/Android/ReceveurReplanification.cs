using Android.App;
using Android.Content;

namespace MelTimeClientAndroid;

/// <summary>
/// Recoit les deux evenements qui exigent de reposer les rappels :
/// le reveil de la nuit, et le demarrage du telephone.
///
/// Un receveur n'a que quelques secondes devant lui : GoAsync prolonge ce
/// delai le temps de joindre le serveur et de recalculer les alertes.
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter(new[] { Intent.ActionBootCompleted, Intent.ActionMyPackageReplaced, Replanificateur.ActionReplanifier })]
public class ReceveurReplanification : BroadcastReceiver
{
    public override void OnReceive(Context? contexte, Intent? intention)
    {
        var termine = GoAsync();

        _ = Task.Run(async () =>
        {
            try
            {
                await Replanificateur.ExecuterAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Replanification : " + ex.Message);
            }
            finally
            {
                termine.Finish();
            }
        });
    }
}
