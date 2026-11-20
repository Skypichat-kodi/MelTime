namespace MelTimeClientAndroid;

/// <summary>
/// Choix de la duree hebdomadaire du contrat.
///
/// Deux listes plutot qu'un champ libre : sur un telephone, eviter le clavier
/// pour une valeur qui se choisit parmi quelques dizaines est plus rapide et
/// sans faute de frappe.
/// </summary>
public partial class PageContrat : ContentPage
{
    private readonly TaskCompletionSource<int?> _resultat;

    public PageContrat(int contratMinutes, TaskCompletionSource<int?> resultat)
    {
        InitializeComponent();

        _resultat = resultat;

        for (var heures = 0; heures <= 60; heures++)
        {
            ChoixHeures.Items.Add(heures.ToString());
        }

        for (var minutes = 0; minutes < 60; minutes += 5)
        {
            ChoixMinutes.Items.Add(minutes.ToString("00"));
        }

        var valeur = Math.Max(0, contratMinutes);

        ChoixHeures.SelectedIndex = Math.Min(60, valeur / 60);
        ChoixMinutes.SelectedIndex = Math.Min(11, (valeur % 60) / 5);
    }

    /// <summary>Quatre elements en ligne : le paysage suffit largement.</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        OrientationEcran.Paysage();
    }

    private async void Valider_Clicked(object? sender, EventArgs e)
    {
        var heures = ChoixHeures.SelectedIndex < 0 ? 0 : ChoixHeures.SelectedIndex;
        var minutes = ChoixMinutes.SelectedIndex < 0 ? 0 : ChoixMinutes.SelectedIndex * 5;

        await Navigation.PopModalAsync();
        _resultat.TrySetResult(heures * 60 + minutes);
    }

    private async void Annuler_Clicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
        _resultat.TrySetResult(null);
    }
}
