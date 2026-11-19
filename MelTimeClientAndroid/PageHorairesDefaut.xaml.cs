using System.Globalization;
using MelTime.Core.Api;

namespace MelTimeClientAndroid;

/// <summary>
/// Modele d'horaires du client : sept journees types, qui prereplissent
/// ensuite toutes ses semaines jamais saisies.
///
/// Sur un telephone, les sept journees sont empilees dans une liste
/// defilante : une roue de selection par horaire, directement accessible,
/// plutot qu'un panneau a ouvrir jour par jour.
/// </summary>
public partial class PageHorairesDefaut : ContentPage
{
    private static readonly string[] NomsJours =
    {
        "Lundi", "Mardi", "Mercredi", "Jeudi", "Vendredi", "Samedi", "Dimanche"
    };

    private readonly ClientApi _api;
    private readonly TimePicker[][] _champs = new TimePicker[7][];
    private readonly CheckBox[] _cases = new CheckBox[7];

    public PageHorairesDefaut(ClientApi api)
    {
        InitializeComponent();

        _api = api;

        ConstruireJours();

        Loaded += async (_, _) => await ChargerAsync();
    }

    /// <summary>Sept journees empilees : le portrait est plus lisible.</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        OrientationEcran.Portrait();
    }

    private void ConstruireJours()
    {
        for (var jour = 0; jour < 7; jour++)
        {
            var cadre = new Border
            {
                BackgroundColor = Color.FromArgb("#262626"),
                Stroke = Color.FromArgb("#3D3D3D"),
                StrokeThickness = 1,
                Padding = 12
            };

            var contenu = new VerticalStackLayout { Spacing = 8 };

            var entete = new HorizontalStackLayout { Spacing = 8 };

            entete.Children.Add(new Label
            {
                Text = NomsJours[jour],
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#F2F2F2"),
                FontSize = 15,
                VerticalOptions = LayoutOptions.Center
            });

            _cases[jour] = new CheckBox();
            entete.Children.Add(_cases[jour]);

            entete.Children.Add(new Label
            {
                Text = "travaillé",
                FontSize = 12,
                TextColor = Color.FromArgb("#A0A0A0"),
                VerticalOptions = LayoutOptions.Center
            });

            contenu.Children.Add(entete);

            // Quatre selecteurs sur deux lignes de deux, pour rester lisibles.
            var grille = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star)
                },
                RowDefinitions =
                {
                    new RowDefinition(GridLength.Auto),
                    new RowDefinition(GridLength.Auto)
                },
                ColumnSpacing = 8,
                RowSpacing = 4
            };

            var libelles = new[] { "Arrivée", "Pause début", "Pause fin", "Départ" };
            _champs[jour] = new TimePicker[4];

            for (var index = 0; index < 4; index++)
            {
                var cellule = new VerticalStackLayout { Spacing = 0 };

                cellule.Children.Add(new Label
                {
                    Text = libelles[index],
                    FontSize = 10,
                    TextColor = Color.FromArgb("#A0A0A0")
                });

                _champs[jour][index] = new TimePicker
                {
                    Format = "HH:mm",
                    TextColor = Color.FromArgb("#F2F2F2"),
                    Time = new TimeSpan(8 + index, 0, 0)
                };

                cellule.Children.Add(_champs[jour][index]);

                Grid.SetRow(cellule, index / 2);
                Grid.SetColumn(cellule, index % 2);
                grille.Children.Add(cellule);
            }

            contenu.Children.Add(grille);
            cadre.Content = contenu;

            var indexJour = jour;
            _cases[jour].CheckedChanged += (_, e) =>
            {
                _champs[indexJour][0].IsEnabled = e.Value;
                _champs[indexJour][1].IsEnabled = e.Value;
                _champs[indexJour][2].IsEnabled = e.Value;
                _champs[indexJour][3].IsEnabled = e.Value;
            };

            ListeJours.Children.Add(cadre);
        }
    }

    private async Task ChargerAsync()
    {
        try
        {
            TexteEtat.Text = "Chargement…";

            var modele = await _api.HorairesDefautAsync();

            for (var jour = 0; jour < 7; jour++)
            {
                var donnee = modele.Jours.FirstOrDefault(j => j.Index == jour);

                var arrivee = EnHeure(donnee?.Arrivee);
                var depart = EnHeure(donnee?.Depart);
                var travaille = arrivee is not null;

                _cases[jour].IsChecked = travaille;

                _champs[jour][0].Time = (arrivee ?? new TimeOnly(8, 0)).ToTimeSpan();
                _champs[jour][1].Time = (EnHeure(donnee?.PauseDebut) ?? new TimeOnly(12, 0)).ToTimeSpan();
                _champs[jour][2].Time = (EnHeure(donnee?.PauseFin) ?? new TimeOnly(13, 0)).ToTimeSpan();
                _champs[jour][3].Time = (depart ?? new TimeOnly(17, 0)).ToTimeSpan();
            }

            TexteEtat.Text = string.Empty;
        }
        catch (Exception ex)
        {
            TexteEtat.Text = "Chargement impossible : " + ex.Message;
        }
    }

    private async void Enregistrer_Clicked(object? sender, EventArgs e)
    {
        try
        {
            TexteEtat.Text = "Enregistrement…";

            var modele = new HorairesDefautApi();

            for (var jour = 0; jour < 7; jour++)
            {
                if (_cases[jour].IsChecked != true)
                {
                    modele.Jours.Add(new JourDefautApi { Index = jour });
                    continue;
                }

                modele.Jours.Add(new JourDefautApi
                {
                    Index = jour,
                    Arrivee = EnTexte(_champs[jour][0].Time),
                    PauseDebut = EnTexte(_champs[jour][1].Time),
                    PauseFin = EnTexte(_champs[jour][2].Time),
                    Depart = EnTexte(_champs[jour][3].Time)
                });
            }

            await _api.EnregistrerHorairesDefautAsync(modele);

            TexteEtat.Text = "Enregistré.";
        }
        catch (Exception ex)
        {
            TexteEtat.Text = "Enregistrement impossible : " + ex.Message;
        }
    }

    private async void Fermer_Clicked(object? sender, EventArgs e) => await Navigation.PopModalAsync();

    private static TimeOnly? EnHeure(string? texte) =>
        string.IsNullOrWhiteSpace(texte)
            ? null
            : TimeOnly.TryParseExact(texte, SemaineApi.FormatHeure, CultureInfo.InvariantCulture, DateTimeStyles.None, out var heure)
                ? heure
                : null;

    private static string EnTexte(TimeSpan valeur) =>
        valeur.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
}
