using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MelTime.Core.Api;
using MelTime.Core.Modeles;

namespace MelTime.ClientWindows;

/// <summary>
/// Fenetre de connexion. En cas de succes, conserve l'adresse du serveur
/// et le jeton dans %AppData% pour les prochains lancements.
/// </summary>
public partial class FenetreConnexion : Window
{
    private readonly SessionLocale _session;

    public FenetreConnexion(SessionLocale session)
    {
        InitializeComponent();

        _session = session;

        SourceInitialized += (_, _) => ThemeSombre.Appliquer(this);

        // L'adresse enregistree est une URL complete : on la decompose
        // pour presenter l'hote et le port separement.
        // Tant qu'aucune adresse reelle n'a ete saisie, le champ reste vide
        // et affiche son invite : « localhost » n'aiderait personne.
        if (!session.AdresseParDefautNonRenseignee
            && Uri.TryCreate(session.AdresseServeur, UriKind.Absolute, out var adresse))
        {
            ChampHote.Text = adresse.Host;
            ChampPort.Text = adresse.Port.ToString();
        }
        else
        {
            ChampHote.Text = string.Empty;
            ChampPort.Text = "5080";
        }

        ChampHote.TextChanged += (_, _) => RafraichirInviteHote();
        RafraichirInviteHote();

        ChampIdentifiant.Text = session.Identifiant ?? string.Empty;

        Loaded += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(ChampHote.Text))
            {
                ChampHote.Focus();
            }
            else if (string.IsNullOrWhiteSpace(ChampIdentifiant.Text))
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

    /// <summary>Evite que la synchronisation entre les deux champs ne boucle.</summary>
    private bool _synchronisation;

    private void ChampMotDePasse_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_synchronisation)
        {
            return;
        }

        _synchronisation = true;
        ChampMotDePasseVisible.Text = ChampMotDePasse.Password;
        _synchronisation = false;
    }

    private void ChampMotDePasseVisible_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_synchronisation)
        {
            return;
        }

        _synchronisation = true;
        ChampMotDePasse.Password = ChampMotDePasseVisible.Text;
        _synchronisation = false;
    }

    private void CaseAfficher_Change(object sender, RoutedEventArgs e)
    {
        var afficher = CaseAfficherMotDePasse.IsChecked == true;

        ChampMotDePasseVisible.Visibility = afficher ? Visibility.Visible : Visibility.Collapsed;
        ChampMotDePasse.Visibility = afficher ? Visibility.Collapsed : Visibility.Visible;

        if (afficher)
        {
            ChampMotDePasseVisible.Focus();
            ChampMotDePasseVisible.CaretIndex = ChampMotDePasseVisible.Text.Length;
        }
        else
        {
            ChampMotDePasse.Focus();
        }
    }

    private void RafraichirInviteHote()
    {
        InviteHote.Visibility = ChampHote.Text.Length == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>
    /// Deplace la fenetre : sans barre de titre, c'est le clic sur le panneau
    /// qui fait office de poignee. Les champs absorbent le clic, la saisie
    /// n'est donc pas perturbee.
    /// </summary>
    private void ZoneDeplacement_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    /// <summary>Echap ferme la fenetre, comme dans n'importe quelle boite de dialogue.</summary>
    private void Fenetre_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
        }
    }

    private void BoutonFermer_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private async void BoutonConnexion_Click(object sender, RoutedEventArgs e)    {
        var hote = NormaliserHote(ChampHote.Text);
        var portTexte = ChampPort.Text.Trim();
        var identifiant = ChampIdentifiant.Text.Trim();
        var motDePasse = ChampMotDePasse.Password;

        if (string.IsNullOrWhiteSpace(hote) || string.IsNullOrWhiteSpace(identifiant) || string.IsNullOrEmpty(motDePasse))
        {
            AfficherErreur("Le serveur, l'identifiant et le mot de passe sont obligatoires.");
            return;
        }

        if (!int.TryParse(portTexte, out var port) || port < 1 || port > 65535)
        {
            AfficherErreur("Le port doit être un nombre entre 1 et 65535.");
            return;
        }

        var adresse = $"http://{hote}:{port}";

        BoutonConnexion.IsEnabled = false;
        BoutonConnexion.Content = "Connexion…";
        CadreErreur.Visibility = Visibility.Collapsed;

        try
        {
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

    /// <summary>
    /// Accepte que l'utilisateur colle une URL complete : on ne garde que l'hote.
    /// </summary>
    private static string NormaliserHote(string saisie)
    {
        var texte = saisie.Trim().TrimEnd('/');

        if (texte.Contains("://", StringComparison.Ordinal))
        {
            texte = texte[(texte.IndexOf("://", StringComparison.Ordinal) + 3)..];
        }

        var separateur = texte.IndexOf('/');
        if (separateur >= 0)
        {
            texte = texte[..separateur];
        }

        return texte;
    }

    private void AfficherErreur(string message)
    {
        TexteErreur.Text = message;
        CadreErreur.Visibility = Visibility.Visible;
    }
}
