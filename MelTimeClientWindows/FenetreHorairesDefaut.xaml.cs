using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MelTime.Core.Api;

namespace MelTime.ClientWindows;

/// <summary>
/// Saisie du modele d'horaires : sept journees types, qui prereplissent
/// ensuite toutes les semaines jamais saisies.
/// </summary>
public partial class FenetreHorairesDefaut : Window
{
    private static readonly string[] NomsJours =
    {
        "Lundi", "Mardi", "Mercredi", "Jeudi", "Vendredi", "Samedi", "Dimanche"
    };

    private static readonly Brush FondLigneAlternee = new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x2B));

    private readonly ClientApi _api;

    private readonly SelecteurHeure[] _arrivees = new SelecteurHeure[7];
    private readonly SelecteurHeure[] _pausesDebut = new SelecteurHeure[7];
    private readonly SelecteurHeure[] _pausesFin = new SelecteurHeure[7];
    private readonly SelecteurHeure[] _departs = new SelecteurHeure[7];

    public FenetreHorairesDefaut(ClientApi api)
    {
        InitializeComponent();

        _api = api;

        SourceInitialized += (_, _) => ThemeSombre.Appliquer(this);

        ConstruireGrille();

        Loaded += async (_, _) => await ChargerAsync();
    }

    private static Brush Palette(string cle) =>
        Application.Current.TryFindResource(cle) as Brush ?? Brushes.Gray;

    private void ConstruireGrille()
    {
        GrilleJours.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });

        for (var index = 0; index < 4; index++)
        {
            GrilleJours.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var entetes = new[] { "Jour", "Arrivée", "Pause début", "Pause fin", "Départ" };

        GrilleJours.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

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
            GrilleJours.Children.Add(entete);
        }

        for (var jour = 0; jour < 7; jour++)
        {
            GrilleJours.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });

            var ligne = jour + 1;
            var fond = jour % 2 == 1 ? FondLigneAlternee : Brushes.Transparent;

            var etiquette = new TextBlock
            {
                Text = NomsJours[jour],
                FontWeight = FontWeights.SemiBold,
                Foreground = Palette("Texte"),
                VerticalAlignment = VerticalAlignment.Center
            };

            AjouterCellule(ligne, 0, etiquette, new Thickness(10, 4, 8, 4), fond);

            _arrivees[jour] = AjouterSelecteur(ligne, 1, fond);
            _pausesDebut[jour] = AjouterSelecteur(ligne, 2, fond);
            _pausesFin[jour] = AjouterSelecteur(ligne, 3, fond);
            _departs[jour] = AjouterSelecteur(ligne, 4, fond);
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
        GrilleJours.Children.Add(cellule);
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

    private async Task ChargerAsync()
    {
        try
        {
            TexteStatut.Text = "Chargement…";

            var modele = await _api.HorairesDefautAsync();

            for (var jour = 0; jour < 7; jour++)
            {
                var donnee = modele.Jours.FirstOrDefault(j => j.Index == jour);

                _arrivees[jour].Heure = EnHeure(donnee?.Arrivee);
                _pausesDebut[jour].Heure = EnHeure(donnee?.PauseDebut);
                _pausesFin[jour].Heure = EnHeure(donnee?.PauseFin);
                _departs[jour].Heure = EnHeure(donnee?.Depart);
            }

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
        var modele = new HorairesDefautApi();

        for (var jour = 0; jour < 7; jour++)
        {
            modele.Jours.Add(new JourDefautApi
            {
                Index = jour,
                Arrivee = EnTexte(_arrivees[jour].Heure),
                PauseDebut = EnTexte(_pausesDebut[jour].Heure),
                PauseFin = EnTexte(_pausesFin[jour].Heure),
                Depart = EnTexte(_departs[jour].Heure)
            });
        }

        try
        {
            BoutonEnregistrer.IsEnabled = false;
            TexteStatut.Text = "Enregistrement…";

            await _api.EnregistrerHorairesDefautAsync(modele);

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
