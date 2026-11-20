using System.Globalization;
using MelTime.Core.Api;
using MelTime.Core.Modeles;

namespace MelTimeClientAndroid;

/// <summary>
/// Salaires du client et prix de l'heure qui en decoule.
/// Les montants sont enregistres sur son compte, dans sa propre base :
/// ils ne sont visibles ni par l'administrateur, ni par les autres clients.
/// </summary>
public partial class PageReglages : ContentPage
{
    private readonly ClientApi _api;
    private readonly Client _profil;

    private ReglagesApi _reglages = new();
    private bool _chargement;

    public PageReglages(ClientApi api, Client profil)
    {
        InitializeComponent();

        _api = api;
        _profil = profil;

        Loaded += async (_, _) => await ChargerAsync();
    }

    /// <summary>Les salaires se lisent en paysage : champs et resultats cote a cote.</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        OrientationEcran.Paysage();
    }

    private async Task ChargerAsync()
    {
        try
        {
            _chargement = true;

            _reglages = await _api.ReglagesAsync();

            ChampBrut.Text = FormaterSaisie(_reglages.SalaireBrut);
            ChampNet.Text = FormaterSaisie(_reglages.SalaireNet);
        }
        catch (Exception ex)
        {
            TexteEtat.Text = "Chargement impossible : " + ex.Message;
        }
        finally
        {
            _chargement = false;
            CalculerPrix();
        }
    }

    private void Champ_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_chargement)
        {
            CalculerPrix();
        }
    }

    /// <summary>
    /// Prix de l'heure = salaire mensuel divise par les heures mensualisees
    /// du contrat (contrat hebdomadaire x 52 / 12), comme sur un bulletin.
    /// </summary>
    private void CalculerPrix()
    {
        var heuresMensuelles = _profil.ContratHebdomadaireMinutes / 60.0 * 52.0 / 12.0;
        var brut = LireMontant(ChampBrut.Text);
        var net = LireMontant(ChampNet.Text);

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

        TexteCharges.Text = brut is > 0 && net is > 0
            ? $"{brut.Value - net.Value:N2} €"
            : "—";
    }

    private async void Enregistrer_Clicked(object? sender, EventArgs e)
    {
        try
        {
            TexteEtat.Text = "Enregistrement…";

            _reglages = await _api.EnregistrerReglagesAsync(new ReglagesApi
            {
                SalaireBrut = LireMontant(ChampBrut.Text),
                SalaireNet = LireMontant(ChampNet.Text)
            });

            TexteEtat.Text = "Enregistré.";
        }
        catch (Exception ex)
        {
            TexteEtat.Text = "Enregistrement impossible : " + ex.Message;
        }
    }

    private async void Fermer_Clicked(object? sender, EventArgs e) => await Navigation.PopModalAsync();

    private static decimal? LireMontant(string? texte)
    {
        var propre = (texte ?? string.Empty).Replace(" ", string.Empty).Replace(",", ".");

        return decimal.TryParse(propre, NumberStyles.Any, CultureInfo.InvariantCulture, out var valeur)
            ? valeur
            : null;
    }

    private static string FormaterSaisie(decimal? montant) =>
        montant is null ? string.Empty : montant.Value.ToString("0.00", CultureInfo.InvariantCulture);
}
