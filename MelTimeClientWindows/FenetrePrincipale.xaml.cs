using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MelTime.Core.Agenda;
using MelTime.Core.Api;
using MelTime.Core.Modeles;

namespace MelTime.ClientWindows;

/// <summary>
/// Feuille de temps hebdomadaire : sept jours, quatre horaires par jour,
/// total de la semaine et heures supplementaires.
/// Les totaux affiches sont calcules localement pour repondre instantanement,
/// mais le serveur recalcule tout a l'enregistrement : c'est lui qui fait foi.
/// </summary>
public partial class FenetrePrincipale : Window
{
    private static readonly string[] NomsJours =
    {
        "Lundi", "Mardi", "Mercredi", "Jeudi", "Vendredi", "Samedi", "Dimanche"
    };

    /// <summary>Fond legerement plus clair des lignes paires, pour guider l'oeil.</summary>
    private static readonly Brush FondLigneAlternee = new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x2B));

    /// <summary>Recupere une couleur de la palette definie dans App.xaml.</summary>
    private static Brush Palette(string cle) =>
        Application.Current.TryFindResource(cle) as Brush ?? Brushes.Gray;

    private readonly ClientApi _api;
    private readonly SessionLocale _session;

    private readonly SelecteurHeure[] _arrivees = new SelecteurHeure[7];
    private readonly SelecteurHeure[] _pausesDebut = new SelecteurHeure[7];
    private readonly SelecteurHeure[] _pausesFin = new SelecteurHeure[7];
    private readonly SelecteurHeure[] _departs = new SelecteurHeure[7];
    private readonly TextBlock[] _etiquettesJours = new TextBlock[7];
    private readonly TextBlock[] _travaille = new TextBlock[7];

    private SemaineApi _semaine = new();
    private bool _suspendu;
    private bool _modifie;
    private bool _coherent = true;
    private bool _erreurAffichee;

    public FenetrePrincipale(ClientApi api, SessionLocale session, Client profil)
    {
        InitializeComponent();

        _api = api;
        _session = session;
        Profil = profil;

        TexteUtilisateur.Text = $"{profil.Prenom} {profil.Nom}";

        SourceInitialized += (_, _) => ThemeSombre.Appliquer(this);

        ConstruireGrille();
        RemplirContrat();

        Loaded += async (_, _) => await ChargerSemaineCouranteAsync();
        Closed += (_, _) => Application.Current.Shutdown();
    }

    public Client Profil { get; }

    private int ContratMinutes =>
        (ChoixContratHeures.SelectedItem is int heures ? heures : 35) * 60
        + (ChoixContratMinutes.SelectedItem is int minutes ? minutes : 0);

    // ------------------------------------------------------------------
    // Construction de la grille
    // ------------------------------------------------------------------

    private void ConstruireGrille()
    {
        GrilleJours.RowDefinitions.Clear();
        GrilleJours.ColumnDefinitions.Clear();

        GrilleJours.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });

        for (var index = 0; index < 4; index++)
        {
            GrilleJours.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        GrilleJours.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        GrilleJours.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });

        var entetes = new[] { "Jour", "Arrivée", "Pause début", "Pause fin", "Départ", "Travaillé", string.Empty };

        GrilleJours.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (var colonne = 0; colonne < entetes.Length; colonne++)
        {
            // La colonne « Travaille » est mise en avant : c'est le resultat
            // du calcul, la seule colonne que l'utilisateur ne saisit pas.
            var estTravaille = colonne == 5;

            var texte = new TextBlock
            {
                Text = entetes[colonne],
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Foreground = estTravaille ? Palette("Accent") : Palette("TexteSecondaire"),
                VerticalAlignment = VerticalAlignment.Center
            };

            var entete = Cellule(texte, new Thickness(10, 9, 8, 9));
            entete.Background = Palette("SurfaceSurvol");
            entete.BorderThickness = new Thickness(0, 0, 1, 1);

            Grid.SetRow(entete, 0);
            Grid.SetColumn(entete, colonne);
            GrilleJours.Children.Add(entete);
        }

        for (var jour = 0; jour < 7; jour++)
        {
            GrilleJours.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });

            var ligne = jour + 1;
            var fond = jour % 2 == 1 ? FondLigneAlternee : Brushes.Transparent;

            _etiquettesJours[jour] = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold,
                Foreground = Palette("Texte")
            };
            AjouterCellule(ligne, 0, _etiquettesJours[jour], new Thickness(10, 4, 8, 4), fond);

            _arrivees[jour] = AjouterSelecteur(ligne, 1, fond);
            _pausesDebut[jour] = AjouterSelecteur(ligne, 2, fond);
            _pausesFin[jour] = AjouterSelecteur(ligne, 3, fond);
            _departs[jour] = AjouterSelecteur(ligne, 4, fond);

            _travaille[jour] = new TextBlock
            {
                Text = "—",
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = Palette("Texte")
            };

            // Alignement explicitement transmis : AjouterCellule applique sinon
            // Stretch, qui annule le centrage demande ci-dessus.
            AjouterCellule(ligne, 5, _travaille[jour], new Thickness(6, 4, 6, 4), fond, HorizontalAlignment.Center);

            var croix = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 0 0 L 9 9 M 9 0 L 0 9"),
                Stroke = Palette("Erreur"),
                StrokeThickness = 2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var boutonEffacer = new Button
            {
                Content = croix,
                Width = 28,
                Height = 24,
                Padding = new Thickness(0),
                Style = Application.Current.TryFindResource("BoutonFantome") as Style,
                ToolTip = "Effacer cette journée"
            };

            // Copie locale indispensable : dans une boucle « for », la variable
            // est unique et capturee par reference. Sans cette copie, les sept
            // gestionnaires recevraient la valeur finale (7), hors du tableau.
            var indexJour = jour;
            boutonEffacer.Click += (_, _) => EffacerJour(indexJour);

            AjouterCellule(ligne, 6, boutonEffacer, new Thickness(8, 4, 8, 4), fond, HorizontalAlignment.Center);
        }
    }

    private SelecteurHeure AjouterSelecteur(int ligne, int colonne, Brush fond)
    {
        var selecteur = new SelecteurHeure
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Width = 112,
            Margin = new Thickness(0, 4, 0, 4)
        };

        selecteur.HeureModifiee += SurHeureModifiee;

        AjouterCellule(ligne, colonne, selecteur, new Thickness(8, 0, 8, 0), fond, HorizontalAlignment.Center);

        return selecteur;
    }

    private void AjouterCellule(
        int ligne,
        int colonne,
        UIElement contenu,
        Thickness marge,
        Brush fond,
        HorizontalAlignment alignement = HorizontalAlignment.Stretch)
    {
        var cellule = Cellule(contenu, marge);
        cellule.Background = fond;

        if (contenu is FrameworkElement element)
        {
            element.HorizontalAlignment = alignement;
        }

        cellule.HorizontalAlignment = HorizontalAlignment.Stretch;

        Grid.SetRow(cellule, ligne);
        Grid.SetColumn(cellule, colonne);
        GrilleJours.Children.Add(cellule);
    }

    private static Border Cellule(UIElement contenu, Thickness marge) => new()
    {
        BorderBrush = Palette("Bordure"),
        BorderThickness = new Thickness(0, 0, 1, 1),
        Padding = marge,
        Child = contenu
    };

    private void RemplirContrat()
    {
        _suspendu = true;

        for (var heures = 0; heures <= 60; heures++)
        {
            ChoixContratHeures.Items.Add(heures);
        }

        for (var minutes = 0; minutes <= 59; minutes++)
        {
            ChoixContratMinutes.Items.Add(minutes);
        }

        _suspendu = false;
    }

    private void RemplirAnnees(int anneeCourante)
    {
        for (var annee = anneeCourante - 3; annee <= anneeCourante + 1; annee++)
        {
            ChoixAnnee.Items.Add(annee);
        }
    }

    private void RemplirSemaines(int anneeIso, int numero)
    {
        ChoixSemaine.Items.Clear();

        var total = SemaineTravail.SemainesDansLAnnee(anneeIso);

        for (var semaine = 1; semaine <= total; semaine++)
        {
            ChoixSemaine.Items.Add(semaine);
        }

        ChoixSemaine.SelectedItem = Math.Min(numero, total);
    }

    // ------------------------------------------------------------------
    // Chargement et affichage
    // ------------------------------------------------------------------

    private async Task ChargerSemaineCouranteAsync()
    {
        var (anneeIso, numero) = SemaineTravail.SemaineIsoDe(DateOnly.FromDateTime(DateTime.Today));

        _suspendu = true;
        RemplirAnnees(anneeIso);
        ChoixAnnee.SelectedItem = anneeIso;
        RemplirSemaines(anneeIso, numero);
        _suspendu = false;

        await ChargerSemaineAsync(anneeIso, numero);
    }

    private async Task ChargerSemaineAsync(int anneeIso, int numero)
    {
        try
        {
            TexteStatut.Text = "Chargement…";

            _semaine = await _api.ChargerSemaineAsync(anneeIso, numero);
            AfficherSemaine();
        }
        catch (UnauthorizedAccessException)
        {
            SignalerSessionExpiree();
        }
        catch (Exception ex)
        {
            TexteStatut.Text = "Chargement impossible";
            AfficherBandeau("Impossible de charger la semaine : " + ex.Message);
        }
    }

    private void AfficherSemaine()
    {
        _suspendu = true;

        for (var jour = 0; jour < 7; jour++)
        {
            var donnee = jour < _semaine.Jours.Count ? _semaine.Jours[jour] : null;

            _arrivees[jour].Heure = EnHeure(donnee?.Arrivee);
            _pausesDebut[jour].Heure = EnHeure(donnee?.PauseDebut);
            _pausesFin[jour].Heure = EnHeure(donnee?.PauseFin);
            _departs[jour].Heure = EnHeure(donnee?.Depart);
            _travaille[jour].Text = donnee?.Travaille ?? "—";

            var date = "  ";
            if (donnee is not null
                && DateOnly.TryParseExact(donnee.Date, SemaineApi.FormatDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var valeur))
            {
                date = valeur.ToString("dd/MM", CultureInfo.InvariantCulture);
            }

            _etiquettesJours[jour].Text = $"{NomsJours[jour]}   {date}";
        }

        ChoixContratHeures.SelectedItem = Math.Clamp(_semaine.ContratHebdomadaireMinutes / 60, 0, 60);
        ChoixContratMinutes.SelectedItem = Math.Clamp(_semaine.ContratHebdomadaireMinutes % 60, 0, 59);

        SynchroniserNavigation();

        _suspendu = false;

        MettreAJourTotaux();
        AfficherPeriode();
        MarquerModifie(false);
    }

    /// <summary>
    /// Affiche la periode couverte par la semaine. Remplace l'ancien
    /// « Chargement… », qui n'etait jamais efface et restait donc affiche
    /// en permanence, sans rien apprendre a l'utilisateur.
    /// </summary>
    private void AfficherPeriode()
    {
        var premier = _semaine.Jours.FirstOrDefault();
        var dernier = _semaine.Jours.LastOrDefault();

        if (premier is null || dernier is null
            || !DateOnly.TryParseExact(premier.Date, SemaineApi.FormatDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var debut)
            || !DateOnly.TryParseExact(dernier.Date, SemaineApi.FormatDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fin))
        {
            TexteStatut.Text = string.Empty;
            return;
        }

        TexteStatut.Text = $"du {debut:dd/MM} au {fin:dd/MM/yyyy}";
        TexteStatut.Foreground = Palette("TexteSecondaire");
    }

    /// <summary>Aligne les listes de navigation sur la semaine affichee, sans declencher de rechargement.</summary>
    private void SynchroniserNavigation()
    {
        var etait = _suspendu;
        _suspendu = true;

        if (!ChoixAnnee.Items.Contains(_semaine.AnneeIso))
        {
            ChoixAnnee.Items.Add(_semaine.AnneeIso);
        }

        ChoixAnnee.SelectedItem = _semaine.AnneeIso;

        if (ChoixSemaine.Items.Count != SemaineTravail.SemainesDansLAnnee(_semaine.AnneeIso))
        {
            RemplirSemaines(_semaine.AnneeIso, _semaine.Numero);
        }

        ChoixSemaine.SelectedItem = _semaine.Numero;

        _suspendu = etait;
    }

    private void MettreAJourTotaux()
    {
        if (_semaine.AnneeIso <= 0 || _semaine.Numero <= 0)
        {
            return;
        }

        var modele = ConstruireModele();

        TexteTotal.Text = Durees.Formater(modele.TotalTravaille);
        TexteSupplementaires.Text = $"Heures supp. : {Durees.Formater(modele.HeuresSupplementaires())}";

        for (var jour = 0; jour < 7; jour++)
        {
            var cellule = modele.Jours[jour];
            var chronologique = cellule.EstChronologique;

            _travaille[jour].Text = chronologique ? Durees.Formater(cellule.DureeTravaillee) : "⚠";
            _travaille[jour].Foreground = chronologique ? Palette("Accent") : Palette("Erreur");
            _travaille[jour].ToolTip = chronologique
                ? null
                : cellule.Valider().FirstOrDefault();
        }

        // Une journee incoherente ne produit aucun total : il faut le dire clairement,
        // sinon l'utilisateur croirait simplement que rien n'est saisi.
        var anomalies = modele.Valider();
        _coherent = anomalies.Count == 0;

        if (!_coherent)
        {
            TexteStatut.Text = anomalies[0];
            TexteStatut.Foreground = Palette("Erreur");
            _erreurAffichee = true;
        }
        else if (_erreurAffichee)
        {
            TexteStatut.Text = string.Empty;
            TexteStatut.Foreground = Palette("TexteSecondaire");
            _erreurAffichee = false;
        }
    }

    private SemaineTravail ConstruireModele()
    {
        var modele = SemaineTravail.Creer(_semaine.AnneeIso, _semaine.Numero);
        modele.ContratHebdomadaireMinutes = ContratMinutes;

        for (var jour = 0; jour < 7; jour++)
        {
            modele.Jours[jour].Arrivee = _arrivees[jour].Heure;
            modele.Jours[jour].PauseDebut = _pausesDebut[jour].Heure;
            modele.Jours[jour].PauseFin = _pausesFin[jour].Heure;
            modele.Jours[jour].Depart = _departs[jour].Heure;
        }

        return modele;
    }

    private void ValiderSelecteurs()
    {
        foreach (var selecteur in _arrivees) selecteur.Valider();
        foreach (var selecteur in _pausesDebut) selecteur.Valider();
        foreach (var selecteur in _pausesFin) selecteur.Valider();
        foreach (var selecteur in _departs) selecteur.Valider();
    }

    private void MarquerModifie(bool modifie)
    {
        _modifie = modifie;
        BoutonEnregistrer.IsEnabled = modifie && _coherent && !_suspendu;
        Title = modifie ? $"MelTime — {Profil.Identifiant} *" : $"MelTime — {Profil.Identifiant}";
    }

    // ------------------------------------------------------------------
    // Evenements
    // ------------------------------------------------------------------

    private void SurHeureModifiee(object? sender, EventArgs e)
    {
        if (_suspendu)
        {
            return;
        }

        MettreAJourTotaux();
        MarquerModifie(true);
    }

    private void Contrat_Modifie(object sender, SelectionChangedEventArgs e)
    {
        if (_suspendu)
        {
            return;
        }

        MettreAJourTotaux();
        MarquerModifie(true);
    }

    private void EffacerJour(int jour)
    {
        _suspendu = true;

        _arrivees[jour].Heure = null;
        _pausesDebut[jour].Heure = null;
        _pausesFin[jour].Heure = null;
        _departs[jour].Heure = null;

        _suspendu = false;

        MettreAJourTotaux();
        MarquerModifie(true);
    }

    private void ChoixAnnee_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suspendu || ChoixAnnee.SelectedItem is not int annee)
        {
            return;
        }

        var numero = ChoixSemaine.SelectedItem is int courant ? courant : 1;

        _suspendu = true;
        RemplirSemaines(annee, numero);
        _suspendu = false;

        _ = ChargerSemaineAsync(annee, numero);
    }

    private void ChoixSemaine_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suspendu || ChoixAnnee.SelectedItem is not int annee || ChoixSemaine.SelectedItem is not int numero)
        {
            return;
        }

        _ = ChargerSemaineAsync(annee, numero);
    }

    private async void BoutonPrecedent_Click(object sender, RoutedEventArgs e) => await NaviguerAsync(-1);

    private async void BoutonSuivant_Click(object sender, RoutedEventArgs e) => await NaviguerAsync(1);

    private async Task NaviguerAsync(int decalage)
    {
        var lundi = SemaineTravail.LundiDeLaSemaine(_semaine.AnneeIso, _semaine.Numero).AddDays(7 * decalage);
        var (anneeIso, numero) = SemaineTravail.SemaineIsoDe(lundi);

        await ChargerSemaineAsync(anneeIso, numero);
    }

    private async void BoutonAujourdhui_Click(object sender, RoutedEventArgs e)
    {
        var (anneeIso, numero) = SemaineTravail.SemaineIsoDe(DateOnly.FromDateTime(DateTime.Today));
        await ChargerSemaineAsync(anneeIso, numero);
    }

    private async void BoutonActualiser_Click(object sender, RoutedEventArgs e)
    {
        if (_modifie)
        {
            var confirmation = MessageBox.Show(this,
                "Des modifications ne sont pas enregistrées et seront perdues." + Environment.NewLine
                + "Recharger malgré tout ?",
                "MelTime", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }
        }

        await ChargerSemaineAsync(_semaine.AnneeIso, _semaine.Numero);
    }

    private async void BoutonEnregistrer_Click(object sender, RoutedEventArgs e)
    {
        ValiderSelecteurs();
        MettreAJourTotaux();

        if (!_coherent)
        {
            MessageBox.Show(this,
                "La feuille de temps contient une incohérence :" + Environment.NewLine
                + Environment.NewLine + TexteStatut.Text,
                "Enregistrement impossible", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            BoutonEnregistrer.IsEnabled = false;
            TexteStatut.Text = "Enregistrement…";

            var modele = ConstruireModele();
            _semaine = await _api.EnregistrerSemaineAsync(SemaineApi.Depuis(modele));

            AfficherSemaine();
            TexteStatut.Text = $"Enregistré — semaine {_semaine.Numero}.";
        }
        catch (UnauthorizedAccessException)
        {
            SignalerSessionExpiree();
        }
        catch (Exception ex)
        {
            MarquerModifie(true);
            TexteStatut.Text = "Enregistrement impossible";
            AfficherBandeau("L'enregistrement a échoué : " + ex.Message);
        }
    }

    // ------------------------------------------------------------------
    // Bandeau d'erreur intégré
    // ------------------------------------------------------------------

    private void AfficherBandeau(string message)
    {
        TexteBandeau.Text = message;
        BandeauErreur.Visibility = Visibility.Visible;
    }

    private void BoutonFermerBandeau_Click(object sender, RoutedEventArgs e) =>
        BandeauErreur.Visibility = Visibility.Collapsed;

    private async void BoutonDeconnexion_Click(object sender, RoutedEventArgs e)
    {
        await _api.DeconnecterAsync();
        _session.EffacerJeton();

        Application.Current.Shutdown();
    }

    private void SignalerSessionExpiree()
    {
        MessageBox.Show(this,
            "Votre session a expiré." + Environment.NewLine
            + "Le client va se fermer : relancez-le pour vous reconnecter.",
            "MelTime", MessageBoxButton.OK, MessageBoxImage.Warning);

        _session.EffacerJeton();
        Close();
    }

    private static TimeOnly? EnHeure(string? texte) =>
        string.IsNullOrWhiteSpace(texte)
            ? null
            : TimeOnly.TryParseExact(texte, SemaineApi.FormatHeure, CultureInfo.InvariantCulture, DateTimeStyles.None, out var heure)
                ? heure
                : null;
}
