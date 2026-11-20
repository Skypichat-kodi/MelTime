using System.Windows;
using System.Windows.Input;

namespace MelTime.ClientWindows;

/// <summary>
/// Boite de dialogue aux couleurs de MelTime, en remplacement des MessageBox
/// de Windows qui juraient avec le reste.
///
/// Sans barre de titre : le panneau se deplace au glisser, comme la fenetre
/// de connexion. Deux usages seulement, informer et confirmer.
/// </summary>
public partial class Dialogue : Window
{
    private Dialogue(string titre, string message, bool avecAnnulation, string libelleValidation)
    {
        InitializeComponent();

        Titre.Text = titre;
        Message.Text = message;

        BoutonValider.Content = libelleValidation;

        if (avecAnnulation)
        {
            BoutonAnnuler.Visibility = Visibility.Visible;
        }

        SourceInitialized += (_, _) =>
        {
            ThemeSombre.Appliquer(this);
            ThemeSombre.ArrondirCoins(this);
        };

        Loaded += (_, _) => BoutonValider.Focus();
    }

    /// <summary>Affiche une information et attend que l'utilisateur la referme.</summary>
    public static void Informer(Window? proprietaire, string titre, string message, string libelle = "D'accord")
    {
        var dialogue = new Dialogue(titre, message, avecAnnulation: false, libelle);

        if (proprietaire is not null && proprietaire.IsVisible)
        {
            dialogue.Owner = proprietaire;
        }

        dialogue.ShowDialog();
    }

    /// <summary>Pose une question et retourne vrai si l'utilisateur accepte.</summary>
    public static bool Confirmer(Window? proprietaire, string titre, string message, string libelle = "Continuer")
    {
        var dialogue = new Dialogue(titre, message, avecAnnulation: true, libelle);

        if (proprietaire is not null && proprietaire.IsVisible)
        {
            dialogue.Owner = proprietaire;
        }

        return dialogue.ShowDialog() == true;
    }

    private void Valider_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Annuler_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    /// <summary>Deplace la fenetre : le panneau fait office de barre de titre.</summary>
    private void ZoneDeplacement_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Fenetre_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }
}
