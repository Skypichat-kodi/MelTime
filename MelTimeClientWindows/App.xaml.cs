using System.Windows;
using MelTime.Core.Api;
using MelTime.Core.Modeles;

namespace MelTime.ClientWindows;

/// <summary>
/// Demarrage du client : tente de reprendre la session enregistree,
/// et ne demande les identifiants que si necessaire.
/// </summary>
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var session = SessionLocale.Charger();
            var api = new ClientApi(session.AdresseServeur) { Jeton = session.Jeton };

            Client? profil = null;

            if (!string.IsNullOrEmpty(session.Jeton))
            {
                try
                {
                    profil = await api.ProfilAsync();
                }
                catch (Exception)
                {
                    // Jeton expire, serveur injoignable ou compte desactive : on repasse par la connexion.
                    api.Jeton = null;
                    session.EffacerJeton();
                }
            }

            if (profil is null)
            {
                var connexion = new FenetreConnexion(api, session);

                if (connexion.ShowDialog() != true || connexion.Profil is null)
                {
                    Shutdown();
                    return;
                }

                profil = connexion.Profil;
            }

            var principale = new FenetrePrincipale(api, session, profil);
            MainWindow = principale;
            principale.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Impossible de démarrer MelTime :" + Environment.NewLine + ex.Message,
                "MelTime",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
    }
}
