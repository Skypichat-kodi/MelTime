using System.Globalization;
using MelTime.Core.Api;

namespace MelTimeClientAndroid;

/// <summary>
/// Repose les rappels d'horaires sans que l'utilisateur ouvre l'application.
///
/// Deux evenements declenchent cette remise en place :
///   - chaque nuit a 00 h 05, pour poser les alertes du jour qui commence ;
///   - le demarrage du telephone, car Android efface les alarmes a l'extinction.
///
/// Le calcul se fait a partir de la semaine rechargee sur le serveur : une
/// journee modifiee depuis le client Windows est donc prise en compte. Si le
/// serveur est injoignable, les alarmes deja posees restent en place : mieux
/// vaut des rappels peut-etre perimes que pas de rappel du tout.
/// </summary>
public static class Replanificateur
{
    /// <summary>Action interne du reveil quotidien.</summary>
    public const string ActionReplanifier = "meltime.replanifier";

    /// <summary>Identifiant du PendingIntent du reveil quotidien.</summary>
    private const int CodeReveil = 100;

    /// <summary>Heure locale du reveil quotidien.</summary>
    private static readonly TimeSpan HeureReveil = new(0, 5, 0);

    /// <summary>Recharge la semaine et repose les alertes des jours a venir.</summary>
    public static async Task ExecuterAsync()
    {
        try
        {
            var session = new SessionAndroid();

            var adresse = session.AdresseServeur;
            var jeton = session.Jeton;

            if (string.IsNullOrWhiteSpace(adresse) || string.IsNullOrWhiteSpace(jeton))
            {
                // Session fermee : plus aucun rappel ne doit sonner.
                AlertesHoraires.AnnulerTout();
                return;
            }

            using var api = new ClientApi(adresse) { Jeton = jeton };

            var maintenant = DateTime.Now;
            var annee = ISOWeek.GetYear(maintenant);
            var numero = ISOWeek.GetWeekOfYear(maintenant);

            var semaine = await api.ChargerSemaineAsync(annee, numero);

            NotificationsMelTime.CreerCanal();
            AlertesHoraires.Planifier(AlertesHoraires.Calculer(semaine, maintenant));
        }
        catch (Exception ex)
        {
            // Serveur injoignable ou jeton expire : on laisse les alarmes en place.
            System.Diagnostics.Debug.WriteLine("Replanification impossible : " + ex.Message);
        }
        finally
        {
            PlanifierReveilQuotidien();
        }
    }

    /// <summary>Pose le reveil de la nuit prochaine. A appeler aussi au demarrage de l'application.</summary>
    public static void PlanifierReveilQuotidien()
    {
        try
        {
            if (Platform.AppContext.GetSystemService(Android.Content.Context.AlarmService) is not Android.App.AlarmManager gestionnaire)
            {
                return;
            }

            var prochain = DateTime.Today.AddDays(1).Add(HeureReveil);

            var intention = new Android.Content.Intent(Platform.AppContext, typeof(ReceveurReplanification));
            intention.SetAction(ActionReplanifier);

            var attente = Android.App.PendingIntent.GetBroadcast(
                Platform.AppContext,
                CodeReveil,
                intention,
                Android.App.PendingIntentFlags.UpdateCurrent | Android.App.PendingIntentFlags.Immutable);

            if (attente is null)
            {
                return;
            }

            var millisecondes = new DateTimeOffset(prochain).ToUnixTimeMilliseconds();

            try
            {
                gestionnaire.SetExactAndAllowWhileIdle(Android.App.AlarmType.RtcWakeup, millisecondes, attente);
            }
            catch (Exception)
            {
                gestionnaire.Set(Android.App.AlarmType.RtcWakeup, millisecondes, attente);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Reveil quotidien impossible : " + ex.Message);
        }
    }
}
