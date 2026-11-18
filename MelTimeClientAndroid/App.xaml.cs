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
        return new Window(new NavigationPage(new PageConnexion()));
    }
}
