using System.Windows;
using MelTime.Core.Api;
using MelTime.Core.Modeles;

namespace MelTime.ClientWindows;

/// <summary>
/// Fenetre de connexion. En cas de succes, conserve l'adresse du serveur
/// et le jeton dans %AppData% pour les prochains lancements.
/// </summary>
public partial class FenetreConnexion : Window
{
    private readonly ClientApi _api;
    private readonly SessionLocale _session;

    public FenetreConnexion(ClientApi api, SessionLocale session)
    {
        InitializeComponent();

        _api = api;
        _session = session;

        ChampServeur.Text = session.AdresseServeur;
        ChampIdentifiant.Text = session.Identifiant ?? string.Empty;

        Loaded += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(ChampIdentifiant.Text))
            {
                ChampIdentifiant.Focus();
            }
            else
            {
                ChampMotDePasse.Focus();
            }
        };
    }

    /// <summary>Profil authentifie, renseigne uniquement si la connexion a reussi.</summary>
    public Client? Profil { get; private set; }

    private async void BoutonConnexion_Click(object sender, RoutedEventArgs e)
    {
        var adresse = ChampServeur.Text.Trim();
        var identifiant = ChampIdentifiant.Text.Trim();
        var motDePasse = ChampMotDePasse.Password;

        if (string.IsNullOrWhiteSpace(adresse) || string.IsNullOrWhiteSpace(identifiant) || string.IsNullOrEmpty(motDePasse))
        {
            AfficherErreur("L'adresse du serveur, l'identifiant et le mot de passe sont obligatoires.");
            return;
        }

        BoutonConnexion.IsEnabled = false;
        BoutonConnexion.Content = "Connexion…";
        TexteErreur.Visibility = Visibility.Collapsed;

        try
        {
            // L'adresse peut changer d'un essai a l'autre : on reconstruit le client.
            using var api = new ClientApi(adresse);
            var reponse = await api.ConnecterAsync(identifiant, motDePasse);

            if (!reponse.Succes || reponse.Jeton is null || reponse.Client is null)
            {
                AfficherErreur(reponse.Erreur ?? "Connexion refusée.");
                return;
            }

            _session.AdresseServeur = adresse;
            _session.Identifiant = identifiant;
            _session.Jeton = reponse.Jeton;
            _session.Enregistrer();

            _api.Jeton = reponse.Jeton;

            Profil = reponse.Client;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            AfficherErreur($"Serveur injoignable :{Environment.NewLine}{ex.Message}");
        }
        finally
        {
            BoutonConnexion.IsEnabled = true;
            BoutonConnexion.Content = "Se connecter";
        }
    }

    private void AfficherErreur(string message)
    {
        TexteErreur.Text = message;
        TexteErreur.Visibility = Visibility.Visible;
    }
}
