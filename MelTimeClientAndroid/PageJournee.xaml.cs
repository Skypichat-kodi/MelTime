using System.Globalization;
using MelTime.Core.Api;

namespace MelTimeClientAndroid;

/// <summary>
/// Panneau d'edition d'une journee : quatre selecteurs d'heure natifs Android.
///
/// Le client Windows utilise des champs « HH:mm » au clavier ; sur un
/// telephone, la roue de selection est plus sure et n'ouvre pas le clavier
/// virtuel, qui masquerait la moitie de l'ecran.
/// </summary>
public partial class PageJournee : ContentPage
{
    private static readonly TimeSpan HeureParDefaut = new(8, 0, 0);

    private readonly JourApi _jour;
    private readonly TaskCompletionSource<bool> _terminee;

    public PageJournee(JourApi jour, string nomJour, TaskCompletionSource<bool> terminee)
    {
        InitializeComponent();

        _jour = jour;
        _terminee = terminee;

        var date = string.Empty;

        if (DateOnly.TryParseExact(jour.Date, SemaineApi.FormatDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var valeur))
        {
            date = valeur.ToString("dd/MM");
        }

        Titre.Text = $"{nomJour} {date}";

        ChampArrivee.Time = EnHeure(jour.Arrivee)?.ToTimeSpan() ?? HeureParDefaut;
        ChampPauseDebut.Time = EnHeure(jour.PauseDebut)?.ToTimeSpan() ?? new TimeSpan(12, 0, 0);
        ChampPauseFin.Time = EnHeure(jour.PauseFin)?.ToTimeSpan() ?? new TimeSpan(13, 0, 0);
        ChampDepart.Time = EnHeure(jour.Depart)?.ToTimeSpan() ?? new TimeSpan(17, 0, 0);

        // Une journee sans arrivee est consideree comme non travaillee.
        CaseTravaillee.IsChecked = EnHeure(jour.Arrivee) is not null;
        ZoneHoraires.IsEnabled = CaseTravaillee.IsChecked;
    }

    /// <summary>La journee s'edite en portrait : quatre champs empiles.</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        OrientationEcran.Portrait();
    }

    private void CaseTravaillee_Changed(object? sender, CheckedChangedEventArgs e)
    {
        ZoneHoraires.IsEnabled = e.Value;
    }

    private async void Valider_Clicked(object? sender, EventArgs e)
    {
        if (CaseTravaillee.IsChecked)
        {
            _jour.Arrivee = EnTexte(ChampArrivee.Time);
            _jour.PauseDebut = EnTexte(ChampPauseDebut.Time);
            _jour.PauseFin = EnTexte(ChampPauseFin.Time);
            _jour.Depart = EnTexte(ChampDepart.Time);
        }
        else
        {
            _jour.Arrivee = null;
            _jour.PauseDebut = null;
            _jour.PauseFin = null;
            _jour.Depart = null;
        }

        await Navigation.PopModalAsync();
        _terminee.TrySetResult(true);
    }

    private async void Annuler_Clicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
        _terminee.TrySetResult(false);
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
