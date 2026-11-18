using MelTime.Core.Api;
using MelTime.Core.Modeles;

namespace MelTimeClientAndroid;

/// <summary>
/// Ecran de connexion. Le service MelTime doit etre joignable depuis le
/// telephone : meme reseau, ou nom de domaine si le serveur est expose.
/// </summary>
public partial class PageConnexion : ContentPage
{
    private readonly SessionAndroid _session = new();
    private bool _connexionEnCours;

    public PageConnexion()
    {
        InitializeComponent();

        var (hote, port) = _session.DecouperAdresse();

        ChampHote.Text = hote;
        ChampPort.Text = port;
        ChampIdentifiant.Text = _session.Identifiant ?? string.Empty;
    }

    private async void BoutonConnexion_Clicked(object? sender, EventArgs e)
    {
        if (_connexionEnCours)
        {
            return;
        }

        var hote = NettoyerHote(ChampHote.Text);
        var portTexte = (ChampPort.Text ?? string.Empty).Trim();
        var identifiant = (ChampIdentifiant.Text ?? string.Empty).Trim();
        var motDePasse = ChampMotDePasse.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(hote) || string.IsNullOrWhiteSpace(identifiant) || motDePasse.Length == 0)
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

        _connexionEnCours = true;
        BoutonConnexion.IsEnabled = false;
        BoutonConnexion.Text = "Connexion…";
        TexteErreur.IsVisible = false;

        try
        {
            var api = new ClientApi(adresse);
            var reponse = await api.ConnecterAsync(identifiant, motDePasse);

            if (!reponse.Succes || reponse.Jeton is null || reponse.Client is null)
            {
                AfficherErreur(reponse.Erreur ?? "Connexion refusée.");
                api.Dispose();
                return;
            }

            _session.AdresseServeur = adresse;
            _session.Identifiant = identifiant;
            _session.Jeton = reponse.Jeton;

            api.Jeton = reponse.Jeton;

            OuvrirFeuilleDeTemps(api, reponse.Client);
        }
        catch (Exception ex)
        {
            AfficherErreur($"Serveur injoignable : {ex.Message}");
        }
        finally
        {
            _connexionEnCours = false;
            BoutonConnexion.IsEnabled = true;
            BoutonConnexion.Text = "Se connecter";
        }
    }

    /// <summary>Remplace la page courante : on ne revient pas en arriere vers la connexion.</summary>
    private static void OuvrirFeuilleDeTemps(ClientApi api, Client profil)
    {
        var fenetre = Application.Current?.Windows.FirstOrDefault();

        if (fenetre is not null)
        {
            fenetre.Page = new NavigationPage(new PageFeuilleTemps(api, profil));
        }
    }

    private static string NettoyerHote(string? saisie)
    {
        var texte = (saisie ?? string.Empty).Trim().TrimEnd('/');

        if (texte.Contains("://", StringComparison.Ordinal))
        {
            texte = texte[(texte.IndexOf("://", StringComparison.Ordinal) + 3)..];
        }

        var separateur = texte.IndexOf('/');
        return separateur >= 0 ? texte[..separateur] : texte;
    }

    private void AfficherErreur(string message)
    {
        TexteErreur.Text = message;
        TexteErreur.IsVisible = true;
    }
}
