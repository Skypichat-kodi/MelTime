using System.Windows;
using MelTime.Core.Api;
using MelTime.Core.Modeles;

namespace MelTime.ClientWindows;

/// <summary>
/// Demarrage du client : tente de reprendre la session enregistree,
/// et ne demande les identifiants que si necessaire.
///
/// Le meme enchainement sert au demarrage et au retour apres deconnexion :
/// une seule logique, donc un seul comportement a connaitre.
/// </summary>
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Une exception non geree fermait l'application et faisait perdre
        // la saisie en cours. On l'affiche, et on laisse la fenetre en vie.
        DispatcherUnhandledException += (_, arguments) =>
        {
            MessageBox.Show(
                "Une erreur inattendue est survenue :" + Environment.NewLine + Environment.NewLine
                + arguments.Exception,
                "MelTime",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            arguments.Handled = true;
        };

        await DemarrerCycleAsync();
    }

    /// <summary>
    /// Reprend la session si elle est valide, sinon demande les identifiants,
    /// puis ouvre la feuille de temps. Utilise au demarrage et apres deconnexion.
    /// </summary>
    public async Task DemarrerCycleAsync()
    {
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
                    // Jeton expire, serveur injoignable ou compte desactive :
                    // on repasse par la connexion.
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
}
