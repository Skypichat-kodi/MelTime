using System.Diagnostics;
using MelTime.Core.Protocole;

namespace MelTime.Tray;

/// <summary>
/// Vie de l'icone dans la zone de notification.
/// Elle interroge le service a intervalle regulier pour refleter son etat reel :
/// vert si le service repond, rouge sinon.
/// Les notifications de connexion / deconnexion des utilisateurs viendront
/// lorsque l'authentification des clients sera en place.
/// </summary>
internal sealed class ContexteTray : ApplicationContext
{
    private static readonly TimeSpan Intervalle = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DelaiInterrogation = TimeSpan.FromSeconds(2);

    private readonly ClientPipe _pipe = new();
    private readonly NotifyIcon _icone = new();
    private readonly System.Windows.Forms.Timer _minuteur = new();
    private readonly IconeEtat _etatActif = new(Color.FromArgb(46, 160, 67));
    private readonly IconeEtat _etatInactif = new(Color.FromArgb(200, 60, 60));

    private bool _verificationEnCours;
    private bool _serviceActif;

    public ContexteTray()
    {
        var menu = new ContextMenuStrip();

        var ouvrir = new ToolStripMenuItem("Ouvrir l'administration");
        ouvrir.Click += (_, _) => OuvrirAdministration();

        var quitter = new ToolStripMenuItem("Quitter");
        quitter.Click += (_, _) => Quitter();

        menu.Items.Add(ouvrir);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(quitter);

        _icone.Icon = _etatInactif.Icone;
        _icone.Text = "MelTime — service injoignable";
        _icone.Visible = true;
        _icone.ContextMenuStrip = menu;
        _icone.DoubleClick += (_, _) => OuvrirAdministration();

        _minuteur.Interval = (int)Intervalle.TotalMilliseconds;
        _minuteur.Tick += async (_, _) => await VerifierServiceAsync();
        _minuteur.Start();

        _ = VerifierServiceAsync();
    }

    private async Task VerifierServiceAsync()
    {
        if (_verificationEnCours)
        {
            return;
        }

        _verificationEnCours = true;

        try
        {
            using var limite = new CancellationTokenSource(DelaiInterrogation);
            var reponse = await _pipe.EnvoyerAsync(
                new Requete { Commande = Commandes.ServiceVivant },
                limite.Token);

            AppliquerEtat(reponse.Succes);
        }
        catch (Exception)
        {
            // Service arrete, injoignable, ou droits insuffisants : l'icone passe au rouge.
            AppliquerEtat(false);
        }
        finally
        {
            _verificationEnCours = false;
        }
    }

    private void AppliquerEtat(bool actif)
    {
        if (actif != _serviceActif)
        {
            _serviceActif = actif;
            _icone.Icon = actif ? _etatActif.Icone : _etatInactif.Icone;
            _icone.Text = actif ? "MelTime — service actif" : "MelTime — service injoignable";
        }
    }

    private void OuvrirAdministration()
    {
        var chemin = LocaliserAdministration();

        if (chemin is null)
        {
            MessageBox.Show(
                "Le programme d'administration MelTime.UI.exe est introuvable."
                + Environment.NewLine
                + "Il doit se trouver dans le même dossier que le Tray.",
                "MelTime",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(chemin) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Impossible de démarrer l'administration :{Environment.NewLine}{ex.Message}",
                "MelTime",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Recherche MelTime.UI.exe a cote du Tray (cas du deploiement),
    /// puis dans un sous-dossier voisin (cas des dossiers de publication separes).
    /// </summary>
    private static string? LocaliserAdministration()
    {
        var racine = AppContext.BaseDirectory;

        var candidats = new[]
        {
            Path.Combine(racine, "MelTime.UI.exe"),
            Path.GetFullPath(Path.Combine(racine, "..", "UI", "MelTime.UI.exe"))
        };

        return candidats.FirstOrDefault(File.Exists);
    }

    private void Quitter()
    {
        _minuteur.Stop();
        _icone.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _minuteur.Dispose();
            _icone.Dispose();
            _etatActif.Dispose();
            _etatInactif.Dispose();
        }

        base.Dispose(disposing);
    }
}
