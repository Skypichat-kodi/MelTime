using System.Globalization;
using Android.App;
using Android.Content;
using MelTime.Core.Api;

namespace MelTimeClientAndroid;

/// <summary>Une alerte a afficher, datee.</summary>
public sealed record Alerte(int Code, DateTime Quand, string Texte);

/// <summary>
/// Rappels d'horaires : cinq minutes avant et a l'heure precise de chaque
/// changement de la journee.
///
/// Le calcul se fait ici, la programmation aupres d'Android aussi : les
/// alertes arrivent donc meme si MelTime est ferme. Quatre moments sont
/// surveilles : arrivee, debut de pause, fin de pause et depart.
///
/// Seules les journees reellement saisies declenchent des alertes : un samedi
/// vide, un ferie ou un conge restent silencieux.
/// </summary>
public static class AlertesHoraires
{
    /// <summary>Nombre de minutes avant l'evenement pour le preavis.</summary>
    private const int Preavis = 5;

    /// <summary>Libelles des huit alertes d'une journee, dans l'ordre des codes.</summary>
    private static readonly string[] Textes =
    {
        "Attention, il va être l'heure de reprendre le travail.",
        "Travaillez bien.",
        "Bientôt le moment d'une pause.",
        "Profitez bien de ce moment de répit.",
        "La pause se termine dans 5 minutes.",
        "Bonne reprise.",
        "Votre journée s'arrête dans 5 minutes.",
        "Profitez bien de votre repos."
    };

    /// <summary>
    /// Construit les alertes a venir de la semaine, en ignorant celles
    /// deja passees.
    /// </summary>
    public static List<Alerte> Calculer(SemaineApi semaine, DateTime maintenant)
    {
        var alertes = new List<Alerte>();

        foreach (var jour in semaine.Jours)
        {
            // Une journee sans arrivee ni depart n'est pas travaillee.
            if (string.IsNullOrWhiteSpace(jour.Arrivee) || string.IsNullOrWhiteSpace(jour.Depart))
            {
                continue;
            }

            if (!DateTime.TryParseExact(jour.Date, SemaineApi.FormatDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                continue;
            }

            var arrivee = Combiner(date, jour.Arrivee);
            var pauseDebut = Combiner(date, jour.PauseDebut);
            var pauseFin = Combiner(date, jour.PauseFin);
            var depart = Combiner(date, jour.Depart);

            Ajouter(alertes, maintenant, jour.Index, 0, arrivee, Preavis);
            Ajouter(alertes, maintenant, jour.Index, 1, arrivee, 0);
            Ajouter(alertes, maintenant, jour.Index, 2, pauseDebut, Preavis);
            Ajouter(alertes, maintenant, jour.Index, 3, pauseDebut, 0);
            Ajouter(alertes, maintenant, jour.Index, 4, pauseFin, Preavis);
            Ajouter(alertes, maintenant, jour.Index, 5, pauseFin, 0);
            Ajouter(alertes, maintenant, jour.Index, 6, depart, Preavis);
            Ajouter(alertes, maintenant, jour.Index, 7, depart, 0);
        }

        return alertes;
    }

    /// <summary>Programme les alertes aupres du systeme, en remplacant les precedentes.</summary>
    public static void Planifier(IReadOnlyList<Alerte> alertes)
    {
        AnnulerTout();

        if (Platform.AppContext.GetSystemService(Context.AlarmService) is not AlarmManager gestionnaire)
        {
            return;
        }

        foreach (var alerte in alertes)
        {
            var intention = new Intent(Platform.AppContext, typeof(ReceveurAlerte));
            intention.PutExtra(ReceveurAlerte.CleTexte, alerte.Texte);

            var attente = PendingIntent.GetBroadcast(
                Platform.AppContext,
                alerte.Code,
                intention,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            if (attente is null)
            {
                continue;
            }

            var millisecondes = new DateTimeOffset(alerte.Quand).ToUnixTimeMilliseconds();

            try
            {
                // Heure exacte, meme en veille prolongee (Doze).
                gestionnaire.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, millisecondes, attente);
            }
            catch (Exception ex)
            {
                // Android 12 et suivants peuvent refuser l'heure exacte si
                // l'autorisation n'est pas accordee : l'alerte reste posee,
                // simplement moins precise.
                System.Diagnostics.Debug.WriteLine("Alerte exacte refusee : " + ex.Message);

                try
                {
                    gestionnaire.Set(AlarmType.RtcWakeup, millisecondes, attente);
                }
                catch (Exception interieur)
                {
                    System.Diagnostics.Debug.WriteLine("Alerte impossible : " + interieur.Message);
                }
            }
        }
    }

    /// <summary>Retire toutes les alertes posees. Appele a la deconnexion.</summary>
    public static void AnnulerTout()
    {
        if (Platform.AppContext.GetSystemService(Context.AlarmService) is not AlarmManager gestionnaire)
        {
            return;
        }

        for (var code = 0; code < 70; code++)
        {
            var intention = new Intent(Platform.AppContext, typeof(ReceveurAlerte));

            var attente = PendingIntent.GetBroadcast(
                Platform.AppContext,
                code,
                intention,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            if (attente is not null)
            {
                gestionnaire.Cancel(attente);
                attente.Cancel();
            }
        }
    }

    private static void Ajouter(List<Alerte> alertes, DateTime maintenant, int jour, int type, DateTime? moment, int decalageMinutes)
    {
        if (moment is null)
        {
            return;
        }

        var quand = moment.Value.AddMinutes(-decalageMinutes);

        if (quand <= maintenant)
        {
            return;
        }

        alertes.Add(new Alerte(jour * 10 + type, quand, Textes[type]));
    }

    private static DateTime? Combiner(DateTime date, string? heure)
    {
        if (string.IsNullOrWhiteSpace(heure))
        {
            return null;
        }

        if (!TimeOnly.TryParseExact(heure, SemaineApi.FormatHeure, CultureInfo.InvariantCulture, DateTimeStyles.None, out var valeur))
        {
            return null;
        }

        return date.Date.Add(valeur.ToTimeSpan());
    }
}
