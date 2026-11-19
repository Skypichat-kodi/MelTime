namespace MelTimeClientAndroid;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Point d'entree : la connexion d'abord. Le Shell du modele a ete retire,
    /// une navigation simple suffit pour deux ecrans.
    /// </summary>
    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Pas de NavigationPage : elle afficherait une barre de titre vide
        // en haut de l'ecran, et les pages se remplacent entierement.
        return new Window(new PageConnexion());
    }
}
