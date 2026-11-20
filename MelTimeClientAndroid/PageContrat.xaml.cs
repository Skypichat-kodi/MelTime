using MelTime.Core.Api;
using MelTime.Core.Modeles;

namespace MelTimeClientAndroid;

/// <summary>
/// Contrat hebdomadaire et date d'arrivee.
///
/// Deux listes plutot qu'un champ libre : sur un telephone, eviter le clavier
/// pour une valeur qui se choisit parmi quelques dizaines est plus rapide et
/// sans faute de frappe.
///
/// La date de debut part directement sur le serveur : elle conditionne le
/// calcul du solde, qui doit ignorer les semaines anterieures a l'arrivee.
/// </summary>
public partial class PageContrat : ContentPage
{
    private readonly ClientApi _api;
    private readonly Client _profil;
    private readonly TaskCompletionSource<int?> _resultat;

    private bool _dateEffacee;

    public PageContrat(ClientApi api, Client profil, TaskCompletionSource<int?> resultat)
    {
        InitializeComponent();

        _api = api;
        _profil = profil;
        _resultat = resultat;

        for (var heures = 0; heures <= 60; heures++)
        {
            ChoixHeures.Items.Add(heures.ToString());
        }

        for (var minutes = 0; minutes < 60; minutes += 5)
        {
            ChoixMinutes.Items.Add(minutes.ToString("00"));
        }

        var valeur = Math.Max(0, profil.ContratHebdomadaireMinutes);

        ChoixHeures.SelectedIndex = Math.Min(60, valeur / 60);
        ChoixMinutes.SelectedIndex = Math.Min(11, (valeur % 60) / 5);

        ChampDebut.Date = profil.DebutContrat?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today;
    }

    /// <summary>Quatre elements en ligne : le paysage suffit largement.</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        OrientationEcran.Paysage();
    }

    /// <summary>Retire la date : le solde repart alors de la premiere semaine saisie.</summary>
    private void EffacerDebut_Clicked(object? sender, EventArgs e)
    {
        _dateEffacee = true;
        ChampDebut.Date = DateTime.Today;
        _profil.DebutContrat = null;
    }

    private async void Valider_Clicked(object? sender, EventArgs e)
    {
        var heures = ChoixHeures.SelectedIndex < 0 ? 0 : ChoixHeures.SelectedIndex;
        var minutes = ChoixMinutes.SelectedIndex < 0 ? 0 : ChoixMinutes.SelectedIndex * 5;

        await EnregistrerDebutAsync();

        await Navigation.PopModalAsync();
        _resultat.TrySetResult(heures * 60 + minutes);
    }

    /// <summary>
    /// Envoie la date au serveur, si elle a change. La date par defaut du
    /// selecteur n'est envoyee que si l'utilisateur a touche a la ligne.
    /// </summary>
    private async Task EnregistrerDebutAsync()
    {
        DateOnly? nouvelle = _dateEffacee ? null : DateOnly.FromDateTime(ChampDebut.Date ?? DateTime.Today);

        if (nouvelle == _profil.DebutContrat)
        {
            return;
        }

        try
        {
            var profil = await _api.EnregistrerProfilAsync(
                nouvelle?.ToString(SemaineApi.FormatDate, System.Globalization.CultureInfo.InvariantCulture));

            _profil.DebutContrat = profil.DebutContrat;
        }
        catch (Exception ex)
        {
            await DisplayAlert("MelTime", "Date non enregistrée : " + ex.Message, "Compris");
        }
    }

    private async void Annuler_Clicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
        _resultat.TrySetResult(null);
    }
}
