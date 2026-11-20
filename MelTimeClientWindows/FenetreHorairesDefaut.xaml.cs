using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MelTime.Core.Agenda;
using MelTime.Core.Api;

namespace MelTime.ClientWindows;

/// <summary>
/// Saisie des horaires types : jusqu'a cinq modeles notes de A a E, et le
/// point de depart de leur rotation.
///
/// La semaine de depart porte le premier modele actif, la suivante le
/// deuxieme, et ainsi de suite en recommencant. Les modeles decoches sont
/// ignores : ils n'existent pas dans le cycle.
/// </summary>
public partial class FenetreHorairesDefaut : Window
{
    private const int NombreDeModeles = 5;

    private static readonly string[] NomsJours =
    {
        "Lundi", "Mardi", "Mercredi", "Jeudi", "Vendredi", "Samedi", "Dimanche"
    };

    private static readonly Brush FondLigneAlternee = new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x2B));

    private readonly ClientApi _api;

    private readonly Grid[] _grilles = new Grid[NombreDeModeles];
    private readonly CheckBox[] _casesActif = new CheckBox[NombreDeModeles];

    private readonly SelecteurHeure[][] _arrivees = new SelecteurHeure[NombreDeModeles][];
    private readonly SelecteurHeure[][] _pausesDebut = new SelecteurHeure[NombreDeModeles][];
    private readonly SelecteurHeure[][] _pausesFin = new SelecteurHeure[NombreDeModeles][];
    private readonly SelecteurHeure[][] _departs = new SelecteurHeure[NombreDeModeles][];

    public FenetreHorairesDefaut(ClientApi api)
    {
        InitializeComponent();

        _api = api;

        SourceInitialized += (_, _) => ThemeSombre.Appliquer(this);

        ConstruireOnglets();

        ConstruireChoixSemaine();

        ChoixAnnee.SelectionChanged += (_, _) => RemplirSemaines();
        ChoixSemaine.SelectionChanged += (_, _) => AfficherExplication();

        Loaded += async (_, _) => await ChargerAsync();
    }

    /// <summary>Liste des annees proposees : l'annee courante, et les suivantes.</summary>
    private void ConstruireChoixSemaine()
    {
        var courante = DateTime.Today.Year;

        for (var annee = courante - 1; annee <= courante + 5; annee++)
        {
            ChoixAnnee.Items.Add(annee);
        }

        ChoixAnnee.SelectedItem = courante;

        RemplirSemaines();
    }

    /// <summary>
    /// Remplit la liste des semaines pour l'annee choisie. La premiere entree
    /// ne porte aucune semaine : le cycle n'a alors pas de point de depart.
    /// </summary>
    private void RemplirSemaines()
    {
        if (ChoixAnnee.SelectedItem is not int annee)
        {
            return;
        }

        var choix = ChoixSemaine.SelectedItem as int?;

        ChoixSemaine.Items.Clear();
        ChoixSemaine.Items.Add("Aucune");

        for (var numero = 1; numero <= 52; numero++)
        {
            ChoixSemaine.Items.Add(numero);
        }

        ChoixSemaine.SelectedItem = choix is { } garde && garde >= 1 && garde <= 52 ? garde : "Aucune";

        AfficherExplication();
    }

    private static Brush Palette(string cle) =>
        Application.Current.TryFindResource(cle) as Brush ?? Brushes.Gray;

    /// <summary>Cree un onglet par modele, chacun avec ses sept journees.</summary>
    private void ConstruireOnglets()
    {
        for (var modele = 0; modele < NombreDeModeles; modele++)
        {
            var grille = new Grid();
            _grilles[modele] = grille;

            ConstruireGrille(modele, grille);

            _casesActif[modele] = new CheckBox
            {
                Content = "Ce modèle participe à la rotation",
                Margin = new Thickness(10, 10, 0, 10),
                Foreground = Palette("Texte"),
                IsChecked = modele == 0
            };

            var contenu = new Grid();
            contenu.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            contenu.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(grille, 0);
            contenu.Children.Add(grille);

            Grid.SetRow(_casesActif[modele], 1);
            contenu.Children.Add(_casesActif[modele]);

            var onglet = new TabItem
            {
                Header = ((char)('A' + modele)).ToString(),
                Content = contenu,
                Background = Palette("Surface"),
                Foreground = Palette("Texte"),
                Padding = new Thickness(14, 6, 14, 6)
            };

            Onglets.Items.Add(onglet);
        }
    }

    private void ConstruireGrille(int modele, Grid grille)
    {
        _arrivees[modele] = new SelecteurHeure[7];
        _pausesDebut[modele] = new SelecteurHeure[7];
        _pausesFin[modele] = new SelecteurHeure[7];
        _departs[modele] = new SelecteurHeure[7];

        grille.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });

        for (var index = 0; index < 4; index++)
        {
            grille.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var entetes = new[] { "Jour", "Arrivée", "Pause début", "Pause fin", "Départ" };

        grille.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (var colonne = 0; colonne < entetes.Length; colonne++)
        {
            var texte = new TextBlock
            {
                Text = entetes[colonne],
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = Palette("Texte"),
                VerticalAlignment = VerticalAlignment.Center
            };

            var entete = Cellule(texte, new Thickness(10, 9, 8, 9));
            entete.Background = Palette("SurfaceSurvol");

            Grid.SetRow(entete, 0);
            Grid.SetColumn(entete, colonne);
            grille.Children.Add(entete);
        }

        for (var jour = 0; jour < 7; jour++)
        {
            grille.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });

            var ligne = jour + 1;
            var fond = jour % 2 == 1 ? FondLigneAlternee : Brushes.Transparent;

            var etiquette = new TextBlock
            {
                Text = NomsJours[jour],
                FontWeight = FontWeights.SemiBold,
                Foreground = Palette("Texte"),
                VerticalAlignment = VerticalAlignment.Center
            };

            AjouterCellule(grille, ligne, 0, etiquette, new Thickness(10, 4, 8, 4), fond);

            _arrivees[modele][jour] = AjouterSelecteur(grille, ligne, 1, fond);
            _pausesDebut[modele][jour] = AjouterSelecteur(grille, ligne, 2, fond);
            _pausesFin[modele][jour] = AjouterSelecteur(grille, ligne, 3, fond);
            _departs[modele][jour] = AjouterSelecteur(grille, ligne, 4, fond);
        }
    }

    private SelecteurHeure AjouterSelecteur(Grid grille, int ligne, int colonne, Brush fond)
    {
        var selecteur = new SelecteurHeure
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Width = 112,
            Margin = new Thickness(0, 4, 0, 4)
        };

        AjouterCellule(grille, ligne, colonne, selecteur, new Thickness(8, 0, 8, 0), fond, HorizontalAlignment.Center);

        return selecteur;
    }

    private void AjouterCellule(
        Grid grille,
        int ligne,
        int colonne,
        UIElement contenu,
        Thickness marge,
        Brush fond,
        HorizontalAlignment alignement = HorizontalAlignment.Stretch)
    {
        if (contenu is FrameworkElement element)
        {
            element.Margin = marge;
            element.HorizontalAlignment = alignement;
        }

        var cellule = new Border
        {
            Background = fond,
            BorderBrush = Palette("Bordure"),
            BorderThickness = new Thickness(0, 0, 1, 1),
            Child = contenu
        };

        Grid.SetRow(cellule, ligne);
        Grid.SetColumn(cellule, colonne);
        grille.Children.Add(cellule);
    }

    private static Border Cellule(UIElement contenu, Thickness marge)
    {
        if (contenu is FrameworkElement element)
        {
            element.Margin = marge;
        }

        return new Border
        {
            BorderBrush = Palette("Bordure"),
            BorderThickness = new Thickness(0, 0, 1, 1),
            Child = contenu
        };
    }

    /// <summary>
    /// Rappelle la regle du cycle avec les modeles actuellement coches,
    /// pour que l'effet de la date soit lisible sans enregistrer.
    /// </summary>
    private void AfficherExplication()
    {
        var actifs = new List<string>();

        for (var modele = 0; modele < NombreDeModeles; modele++)
        {
            if (_casesActif[modele]?.IsChecked == true)
            {
                actifs.Add(((char)('A' + modele)).ToString());
            }
        }

        if (actifs.Count == 0)
        {
            TexteExplication.Text = "Aucun modèle actif : les semaines vides resteront vides.";
            return;
        }

        var cycle = string.Join(" → ", actifs) + " → " + actifs[0];

        TexteExplication.Text = SemaineDeDepart() is { } debut
            ? $"La semaine {debut.Numero} de {debut.Annee} (lundi {debut.Lundi:dd/MM/yyyy}) sera une semaine {actifs[0]}. Cycle : {cycle}."
            : $"Sans semaine de départ, toutes les semaines reçoivent le modèle {actifs[0]}.";
    }

    /// <summary>
    /// Lundi de la semaine choisie, ou null si la liste indique « Aucune ».
    /// </summary>
    private (int Annee, int Numero, DateOnly Lundi)? SemaineDeDepart()
    {
        if (ChoixAnnee.SelectedItem is not int annee || ChoixSemaine.SelectedItem is not int numero)
        {
            return null;
        }

        return (annee, numero, SemaineTravail.LundiDeLaSemaine(annee, numero));
    }

    private async Task ChargerAsync()
    {
        try
        {
            TexteStatut.Text = "Chargement…";

            var types = await _api.HorairesDefautAsync();

            if (DateOnly.TryParseExact(
                    types.DebutCycle ?? string.Empty,
                    SemaineApi.FormatDate,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var debut))
            {
                var (anneeIso, numero) = SemaineTravail.SemaineIsoDe(debut);

                ChoixAnnee.SelectedItem = anneeIso;

                if (ChoixAnnee.SelectedItem is null)
                {
                    ChoixAnnee.Items.Add(anneeIso);
                    ChoixAnnee.SelectedItem = anneeIso;
                }

                RemplirSemaines();
                ChoixSemaine.SelectedItem = numero;
            }

            foreach (var modeleRecu in types.Modeles)
            {
                if (modeleRecu.Index < 0 || modeleRecu.Index >= NombreDeModeles)
                {
                    continue;
                }

                _casesActif[modeleRecu.Index].IsChecked = modeleRecu.Actif;

                for (var jour = 0; jour < 7; jour++)
                {
                    var donnee = modeleRecu.Jours.FirstOrDefault(j => j.Index == jour);

                    _arrivees[modeleRecu.Index][jour].Heure = EnHeure(donnee?.Arrivee);
                    _pausesDebut[modeleRecu.Index][jour].Heure = EnHeure(donnee?.PauseDebut);
                    _pausesFin[modeleRecu.Index][jour].Heure = EnHeure(donnee?.PauseFin);
                    _departs[modeleRecu.Index][jour].Heure = EnHeure(donnee?.Depart);
                }
            }

            AfficherExplication();

            TexteStatut.Text = string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show(this,
                "Votre session a expiré. Fermez cette fenêtre et reconnectez-vous.",
                "MelTime", MessageBoxButton.OK, MessageBoxImage.Warning);
            Close();
        }
        catch (Exception ex)
        {
            TexteStatut.Text = "Chargement impossible : " + ex.Message;
        }
    }

    private async void BoutonEnregistrer_Click(object sender, RoutedEventArgs e)
    {
        var types = new HorairesDefautApi
        {
            DebutCycle = SemaineDeDepart() is { } debut
                ? debut.Lundi.ToString(SemaineApi.FormatDate, CultureInfo.InvariantCulture)
                : null
        };

        for (var modele = 0; modele < NombreDeModeles; modele++)
        {
            var entree = new ModeleApi
            {
                Index = modele,
                Actif = _casesActif[modele].IsChecked == true
            };

            for (var jour = 0; jour < 7; jour++)
            {
                entree.Jours.Add(new JourDefautApi
                {
                    Index = jour,
                    Arrivee = EnTexte(_arrivees[modele][jour].Heure),
                    PauseDebut = EnTexte(_pausesDebut[modele][jour].Heure),
                    PauseFin = EnTexte(_pausesFin[modele][jour].Heure),
                    Depart = EnTexte(_departs[modele][jour].Heure)
                });
            }

            types.Modeles.Add(entree);
        }

        try
        {
            BoutonEnregistrer.IsEnabled = false;
            TexteStatut.Text = "Enregistrement…";

            await _api.EnregistrerHorairesDefautAsync(types);

            DialogResult = true;
            Close();
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show(this,
                "Votre session a expiré. Fermez cette fenêtre et reconnectez-vous.",
                "MelTime", MessageBoxButton.OK, MessageBoxImage.Warning);
            Close();
        }
        catch (Exception ex)
        {
            TexteStatut.Text = "Enregistrement impossible : " + ex.Message;
        }
        finally
        {
            BoutonEnregistrer.IsEnabled = true;
        }
    }

    private void BoutonAnnuler_Click(object sender, RoutedEventArgs e) => Close();

    private static TimeOnly? EnHeure(string? texte) =>
        string.IsNullOrWhiteSpace(texte)
            ? null
            : TimeOnly.TryParseExact(texte, SemaineApi.FormatHeure, CultureInfo.InvariantCulture, DateTimeStyles.None, out var heure)
                ? heure
                : null;

    private static string? EnTexte(TimeOnly? heure) =>
        heure?.ToString(SemaineApi.FormatHeure, CultureInfo.InvariantCulture);
}
