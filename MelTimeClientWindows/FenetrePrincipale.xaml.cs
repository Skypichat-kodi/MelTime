using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    /// <summary>
    /// Jour ferie : rouge sombre mais franc.
    /// Sur un fond a #1B1B1B, un rouge trop desature ne se distingue pas.
    /// </summary>
    private static readonly Brush FondFerie = new SolidColorBrush(Color.FromRgb(0x5A, 0x2B, 0x2B));

    /// <summary>
    /// Samedi et dimanche : vert sapin. Teinte froide, nettement distincte
    /// du rouge des feries, et assez soutenue pour se voir sur fond noir.
    /// </summary>
    private static readonly Brush FondWeekend = new SolidColorBrush(Color.FromRgb(0x2A, 0x52, 0x38));

    /// <summary>Cases de chaque ligne, conservees pour pouvoir les recolorier
    /// a chaque changement de semaine (les feries dependent de l'annee affichee).</summary>
    private readonly List<Border>[] _cellulesJour = Enumerable.Range(0, 7).Select(_ => new List<Border>()).ToArray();

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

    /// <summary>Vrai lorsque la fenetre se ferme pour revenir a la connexion,
    /// et non pour quitter l'application.</summary>
    private bool _retourALaConnexion;

    /// <summary>Reglages personnels : salaires, et drapeau pour eviter les enregistrements en boucle.</summary>
    private ReglagesApi _reglages = new();
    private bool _chargementReglages;
    private bool _salairesModifies;

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

        Loaded += async (_, _) =>
        {
            await ChargerSemaineCouranteAsync();
            await ChargerReglagesAsync();
        };

        Closed += (_, _) =>
        {
            // Fermer la feuille de temps quitte l'application, sauf si l'on
            // se deconnecte : dans ce cas la fenetre de connexion reprend.
            if (!_retourALaConnexion)
            {
                Application.Current.Shutdown();
            }
        };
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
                Foreground = estTravaille ? Palette("Accent") : Palette("Texte"),
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

        // ligne 1 = lundi (index 0) : on memorise pour le recoloriage.
        _cellulesJour[ligne - 1].Add(cellule);
    }

    private static Border Cellule(UIElement contenu, Thickness marge)
    {
        // La marge passe sur le contenu, et non sur le Border : le trait de fin
        // de mois doit pouvoir couvrir toute la largeur de la cellule.
        if (contenu is FrameworkElement element)
        {
            element.Margin = marge;
        }

        var soulignement = new System.Windows.Shapes.Rectangle
        {
            Height = 3,
            VerticalAlignment = VerticalAlignment.Bottom,
            Fill = Brushes.Transparent
        };

        var grille = new Grid();
        grille.Children.Add(contenu);
        grille.Children.Add(soulignement);

        return new Border
        {
            BorderBrush = Palette("Bordure"),
            BorderThickness = new Thickness(0, 0, 1, 1),
            Child = grille,
            Tag = soulignement
        };
    }

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
            await ChargerTotalMoisAsync();
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
        // try/finally indispensable : sans lui, la moindre exception laisserait
        // _suspendu a true, et plus aucune modification ne reactiverait le bouton.
        _suspendu = true;

        try
        {
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
        }
        finally
        {
            _suspendu = false;
        }

        MettreAJourTotaux();
        ColorerJours();
        AfficherPeriode();
        MarquerModifie(false);
    }

    /// <summary>
    /// Colore le fond de chaque ligne selon la nature du jour :
    /// ferie, week-end, ou simple alternance. Appele a chaque affichage
    /// de semaine, car les feries dependent de l'annee.
    /// </summary>
    private void ColorerJours()
    {
        for (var jour = 0; jour < 7; jour++)
        {
            // Le caractere ferie vient desormais du serveur : plus aucun calcul
            // local, donc plus rien a reimplementer dans un futur client Android.
            var donnee = jour < _semaine.Jours.Count ? _semaine.Jours[jour] : null;
            var ferie = donnee?.Ferie ?? false;

            // Dernier jour du mois : le lendemain change de mois. On le souligne
            // d'un trait epais, ce qui rend lisible une semaine a cheval sur
            // deux mois (le 30 novembre suivi du 1er decembre, par exemple).
            var dernierDuMois = false;

            if (donnee is not null
                && DateOnly.TryParseExact(donnee.Date, SemaineApi.FormatDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                dernierDuMois = date.AddDays(1).Month != date.Month;
            }

            // Le ferie prime sur le week-end : le 25 decembre un samedi
            // reste signale comme ferie, pas comme simple samedi.
            Brush fond;

            if (ferie)
            {
                fond = FondFerie;
            }
            else if (jour >= 5)
            {
                fond = FondWeekend;
            }
            else
            {
                fond = jour % 2 == 1 ? FondLigneAlternee : Brushes.Transparent;
            }

            foreach (var cellule in _cellulesJour[jour])
            {
                cellule.Background = fond;

                // Les traits de la cellule restent gris : seul le soulignement
                // de fin de mois change de couleur. BorderBrush, lui, s'applique
                // a tous les cotes du Border et deborderait sur la droite.
                cellule.BorderBrush = Palette("Bordure");
                cellule.BorderThickness = new Thickness(0, 0, 1, 1);

                if (cellule.Tag is System.Windows.Shapes.Rectangle trait)
                {
                    trait.Fill = dernierDuMois ? Palette("Accent") : Brushes.Transparent;
                }
            }
        }
    }

    /// <summary>
    /// Demande au service le total du mois auquel appartient la semaine affichee.
    /// On se base sur la date du lundi : une semaine a cheval sur deux mois
    /// est donc rattachee au mois de son premier jour.
    /// </summary>
    private async Task ChargerTotalMoisAsync()
    {
        var premier = _semaine.Jours.FirstOrDefault();

        if (premier is null
            || !DateOnly.TryParseExact(premier.Date, SemaineApi.FormatDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            TexteTotalMois.Text = "—";
            return;
        }

        try
        {
            var total = await _api.TotalMoisAsync(date.Year, date.Month);
            TexteTotalMois.Text = total.TotalTravaille;
        }
        catch (Exception)
        {
            // Le total mensuel est une information annexe : son echec ne doit
            // pas empecher la feuille de temps de s'afficher.
            TexteTotalMois.Text = "—";
        }
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

        try
        {
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
        }
        finally
        {
            _suspendu = etait;
        }
    }

    private void MettreAJourTotaux()
    {
        if (_semaine.AnneeIso <= 0 || _semaine.Numero <= 0)
        {
            return;
        }

        var modele = ConstruireModele();

        TexteTotal.Text = Durees.Formater(modele.TotalTravaille);
        TexteSupplementaires.Text = Durees.Formater(modele.HeuresSupplementaires());

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
        MettreAJourEtatModifie();
    }

    /// <summary>
    /// Le bouton Enregistrer couvre deux choses : la feuille de temps et les
    /// salaires. Il s'active des que l'une ou l'autre a change, pour que
    /// l'utilisateur garde la main sur ce qui part sur le serveur.
    /// </summary>
    private void MettreAJourEtatModifie()
    {
        var enAttente = _modifie || _salairesModifies;

        BoutonEnregistrer.IsEnabled = enAttente && _coherent && !_suspendu;
        Title = enAttente
            ? $"MelTime — {Profil.Identifiant} *"
            : $"MelTime — {Profil.Identifiant}";
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

        try
        {
            _arrivees[jour].Heure = null;
            _pausesDebut[jour].Heure = null;
            _pausesFin[jour].Heure = null;
            _departs[jour].Heure = null;
        }
        finally
        {
            _suspendu = false;
        }

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

    private async void BoutonHorairesDefaut_Click(object sender, RoutedEventArgs e)
    {
        // Les horaires sont propres au client connecte : le modele enregistre
        // ici prereplira ses propres semaines vides, et personne d'autre.
        var fenetre = new FenetreHorairesDefaut(_api)
        {
            Owner = this
        };

        if (fenetre.ShowDialog() != true)
        {
            return;
        }

        // Le modele vient de changer : on recharge pour que la semaine affichee
        // en profite immediatement, sans avoir a cliquer sur « Recharger ».
        // Mais jamais au prix d'une saisie en cours : on demande alors.
        if (_modifie)
        {
            var confirmation = MessageBox.Show(this,
                "Vos horaires par défaut ont été enregistrés." + Environment.NewLine + Environment.NewLine
                + "Recharger la semaine pour les appliquer ?" + Environment.NewLine
                + "Vos modifications non enregistrées seront perdues.",
                "MelTime", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }
        }

        await ChargerSemaineAsync(_semaine.AnneeIso, _semaine.Numero);
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

            var semaineEnregistree = _modifie;

            // La feuille de temps n'est envoyee que si elle a change :
            // enregistrer les seuls salaires ne doit pas reecrire la semaine.
            if (_modifie)
            {
                var modele = ConstruireModele();
                _semaine = await _api.EnregistrerSemaineAsync(SemaineApi.Depuis(modele));

                AfficherSemaine();
                await ChargerTotalMoisAsync();
            }

            if (_salairesModifies)
            {
                await EnregistrerSalairesAsync();
            }

            MarquerModifie(false);

            // La confirmation s'affiche en haut, dans la barre d'etat :
            // inutile d'en ajouter une seconde a cote du panneau.
            TexteStatut.Text = semaineEnregistree
                ? $"Enregistré — semaine {_semaine.Numero}."
                : "Salaires enregistrés.";
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
    // Salaire et prix de l'heure
    // ------------------------------------------------------------------

    private async Task ChargerReglagesAsync()
    {
        try
        {
            _chargementReglages = true;

            _reglages = await _api.ReglagesAsync();

            ChampSalaireBrut.Text = FormaterMontant(_reglages.SalaireBrut);
            ChampSalaireNet.Text = FormaterMontant(_reglages.SalaireNet);
        }
        catch (Exception)
        {
            // Les salaires sont une commodite : leur absence n'empeche rien.
        }
        finally
        {
            _chargementReglages = false;
            CalculerPrixHeure();
        }
    }

    /// <summary>Envoie les salaires saisis au service.</summary>
    private async Task EnregistrerSalairesAsync()
    {
        _reglages = new ReglagesApi
        {
            SalaireBrut = LireMontant(ChampSalaireBrut.Text),
            SalaireNet = LireMontant(ChampSalaireNet.Text)
        };

        await _api.EnregistrerReglagesAsync(_reglages);

        _salairesModifies = false;
    }

    /// <summary>
    /// Toute frappe dans un salaire rend le bouton Enregistrer actif.
    /// Rien ne part sur le serveur sans que l'utilisateur l'ait decide.
    /// </summary>
    private void ChampSalaire_TextChanged(object sender, TextChangedEventArgs e)
    {
        CalculerPrixHeure();

        if (_chargementReglages || _suspendu)
        {
            return;
        }

        if (!_salairesModifies)
        {
            _salairesModifies = true;
            MettreAJourEtatModifie();
        }
    }

    /// <summary>
    /// Prix de l'heure = salaire mensuel divise par les heures mensualisees
    /// du contrat (contrat hebdomadaire x 52 / 12). Ce prix reste stable
    /// d'un mois a l'autre, comme sur un bulletin de paie.
    /// </summary>
    private void CalculerPrixHeure()
    {
        var heuresMensuelles = Profil.ContratHebdomadaireMinutes / 60.0 * 52.0 / 12.0;
        var brut = LireMontant(ChampSalaireBrut.Text);
        var net = LireMontant(ChampSalaireNet.Text);

        if (heuresMensuelles <= 0)
        {
            TextePrixBrut.Text = "—";
            TextePrixNet.Text = "—";
            TexteCharges.Text = "—";
            return;
        }

        TextePrixBrut.Text = brut is > 0
            ? $"{(decimal)(brut.Value / (decimal)heuresMensuelles):N2} €"
            : "—";

        TextePrixNet.Text = net is > 0
            ? $"{(decimal)(net.Value / (decimal)heuresMensuelles):N2} €"
            : "—";

        // Charges : simple difference entre le brut et le net saisis.
        TexteCharges.Text = brut is > 0 && net is > 0
            ? $"{brut.Value - net.Value:N2} €"
            : "—";
    }

    private static decimal? LireMontant(string texte)
    {
        var propre = texte.Replace(" ", string.Empty).Replace(",", ".");

        return decimal.TryParse(propre, NumberStyles.Any, CultureInfo.InvariantCulture, out var valeur)
            ? valeur
            : null;
    }

    private static string FormaterMontant(decimal? montant) =>
        montant is null ? string.Empty : montant.Value.ToString("0.00", CultureInfo.InvariantCulture);

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

    // ------------------------------------------------------------------
    // Deplacement et boutons de fenetre
    // ------------------------------------------------------------------

    /// <summary>
    /// L'en-tete fait office de barre de titre : glisser deplace la fenetre,
    /// double-cliquer bascule entre taille normale et plein ecran.
    /// Les controles qu'il contient absorbent leurs propres clics, la saisie
    /// n'est donc jamais perturbee.
    /// </summary>
    private void EnTete_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;

            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void BoutonReduire_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void BoutonFermer_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void BoutonDeconnexion_Click(object sender, RoutedEventArgs e)
    {
        await _api.DeconnecterAsync();
        _session.EffacerJeton();

        // On ne quitte plus l'application : on revient a l'ecran de connexion,
        // ou l'utilisateur peut changer de compte ou d'adresse de serveur.
        _retourALaConnexion = true;
        Close();

        if (Application.Current is App application)
        {
            await application.DemarrerCycleAsync();
        }
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
