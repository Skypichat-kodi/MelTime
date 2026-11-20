using System.Globalization;
using MelTime.Core.Agenda;
using MelTime.Core.Api;

namespace MelTimeClientAndroid;

/// <summary>
/// Calendriers par defaut : jusqu'a cinq modeles d'horaires, notes de A a E,
/// qui alternent selon la semaine.
///
/// La semaine de depart porte le premier modele actif, la suivante le
/// deuxieme, et ainsi de suite en recommencant. Les modeles decoches sont
/// ignores : ils n'existent pas dans le cycle.
/// </summary>
public partial class PageHorairesDefaut : ContentPage
{
    private const int NombreDeModeles = 5;

    private static readonly string[] NomsJours =
    {
        "Lundi", "Mardi", "Mercredi", "Jeudi", "Vendredi", "Samedi", "Dimanche"
    };

    private readonly ClientApi _api;

    private readonly TimePicker[][][] _champs = new TimePicker[NombreDeModeles][][];
    private readonly CheckBox[][] _cases = new CheckBox[NombreDeModeles][];
    private readonly CheckBox[][] _sansPause = new CheckBox[NombreDeModeles][];
    private readonly VerticalStackLayout[] _conteneurs = new VerticalStackLayout[NombreDeModeles];
    private readonly Button[] _lettres = new Button[NombreDeModeles];
    private readonly CheckBox[] _actifs = new CheckBox[NombreDeModeles];

    private int _modeleCourant;

    public PageHorairesDefaut(ClientApi api)
    {
        InitializeComponent();

        _api = api;

        ConstruireChoixSemaine();
        ConstruireLettres();
        ConstruireModeles();

        ChoisirModele(0);

        Loaded += async (_, _) => await ChargerAsync();
    }

    /// <summary>Les cinq modeles s'editent en portrait : sept journees empilees.</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        OrientationEcran.Portrait();
    }

    // ------------------------------------------------------------------
    // Construction
    // ------------------------------------------------------------------

    private void ConstruireChoixSemaine()
    {
        var courante = DateTime.Today.Year;

        for (var annee = courante - 1; annee <= courante + 5; annee++)
        {
            ChoixAnnee.Items.Add(annee.ToString());
        }

        ChoixAnnee.SelectedIndex = 1;
        ChoixAnnee.SelectedIndexChanged += (_, _) => RemplirSemaines();

        RemplirSemaines();
    }

    /// <summary>
    /// Remplit la liste des semaines de l'annee choisie. La premiere entree
    /// ne porte aucune semaine : le cycle n'a alors pas de point de depart.
    /// </summary>
    private void RemplirSemaines()
    {
        var annee = AnneeChoisie();

        var precedent = ChoixSemaine.SelectedIndex > 0 ? ChoixSemaine.SelectedIndex : 0;

        ChoixSemaine.Items.Clear();
        ChoixSemaine.Items.Add("Aucune");

        // Certaines annees ISO comptent 53 semaines : la liste doit les offrir.
        var semaines = SemaineTravail.SemainesDansLAnnee(annee);

        for (var numero = 1; numero <= semaines; numero++)
        {
            ChoixSemaine.Items.Add($"Semaine {numero}");
        }

        ChoixSemaine.SelectedIndex = Math.Min(precedent, semaines);
    }

    private int AnneeChoisie() =>
        ChoixAnnee.SelectedIndex >= 0 && int.TryParse(ChoixAnnee.Items[ChoixAnnee.SelectedIndex], out var annee)
            ? annee
            : DateTime.Today.Year;

    private void ConstruireLettres()
    {
        for (var modele = 0; modele < NombreDeModeles; modele++)
        {
            var index = modele;

            _lettres[modele] = new Button
            {
                Text = ((char)('A' + modele)).ToString(),
                WidthRequest = 46,
                HeightRequest = 36,
                FontAttributes = FontAttributes.Bold,
                BackgroundColor = Color.FromArgb("#262626"),
                TextColor = Color.FromArgb("#A0A0A0")
            };

            _lettres[modele].Clicked += (_, _) => ChoisirModele(index);

            ZoneLettres.Children.Add(_lettres[modele]);
        }
    }

    private void ConstruireModeles()
    {
        for (var modele = 0; modele < NombreDeModeles; modele++)
        {
            _conteneurs[modele] = new VerticalStackLayout { Spacing = 10, IsVisible = false };
            _actifs[modele] = new CheckBox();

            ConstruireJours(modele, _conteneurs[modele]);

            ZoneJours.Children.Add(_conteneurs[modele]);
        }
    }

    /// <summary>Affiche le modele demande et met les lettres en evidence.</summary>
    private void ChoisirModele(int modele)
    {
        _modeleCourant = modele;

        for (var index = 0; index < NombreDeModeles; index++)
        {
            _conteneurs[index].IsVisible = index == modele;

            _lettres[index].BackgroundColor = index == modele
                ? Color.FromArgb("#0078D4")
                : Color.FromArgb("#262626");

            _lettres[index].TextColor = index == modele
                ? Colors.White
                : Color.FromArgb("#A0A0A0");
        }

        CaseActif.IsChecked = _actifs[modele].IsChecked;
    }

    private void ConstruireJours(int modele, VerticalStackLayout parent)
    {
        _champs[modele] = new TimePicker[7][];
        _cases[modele] = new CheckBox[7];
        _sansPause[modele] = new CheckBox[7];

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

            _cases[modele][jour] = new CheckBox { Color = Color.FromArgb("#0078D4") };
            entete.Children.Add(_cases[modele][jour]);

            entete.Children.Add(new Label
            {
                Text = "travaillé",
                FontSize = 12,
                TextColor = Color.FromArgb("#A0A0A0"),
                VerticalOptions = LayoutOptions.Center
            });

            _sansPause[modele][jour] = new CheckBox { Margin = new Thickness(18, 0, 0, 0) };
            entete.Children.Add(_sansPause[modele][jour]);

            entete.Children.Add(new Label
            {
                Text = "sans pause",
                FontSize = 12,
                TextColor = Color.FromArgb("#A0A0A0"),
                VerticalOptions = LayoutOptions.Center
            });

            contenu.Children.Add(entete);

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
            _champs[modele][jour] = new TimePicker[4];

            for (var index = 0; index < 4; index++)
            {
                var cellule = new VerticalStackLayout { Spacing = 0 };

                cellule.Children.Add(new Label
                {
                    Text = libelles[index],
                    FontSize = 10,
                    TextColor = Color.FromArgb("#A0A0A0")
                });

                _champs[modele][jour][index] = new TimePicker
                {
                    Format = "HH:mm",
                    TextColor = Color.FromArgb("#F2F2F2"),
                    Time = new TimeSpan(8 + index, 0, 0)
                };

                cellule.Children.Add(_champs[modele][jour][index]);

                Grid.SetRow(cellule, index / 2);
                Grid.SetColumn(cellule, index % 2);
                grille.Children.Add(cellule);
            }

            contenu.Children.Add(grille);
            cadre.Content = contenu;

            var indexJour = jour;

            _cases[modele][jour].CheckedChanged += (_, e) =>
            {
                var pausesActives = e.Value && _sansPause[modele][indexJour].IsChecked != true;

                _champs[modele][indexJour][0].IsEnabled = e.Value;
                _champs[modele][indexJour][3].IsEnabled = e.Value;
                _sansPause[modele][indexJour].IsEnabled = e.Value;
                _champs[modele][indexJour][1].IsEnabled = pausesActives;
                _champs[modele][indexJour][2].IsEnabled = pausesActives;
            };

            _sansPause[modele][jour].CheckedChanged += (_, e) =>
            {
                var actif = _cases[modele][indexJour].IsChecked == true && !e.Value;

                _champs[modele][indexJour][1].IsEnabled = actif;
                _champs[modele][indexJour][2].IsEnabled = actif;

                if (e.Value)
                {
                    _champs[modele][indexJour][1].Time = null;
                    _champs[modele][indexJour][2].Time = null;
                }
                else
                {
                    _champs[modele][indexJour][1].Time ??= new TimeSpan(12, 0, 0);
                    _champs[modele][indexJour][2].Time ??= new TimeSpan(13, 0, 0);
                }
            };

            parent.Children.Add(cadre);
        }
    }

    private void CaseActif_Changed(object? sender, CheckedChangedEventArgs e)
    {
        _actifs[_modeleCourant].IsChecked = e.Value;
    }

    // ------------------------------------------------------------------
    // Echanges avec le service
    // ------------------------------------------------------------------

    private async Task ChargerAsync()
    {
        try
        {
            TexteEtat.Text = "Chargement…";

            var types = await _api.HorairesDefautAsync();

            if (DateOnly.TryParseExact(
                    types.DebutCycle ?? string.Empty,
                    SemaineApi.FormatDate,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var debut))
            {
                var (anneeIso, numero) = SemaineTravail.SemaineIsoDe(debut);

                var indexAnnee = ChoixAnnee.Items.IndexOf(anneeIso.ToString());

                if (indexAnnee < 0)
                {
                    ChoixAnnee.Items.Add(anneeIso.ToString());
                    indexAnnee = ChoixAnnee.Items.Count - 1;
                }

                ChoixAnnee.SelectedIndex = indexAnnee;
                ChoixSemaine.SelectedIndex = numero;
            }

            foreach (var modeleRecu in types.Modeles)
            {
                if (modeleRecu.Index < 0 || modeleRecu.Index >= NombreDeModeles)
                {
                    continue;
                }

                var modele = modeleRecu.Index;

                _actifs[modele].IsChecked = modeleRecu.Actif;

                for (var jour = 0; jour < 7; jour++)
                {
                    var donnee = modeleRecu.Jours.FirstOrDefault(j => j.Index == jour);

                    var arrivee = EnHeure(donnee?.Arrivee);
                    var depart = EnHeure(donnee?.Depart);
                    var pauseDebut = EnHeure(donnee?.PauseDebut);
                    var pauseFin = EnHeure(donnee?.PauseFin);

                    _cases[modele][jour].IsChecked = arrivee is not null;
                    _sansPause[modele][jour].IsChecked = pauseDebut is null || pauseFin is null;

                    _champs[modele][jour][0].Time = (arrivee ?? new TimeOnly(8, 0)).ToTimeSpan();
                    _champs[modele][jour][1].Time = pauseDebut?.ToTimeSpan();
                    _champs[modele][jour][2].Time = pauseFin?.ToTimeSpan();
                    _champs[modele][jour][3].Time = (depart ?? new TimeOnly(17, 0)).ToTimeSpan();
                }
            }

            CaseActif.IsChecked = _actifs[_modeleCourant].IsChecked;

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

            var types = new HorairesDefautApi
            {
                DebutCycle = SemaineDeDepart() is { } debut
                    ? debut.ToString(SemaineApi.FormatDate, CultureInfo.InvariantCulture)
                    : null
            };

            for (var modele = 0; modele < NombreDeModeles; modele++)
            {
                var entree = new ModeleApi
                {
                    Index = modele,
                    Actif = _actifs[modele].IsChecked == true
                };

                for (var jour = 0; jour < 7; jour++)
                {
                    if (_cases[modele][jour].IsChecked != true)
                    {
                        entree.Jours.Add(new JourDefautApi { Index = jour });
                        continue;
                    }

                    var sansPause = _sansPause[modele][jour].IsChecked == true;

                    entree.Jours.Add(new JourDefautApi
                    {
                        Index = jour,
                        Arrivee = EnTexte(_champs[modele][jour][0].Time),
                        PauseDebut = sansPause ? null : EnTexte(_champs[modele][jour][1].Time),
                        PauseFin = sansPause ? null : EnTexte(_champs[modele][jour][2].Time),
                        Depart = EnTexte(_champs[modele][jour][3].Time)
                    });
                }

                types.Modeles.Add(entree);
            }

            await _api.EnregistrerHorairesDefautAsync(types);

            TexteEtat.Text = "Enregistré.";
        }
        catch (Exception ex)
        {
            TexteEtat.Text = "Enregistrement impossible : " + ex.Message;
        }
    }

    private async void Fermer_Clicked(object? sender, EventArgs e) => await Navigation.PopModalAsync();

    /// <summary>Lundi de la semaine choisie, ou null si la liste indique « Aucune ».</summary>
    private DateOnly? SemaineDeDepart()
    {
        var numero = ChoixSemaine.SelectedIndex;

        return numero >= 1 ? SemaineTravail.LundiDeLaSemaine(AnneeChoisie(), numero) : null;
    }

    private static TimeOnly? EnHeure(string? texte) =>
        string.IsNullOrWhiteSpace(texte)
            ? null
            : TimeOnly.TryParseExact(texte, SemaineApi.FormatHeure, CultureInfo.InvariantCulture, DateTimeStyles.None, out var heure)
                ? heure
                : null;

    private static string? EnTexte(TimeSpan? valeur) =>
        valeur is null ? null : valeur.Value.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
}
