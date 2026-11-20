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

    /// <summary>
    /// Empeche les cases de se repondre en boucle : cocher « maladie »
    /// decoche « travaillee », dont le gestionnaire decocherait « maladie »
    /// a son tour.
    /// </summary>
    private bool _synchronisation;

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

        var pauseDebut = EnHeure(jour.PauseDebut);
        var pauseFin = EnHeure(jour.PauseFin);

        ChampPauseDebut.Time = pauseDebut?.ToTimeSpan();
        ChampPauseFin.Time = pauseFin?.ToTimeSpan();

        ChampDepart.Time = EnHeure(jour.Depart)?.ToTimeSpan() ?? new TimeSpan(17, 0, 0);

        // Le positionnement initial ne doit pas declencher les gestionnaires :
        // ils decocheraient des cases que l'on vient de remplir.
        _synchronisation = true;

        try
        {
            // Une journee sans arrivee est consideree comme non travaillee.
            CaseTravaillee.IsChecked = EnHeure(jour.Arrivee) is not null;

            // Une pause absente ou incomplete se presente comme « sans pause ».
            CaseSansPause.IsChecked = pauseDebut is null || pauseFin is null;

            CaseMaladie.IsChecked = jour.Maladie;
            CaseConge.IsChecked = jour.Conge;
        }
        finally
        {
            _synchronisation = false;
        }

        ZoneHoraires.IsEnabled = CaseTravaillee.IsChecked == true;
        AppliquerPause();
    }

    /// <summary>
    /// Un arret maladie remplace la journee travaillee : les horaires sont
    /// grises, mais le serveur les remplit avec le modele pour que la journee
    /// soit payee au volume prevu.
    /// </summary>
    private void CaseMaladie_Changed(object? sender, CheckedChangedEventArgs e)
    {
        if (_synchronisation)
        {
            return;
        }

        _synchronisation = true;

        try
        {
            if (e.Value)
            {
                CaseTravaillee.IsChecked = false;
                CaseConge.IsChecked = false;
            }

            ZoneHoraires.IsEnabled = !e.Value && CaseTravaillee.IsChecked == true;
        }
        finally
        {
            _synchronisation = false;
        }
    }

    /// <summary>Un conge paye suit la meme regle qu'un arret : paye, non travaille.</summary>
    private void CaseConge_Changed(object? sender, CheckedChangedEventArgs e)
    {
        if (_synchronisation)
        {
            return;
        }

        _synchronisation = true;

        try
        {
            if (e.Value)
            {
                CaseTravaillee.IsChecked = false;
                CaseMaladie.IsChecked = false;
            }

            ZoneHoraires.IsEnabled = !e.Value && CaseTravaillee.IsChecked == true;
        }
        finally
        {
            _synchronisation = false;
        }
    }

    /// <summary>
    /// Grise et vide les deux selecteurs de pause quand la journee est
    /// declaree continue, les retablit avec une valeur usuelle sinon.
    /// </summary>
    private void AppliquerPause()
    {
        var sansPause = CaseSansPause.IsChecked == true;

        ChampPauseDebut.IsEnabled = !sansPause;
        ChampPauseFin.IsEnabled = !sansPause;

        if (sansPause)
        {
            ChampPauseDebut.Time = null;
            ChampPauseFin.Time = null;
        }
        else
        {
            ChampPauseDebut.Time ??= new TimeSpan(12, 0, 0);
            ChampPauseFin.Time ??= new TimeSpan(13, 0, 0);
        }
    }

    private void CaseSansPause_Changed(object? sender, CheckedChangedEventArgs e)
    {
        AppliquerPause();
    }

    /// <summary>Les quatre horaires et les marques tiennent en paysage.</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        OrientationEcran.Paysage();
    }

    private void CaseTravaillee_Changed(object? sender, CheckedChangedEventArgs e)
    {
        if (_synchronisation)
        {
            return;
        }

        _synchronisation = true;

        try
        {
            // Cocher « travaillee » retire les marques d'absence : la journee
            // ne peut pas etre a la fois travaillee et en conge.
            if (e.Value)
            {
                CaseMaladie.IsChecked = false;
                CaseConge.IsChecked = false;
            }

            ZoneHoraires.IsEnabled = e.Value;
        }
        finally
        {
            _synchronisation = false;
        }
    }

    private async void Valider_Clicked(object? sender, EventArgs e)
    {
        if (CaseMaladie.IsChecked == true || CaseConge.IsChecked == true)
        {
            // Le serveur remplira les horaires avec le modele : la journee
            // compte ainsi comme payee au volume prevu, sans creer de manque.
            _jour.Maladie = CaseMaladie.IsChecked == true;
            _jour.Conge = CaseConge.IsChecked == true;
            _jour.Arrivee = null;
            _jour.PauseDebut = null;
            _jour.PauseFin = null;
            _jour.Depart = null;
        }
        else if (CaseTravaillee.IsChecked)
        {
            _jour.Maladie = false;
            _jour.Conge = false;

            var sansPause = CaseSansPause.IsChecked == true;

            _jour.Arrivee = EnTexte(ChampArrivee.Time);
            _jour.PauseDebut = sansPause ? null : EnTexte(ChampPauseDebut.Time);
            _jour.PauseFin = sansPause ? null : EnTexte(ChampPauseFin.Time);
            _jour.Depart = EnTexte(ChampDepart.Time);
        }
        else
        {
            _jour.Maladie = false;
            _jour.Conge = false;
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
