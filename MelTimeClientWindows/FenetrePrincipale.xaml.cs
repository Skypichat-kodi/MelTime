using System.Windows;
using MelTime.Core.Api;
using MelTime.Core.Modeles;

namespace MelTime.ClientWindows;

/// <summary>
/// Fenetre principale du client. Pour l'instant : confirmation de la connexion.
/// La feuille de temps hebdomadaire viendra s'installer ici.
/// </summary>
public partial class FenetrePrincipale : Window
{
    private readonly ClientApi _api;
    private readonly SessionLocale _session;

    public FenetrePrincipale(ClientApi api, SessionLocale session, Client profil)
    {
        InitializeComponent();

        _api = api;
        _session = session;
        Profil = profil;

        Title = $"MelTime — {profil.Identifiant}";
        TexteBienvenue.Text = $"Bonjour {profil.Prenom} {profil.Nom}";
        TexteContrat.Text = $"Contrat hebdomadaire : {profil.ContratAffiche}";
    }

    public Client Profil { get; }

    private async void BoutonDeconnexion_Click(object sender, RoutedEventArgs e)
    {
        await _api.DeconnecterAsync();
        _session.EffacerJeton();

        Application.Current.Shutdown();
    }
}
