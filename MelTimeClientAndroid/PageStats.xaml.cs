using System.Globalization;
using MelTime.Core.Agenda;
using MelTime.Core.Api;

namespace MelTimeClientAndroid;

/// <summary>
/// Statistiques d'heures : pour la semaine affichee, le mois et l'annee,
/// les heures faites au-dela de 35 h, celles qui manquent au regard du
/// contrat, et la difference qui en resulte.
///
/// Les absences sont presentees a part : elles ne se melangent pas aux heures.
/// </summary>
public partial class PageStats : ContentPage
{
    private static readonly Color CouleurSupplementaires = Color.FromArgb("#0078D4");
    private static readonly Color CouleurManquantes = Color.FromArgb("#FF5B5B");
    private static readonly Color CouleurTexte = Color.FromArgb("#F2F2F2");
    private static readonly Color CouleurSecondaire = Color.FromArgb("#A0A0A0");
    private static readonly Color CouleurEntete = Color.FromArgb("#2E2E2E");
    private static readonly Color CouleurAbsences = Color.FromArgb("#242424");

    private static readonly TimeSpan SeuilLegal = TimeSpan.FromHours(35);

    private readonly ClientApi _api;
    private readonly SemaineApi _semaine;

    public PageStats(ClientApi api, SemaineApi semaine)
    {
        InitializeComponent();

        _api = api;
        _semaine = semaine;

        ConstruireTableau();

        Loaded += async (_, _) => await ChargerAsync();
    }

    /// <summary>Le tableau se lit mieux en paysage.</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        OrientationEcran.Paysage();
    }

    private void ConstruireTableau()
    {
        Tableau.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(150)));

        for (var colonne = 1; colonne <= 5; colonne++)
        {
            Tableau.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        for (var ligne = 0; ligne < 6; ligne++)
        {
            Tableau.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        AjouterEntete(0, 0, "PÉRIODE");
        AjouterEntete(0, 1, "HEURES SUP.");
        AjouterEntete(0, 2, "HEURES MANQ.");
        AjouterEntete(0, 3, "DIFFÉRENCE");
        AjouterEntete(0, 4, "BRUT");
        AjouterEntete(0, 5, "NET");

        AjouterLibelle(1, "Semaine");
        AjouterLibelle(2, "Mois");
        AjouterLibelle(3, "Année · 52 sem.");

        _lignes[0] = (1, ValueLigne(1));
        _lignes[1] = (2, ValueLigne(2));
        _lignes[2] = (3, ValueLigne(3));
    }

    private readonly (int Ligne, Label[] Valeurs)[] _lignes = new (int, Label[])[3];

    private Label[] ValueLigne(int ligne)
    {
        var valeurs = new Label[5];

        for (var colonne = 1; colonne <= 5; colonne++)
        {
            valeurs[colonne - 1] = new Label
            {
                Text = "—",
                FontSize = 15,
                FontAttributes = colonne == 3 ? FontAttributes.Bold : FontAttributes.None,
                TextColor = colonne switch
                {
                    1 => CouleurSupplementaires,
                    2 => CouleurManquantes,
                    _ => CouleurTexte
                },
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(12, 8)
            };

            Tableau.Children.Add(valeurs[colonne - 1]);
            Grid.SetRow(valeurs[colonne - 1], ligne);
            Grid.SetColumn(valeurs[colonne - 1], colonne);
        }

        return valeurs;
    }

    private void AjouterEntete(int ligne, int colonne, string texte)
    {
        var cellule = new Border
        {
            BackgroundColor = CouleurEntete,
            Padding = new Thickness(14, 7),
            Content = new Label
            {
                Text = texte,
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = CouleurSecondaire
            }
        };

        Tableau.Children.Add(cellule);
        Grid.SetRow(cellule, ligne);
        Grid.SetColumn(cellule, colonne);
    }

    private void AjouterLibelle(int ligne, string texte)
    {
        var cellule = new Border
        {
            Padding = new Thickness(14, 8),
            Content = new Label
            {
                Text = texte,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = CouleurTexte,
                VerticalOptions = LayoutOptions.Center
            }
        };

        Tableau.Children.Add(cellule);
        Grid.SetRow(cellule, ligne);
        Grid.SetColumn(cellule, 0);
    }

    // ------------------------------------------------------------------
    // Chargement
    // ------------------------------------------------------------------

    /// <summary>Taux horaire brut et net, deduits des salaires mensuels.</summary>
    private decimal? _tauxBrut;
    private decimal? _tauxNet;

    private async Task ChargerAsync()
    {
        await ChargerTauxAsync();

        ChargerSemaine();

        await ChargerMoisAsync();
        await ChargerAnneeAsync();
    }

    /// <summary>
    /// Le taux horaire se deduit du salaire mensuel et des heures
    /// mensualisees du contrat : contrat hebdomadaire x 52 / 12.
    /// </summary>
    private async Task ChargerTauxAsync()
    {
        try
        {
            var reglages = await _api.ReglagesAsync();

            var heuresMensuelles = _semaine.ContratHebdomadaireMinutes / 60.0 * 52.0 / 12.0;

            if (heuresMensuelles <= 0)
            {
                return;
            }

            _tauxBrut = reglages.SalaireBrut is > 0
                ? (decimal?)((double)reglages.SalaireBrut.Value / heuresMensuelles)
                : null;

            _tauxNet = reglages.SalaireNet is > 0
                ? (decimal?)((double)reglages.SalaireNet.Value / heuresMensuelles)
                : null;
        }
        catch (Exception)
        {
            // Sans salaires enregistres, les colonnes restent vides.
        }
    }

    private void ChargerSemaine()
    {
        var total = EnDuree(_semaine.TotalTravaille);
        var contrat = TimeSpan.FromMinutes(_semaine.ContratHebdomadaireMinutes);

        var supplementaires = total > SeuilLegal ? total - SeuilLegal : TimeSpan.Zero;
        var manquantes = total < contrat ? contrat - total : TimeSpan.Zero;

        Remplir(0, supplementaires, manquantes, total);
    }

    private async Task ChargerMoisAsync()
    {
        try
        {
            var premier = _semaine.Jours.FirstOrDefault();

            if (premier is null
                || !DateOnly.TryParseExact(premier.Date, SemaineApi.FormatDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return;
            }

            var mois = await _api.TotalMoisAsync(date.Year, date.Month);

            Remplir(1, EnDuree(mois.Supplementaires), EnDuree(mois.Manquantes), EnDuree(mois.TotalTravaille));
        }
        catch (Exception)
        {
            // Le mois est une information annexe : son echec ne doit pas
            // empecher le reste du tableau de s'afficher.
        }
    }

    private async Task ChargerAnneeAsync()
    {
        try
        {
            var solde = await _api.SoldeAsync();

            Remplir(2, EnDuree(solde.Supplementaires), EnDuree(solde.Manquantes), EnDuree(solde.TotalTravaille));

            AjouterAbsences(solde.CongesPris, solde.CongesAcquis, solde.JoursMaladie);
        }
        catch (Exception)
        {
            // Idem : le bilan annuel reste indicatif.
        }
    }

    private void Remplir(int index, TimeSpan supplementaires, TimeSpan manquantes, TimeSpan travaille)
    {
        var (_, valeurs) = _lignes[index];

        var difference = supplementaires - manquantes;

        valeurs[0].Text = Durees.Formater(supplementaires);
        valeurs[1].Text = Durees.Formater(manquantes);

        valeurs[2].Text = difference > TimeSpan.Zero
            ? "+" + Durees.Formater(difference)
            : Durees.Formater(difference);

        valeurs[2].TextColor = difference > TimeSpan.Zero
            ? CouleurSupplementaires
            : difference < TimeSpan.Zero ? CouleurManquantes : CouleurTexte;

        // Precalcul des salaires : heures accomplies x taux horaire.
        valeurs[3].Text = Montant(_tauxBrut, travaille);
        valeurs[4].Text = Montant(_tauxNet, travaille);
    }

    private static string Montant(decimal? taux, TimeSpan heures) =>
        taux is null || heures <= TimeSpan.Zero
            ? "—"
            : $"{(decimal)heures.TotalHours * taux.Value:N2} €";

    /// <summary>Les absences s'ajoutent sous les heures, sur fond distinct.</summary>
    private void AjouterAbsences(int congesPris, int congesAcquis, int joursMaladie)
    {
        Tableau.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Tableau.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        AjouterLigneAbsence(4, "Congés payés", $"{congesPris} / {congesAcquis}");
        AjouterLigneAbsence(5, "Arrêt maladie", $"{joursMaladie} j");
    }

    private void AjouterLigneAbsence(int ligne, string libelle, string valeur)
    {
        var texte = new Border
        {
            BackgroundColor = CouleurAbsences,
            Padding = new Thickness(14, 8),
            Content = new Label
            {
                Text = libelle,
                FontSize = 13,
                TextColor = CouleurSecondaire,
                VerticalOptions = LayoutOptions.Center
            }
        };

        Tableau.Children.Add(texte);
        Grid.SetRow(texte, ligne);
        Grid.SetColumn(texte, 0);

        var contenu = new Border
        {
            BackgroundColor = CouleurAbsences,
            Padding = new Thickness(14, 8),
            Content = new Label
            {
                Text = valeur,
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = CouleurTexte,
                VerticalOptions = LayoutOptions.Center
            }
        };

        Tableau.Children.Add(contenu);
        Grid.SetRow(contenu, ligne);
        Grid.SetColumn(contenu, 1);
    }

    private async void Fermer_Clicked(object? sender, EventArgs e) => await Navigation.PopModalAsync();

    private static TimeSpan EnDuree(string? texte)
    {
        if (string.IsNullOrWhiteSpace(texte))
        {
            return TimeSpan.Zero;
        }

        var negatif = texte.StartsWith('-');
        var parties = (negatif ? texte[1..] : texte).Split(':');

        if (parties.Length != 2
            || !int.TryParse(parties[0], out var heures)
            || !int.TryParse(parties[1], out var minutes))
        {
            return TimeSpan.Zero;
        }

        var duree = new TimeSpan(heures, minutes, 0);
        return negatif ? duree.Negate() : duree;
    }
}
