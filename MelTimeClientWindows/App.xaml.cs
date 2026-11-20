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
            string? messageDemarrage = null;

            if (!string.IsNullOrEmpty(session.Jeton))
            {
                // On interroge d'abord l'etat du serveur : sans cela, un serveur
                // eteint faisait attendre l'expiration du delai avant que la
                // fenetre de connexion n'apparaisse, comme si rien ne se passait.
                if (!await api.EtatAsync())
                {
                    api.Jeton = null;
                    messageDemarrage = "Serveur hors ligne ou inaccessible.";
                }
                else
                {
                    try
                    {
                        profil = await api.ProfilAsync();
                    }
                    catch (Exception)
                    {
                        // Jeton expire ou compte desactive : on repasse par la connexion.
                        api.Jeton = null;
                        session.EffacerJeton();
                    }
                }
            }

            if (profil is null)
            {
                var connexion = new FenetreConnexion(session, messageDemarrage);

                if (connexion.ShowDialog() != true || connexion.Profil is null)
                {
                    Shutdown();
                    return;
                }

                profil = connexion.Profil;

                // L'adresse du serveur a pu changer pendant la connexion :
                // a la premiere installation, elle valait encore l'adresse
                // par defaut, et la feuille de temps interrogeait le mauvais
                // port jusqu'a la reconnexion suivante.
                api.Dispose();
                api = new ClientApi(session.AdresseServeur) { Jeton = session.Jeton };
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
