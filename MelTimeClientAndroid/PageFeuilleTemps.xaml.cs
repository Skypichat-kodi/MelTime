using MelTime.Core.Api;
using MelTime.Core.Modeles;

namespace MelTimeClientAndroid;

/// <summary>
/// Feuille de temps hebdomadaire. Pour l'instant, une page d'attente qui
/// confirme la connexion et permet de se deconnecter : la grille sera
/// ajoutee a l'etape suivante.
/// </summary>
public partial class PageFeuilleTemps : ContentPage
{
    private readonly ClientApi _api;
    private readonly SessionAndroid _session = new();

    public PageFeuilleTemps(ClientApi api, Client profil)
    {
        InitializeComponent();

        _api = api;
        Profil = profil;

        Title = $"MelTime — {profil.Identifiant}";
        TexteBienvenue.Text = $"Bonjour {profil.Prenom} {profil.Nom}";
        TexteContrat.Text = $"Contrat hebdomadaire : {profil.ContratAffiche}";
    }

    public Client Profil { get; }

    private async void BoutonDeconnexion_Clicked(object? sender, EventArgs e)
    {
        await _api.DeconnecterAsync();
        _session.EffacerJeton();

        var fenetre = Application.Current?.Windows.FirstOrDefault();

        if (fenetre is not null)
        {
            fenetre.Page = new NavigationPage(new PageConnexion());
        }
    }
}
