using MelTime.Core.Api;
using MelTime.Core.Modeles;

namespace MelTimeClientAndroid;

/// <summary>
/// Ecran de connexion. Le service MelTime doit etre joignable depuis le
/// telephone : meme reseau, ou nom de domaine si le serveur est expose.
///
/// La connexion par empreinte evite de retaper le mot de passe a chaque
/// ouverture. Le mot de passe n'est pas garde en clair : il est range dans
/// le coffre chiffre d'Android (SecureStorage), et l'empreinte autorise
/// seulement sa relecture.
/// </summary>
public partial class PageConnexion : ContentPage
{
    private readonly SessionAndroid _session = new();
    private bool _connexionEnCours;

    /// <summary>Evite de redemander l'empreinte a chaque retour sur la page.</summary>
    private bool _empreinteProposee;

    /// <summary>
    /// Vrai au lancement de l'application : l'empreinte est alors demandee
    /// d'emblee. Faux apres une deconnexion volontaire, ou l'utilisateur
    /// veut pouvoir changer de compte sans etre reconnecte aussitot.
    /// </summary>
    private readonly bool _empreinteAutomatique;

    /// <summary>Chargement du coffre, termine ou non.</summary>
    private Task _chargementEmpreinte = Task.CompletedTask;

    public PageConnexion(bool empreinteAutomatique = true)
    {
        InitializeComponent();

        _empreinteAutomatique = empreinteAutomatique;

        var (hote, port) = _session.DecouperAdresse();

        ChampHote.Text = hote;
        ChampPort.Text = port;
        ChampIdentifiant.Text = _session.Identifiant ?? string.Empty;
        CaseBiometrie.IsChecked = _session.BiometrieActivee;

        TexteVersion.Text = $"{AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})";

        _chargementEmpreinte = ChargerEmpreinteAsync();
    }

    /// <summary>La connexion s'affiche en portrait : trois champs n'ont pas besoin de largeur.</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        OrientationEcran.Portrait();

        // L'empreinte est demandee d'emblee quand elle est disponible :
        // l'utilisateur n'a pas a appuyer sur un bouton pour cela.
        ProposerEmpreinte();
    }

    /// <summary>
    /// Lance la demande d'empreinte une seule fois, et seulement si elle est
    /// reellement utilisable : un mot de passe memorise, une empreinte
    /// enregistree sur le telephone, et la case cochee.
    ///
    /// Apres une deconnexion, la demande n'est pas automatique : l'utilisateur
    /// garde la main pour changer de compte.
    /// </summary>
    private async void ProposerEmpreinte()
    {
        if (!_empreinteAutomatique)
        {
            return;
        }

        // La lecture du coffre est asynchrone : sans cette attente, le bouton
        // n'etait pas encore visible et la demande etait abandonnee.
        await _chargementEmpreinte;

        if (_empreinteProposee || !BoutonEmpreinte.IsVisible)
        {
            return;
        }

        _empreinteProposee = true;

        await ConnexionParEmpreinteAsync();
    }

    /// <summary>
    /// Devoile le mot de passe d'un appui sur l'oeil, et le remasque au
    /// suivant. L'oeil s'eclaire tant que le texte est visible, pour qu'on
    /// sache d'un coup d'oeil dans quel etat on est.
    /// </summary>
    private void BoutonOeil_Tapped(object? sender, TappedEventArgs e)
    {
        var visible = ChampMotDePasse.IsPassword;

        ChampMotDePasse.IsPassword = !visible;

        // Bleu quand le texte est devoile, gris quand il est masque ; la barre
        // diagonale n'apparait que dans le second cas.
        var couleur = visible ? Color.FromArgb("#0078D4") : Color.FromArgb("#A0A0A0");

        OeilContour.Stroke = new SolidColorBrush(couleur);
        OeilPupille.Stroke = new SolidColorBrush(couleur);
        OeilBarre.Stroke = new SolidColorBrush(couleur);
        OeilBarre.IsVisible = visible;
    }

    /// <summary>
    /// Prepare la connexion par empreinte : sans empreinte enregistree sur le
    /// telephone, la case et le bouton restent invisibles.
    /// </summary>
    private async Task ChargerEmpreinteAsync()
    {
        var disponible = Biometrie.EstDisponible;

        ZoneBiometrie.IsVisible = disponible;

        if (!disponible)
        {
            BoutonEmpreinte.IsVisible = false;
            return;
        }

        try
        {
            var motDePasse = await _session.LireMotDePasseAsync();

            BoutonEmpreinte.IsVisible = _session.BiometrieActivee && !string.IsNullOrEmpty(motDePasse);
        }
        catch (Exception ex)
        {
            // Le coffre chiffre peut etre invalide apres une reinstallation :
            // Android refuse alors de rendre la cle. On repart d'une saisie.
            System.Diagnostics.Debug.WriteLine("Coffre illisible : " + ex.Message);
            _session.EffacerMotDePasse();
            BoutonEmpreinte.IsVisible = false;
        }
    }

    /// <summary>
    /// La case commande la visibilite du bouton : la decocher le fait
    /// disparaitre aussitot, la recocher le ramene si une empreinte est
    /// enregistree.
    /// </summary>
    private async void CaseBiometrie_Changed(object? sender, CheckedChangedEventArgs e)
    {
        if (!Biometrie.EstDisponible)
        {
            return;
        }

        if (!e.Value)
        {
            BoutonEmpreinte.IsVisible = false;
            return;
        }

        try
        {
            var motDePasse = await _session.LireMotDePasseAsync();

            BoutonEmpreinte.IsVisible = _session.BiometrieActivee && !string.IsNullOrEmpty(motDePasse);
        }
        catch (Exception)
        {
            BoutonEmpreinte.IsVisible = false;
        }
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

        await ConnecterAsync($"http://{hote}:{port}", identifiant, motDePasse);
    }

    /// <summary>
    /// Connexion par empreinte : le systeme reconnait le doigt, puis le mot
    /// de passe memorise est reutilise sans que l'utilisateur le revoie.
    /// </summary>
    private async void BoutonEmpreinte_Clicked(object? sender, EventArgs e)
        => await ConnexionParEmpreinteAsync();

    /// <summary>
    /// Demande l'empreinte, puis se connecte avec le mot de passe memorise
    /// si elle est reconnue. Appelee au premier affichage comme par le bouton.
    /// </summary>
    private async Task ConnexionParEmpreinteAsync()
    {
        if (_connexionEnCours)
        {
            return;
        }

        var identifiant = _session.Identifiant;
        var adresse = _session.AdresseServeur;

        if (string.IsNullOrWhiteSpace(identifiant) || string.IsNullOrWhiteSpace(adresse))
        {
            AfficherErreur("Aucune connexion enregistrée. Connectez-vous d'abord avec votre mot de passe.");
            return;
        }

        var motDePasse = await _session.LireMotDePasseAsync();

        if (string.IsNullOrEmpty(motDePasse))
        {
            _session.EffacerMotDePasse();
            BoutonEmpreinte.IsVisible = false;
            AfficherErreur("Le mot de passe enregistré a été effacé. Reconnectez-vous avec votre mot de passe.");
            return;
        }

        var reconnu = await Biometrie.AuthentifierAsync("MelTime", "Confirmez votre empreinte");

        if (!reconnu)
        {
            return;
        }

        // Le coffre a pu etre vide entre-temps : on verifie une derniere fois.
        if (string.IsNullOrEmpty(motDePasse))
        {
            return;
        }

        ChampIdentifiant.Text = identifiant;

        await ConnecterAsync(adresse, identifiant, motDePasse);
    }

    private async Task ConnecterAsync(string adresse, string identifiant, string motDePasse)
    {
        _connexionEnCours = true;
        BoutonConnexion.IsEnabled = false;
        BoutonEmpreinte.IsEnabled = false;
        BoutonConnexion.Text = "Connexion…";
        TexteErreur.IsVisible = false;

        try
        {
            var api = new ClientApi(adresse);

            // On verifie d'abord que le serveur repond : sans cela, l'erreur
            // affichee etait celle de la couche reseau, incomprehensible.
            if (!await api.EtatAsync())
            {
                AfficherErreur("Serveur hors ligne ou inaccessible.");
                api.Dispose();
                return;
            }

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

            await MemoriserEmpreinteAsync(motDePasse);

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
            BoutonEmpreinte.IsEnabled = true;
            BoutonConnexion.Text = "Se connecter";
        }
    }

    /// <summary>
    /// Retient le mot de passe si la case est cochee, l'oublie sinon.
    /// Un mot de passe refusé n'est jamais memorise : on n'arrive ici
    /// qu'apres une connexion acceptee par le serveur.
    /// </summary>
    private async Task MemoriserEmpreinteAsync(string motDePasse)
    {
        if (!Biometrie.EstDisponible || CaseBiometrie.IsChecked != true)
        {
            _session.EffacerMotDePasse();
            BoutonEmpreinte.IsVisible = false;
            return;
        }

        try
        {
            await _session.EnregistrerMotDePasseAsync(motDePasse);
            _session.BiometrieActivee = true;
            BoutonEmpreinte.IsVisible = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Empreinte non memorisee : " + ex.Message);
            _session.EffacerMotDePasse();
            BoutonEmpreinte.IsVisible = false;
        }
    }

    /// <summary>Remplace la page courante : on ne revient pas en arriere vers la connexion.</summary>
    private static void OuvrirFeuilleDeTemps(ClientApi api, Client profil)
    {
        var fenetre = Application.Current?.Windows.FirstOrDefault();

        if (fenetre is not null)
        {
            fenetre.Page = new PageFeuilleTemps(api, profil);
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
