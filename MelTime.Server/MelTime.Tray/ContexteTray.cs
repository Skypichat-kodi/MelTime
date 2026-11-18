using System.Diagnostics;
using MelTime.Core.Modeles;
using MelTime.Core.Protocole;

namespace MelTime.Tray;

/// <summary>
/// Vie de l'icone dans la zone de notification.
///
/// Le Tray suit la vie du service : il surveille son etat et se ferme des que
/// le service disparait. Clic gauche : ouvre l'administration. Clic droit :
/// propose d'arreter le service, ce qui ferme du meme coup l'administration.
/// </summary>
internal sealed class ContexteTray : ApplicationContext
{
    private static readonly TimeSpan Intervalle = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DelaiInterrogation = TimeSpan.FromSeconds(3);

    /// <summary>Nombre d'echecs consecutifs avant de considerer le service disparu.</summary>
    private const int EchecsToleres = 2;

    private readonly ClientPipe _pipe = new();
    private readonly NotifyIcon _icone = new();
    private readonly System.Windows.Forms.Timer _minuteur = new();
    private readonly IconeEtat _etatActif = new("tray-vert.ico", Color.FromArgb(46, 160, 67));
    private readonly IconeEtat _etatInactif = new("tray-rouge.ico", Color.FromArgb(217, 48, 37));

    private readonly Dictionary<string, SessionActive> _sessionsConnues = new(StringComparer.Ordinal);

    private bool _verificationEnCours;
    private bool _serviceActif;
    private bool _serviceDejaVu;
    private bool _premierPassage = true;
    private int _echecsConsecutifs;

    public ContexteTray()
    {
        var menu = new ContextMenuStrip();

        var quitterService = new ToolStripMenuItem("Quitter le service MelTime");
        quitterService.Click += (_, _) => DemanderArretService();

        menu.Items.Add(quitterService);

        _icone.Icon = _etatInactif.Icone;
        _icone.Text = "MelTime — recherche du service…";
        _icone.Visible = true;
        _icone.ContextMenuStrip = menu;

        _icone.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                OuvrirAdministration();
            }
        };

        _minuteur.Interval = (int)Intervalle.TotalMilliseconds;
        _minuteur.Tick += async (_, _) => await InterrogerAsync();
        _minuteur.Start();

        // Premiere interrogation une fois la boucle de messages installee :
        // sans cela, un arret immediat s'executerait hors du thread de l'interface.
        Application.Idle += SurPremierIdle;
    }

    private void SurPremierIdle(object? sender, EventArgs e)
    {
        Application.Idle -= SurPremierIdle;
        _ = InterrogerAsync();
    }

    // ------------------------------------------------------------------
    // Surveillance du service et des sessions
    // ------------------------------------------------------------------

    private async Task InterrogerAsync()
    {
        if (_verificationEnCours)
        {
            return;
        }

        _verificationEnCours = true;

        try
        {
            using var limite = new CancellationTokenSource(DelaiInterrogation);

            // La liste des sessions prouve aussi que le service repond : une seule question suffit.
            var reponse = await _pipe.EnvoyerAsync(
                new Requete { Commande = Commandes.SessionsLister },
                limite.Token);

            if (!reponse.Succes)
            {
                SignalerIndisponible();
                return;
            }

            _echecsConsecutifs = 0;
            _serviceDejaVu = true;

            AppliquerEtat(true);
            ComparerSessions(reponse.Sessions ?? new List<SessionActive>());
        }
        catch (Exception)
        {
            SignalerIndisponible();
        }
        finally
        {
            _verificationEnCours = false;
        }
    }

    private void SignalerIndisponible()
    {
        _echecsConsecutifs++;
        AppliquerEtat(false);

        if (_echecsConsecutifs < EchecsToleres)
        {
            return;
        }

        // Le service a disparu : le Tray n'a plus de raison d'exister.
        if (_serviceDejaVu)
        {
            Fermer();
            return;
        }

        // Service absent des le demarrage : on patiente un peu, puis on previent.
        if (_echecsConsecutifs >= 3)
        {
            MessageBox.Show(
                "Le service MelTime ne répond pas." + Environment.NewLine
                + "Le Tray va se fermer." + Environment.NewLine + Environment.NewLine
                + "Vérifiez que la tâche planifiée du service est active.",
                "MelTime",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            Fermer();
        }
    }

    private void ComparerSessions(IReadOnlyList<SessionActive> sessions)
    {
        var actuelles = new Dictionary<string, SessionActive>(StringComparer.Ordinal);

        foreach (var session in sessions)
        {
            actuelles[CleSession(session)] = session;
        }

        // Au premier contact, on prend connaissance sans rien annoncer :
        // l'utilisateur n'a pas besoin d'un deluge de notifications au demarrage.
        if (_premierPassage)
        {
            foreach (var (cle, session) in actuelles)
            {
                _sessionsConnues[cle] = session;
            }

            _premierPassage = false;
            MettreAJourBulle();
            return;
        }

        foreach (var (cle, session) in actuelles)
        {
            if (_sessionsConnues.ContainsKey(cle))
            {
                continue;
            }

            _sessionsConnues[cle] = session;

            Notifier(
                "Connexion",
                $"{session.NomComplet} s'est connecté(e) à {session.Depuis.ToLocalTime():HH:mm}.");
        }

        foreach (var cle in _sessionsConnues.Keys.ToList())
        {
            if (actuelles.ContainsKey(cle))
            {
                continue;
            }

            var partie = _sessionsConnues[cle];
            _sessionsConnues.Remove(cle);

            Notifier("Déconnexion", $"{partie.NomComplet} s'est déconnecté(e).");
        }

        MettreAJourBulle();
    }

    /// <summary>
    /// Identite d'une session : un client peut en ouvrir plusieurs
    /// (Windows et Android), l'heure d'ouverture les distingue.
    /// </summary>
    private static string CleSession(SessionActive session) =>
        $"{session.IdClient}|{session.Depuis.ToUniversalTime():O}";

    private void Notifier(string titre, string message) =>
        _icone.ShowBalloonTip(5000, titre, message, ToolTipIcon.Info);

    // ------------------------------------------------------------------
    // Etat de l'icone
    // ------------------------------------------------------------------

    private void AppliquerEtat(bool actif)
    {
        if (actif != _serviceActif)
        {
            _serviceActif = actif;
            _icone.Icon = actif ? _etatActif.Icone : _etatInactif.Icone;
        }

        MettreAJourBulle();
    }

    private void MettreAJourBulle()
    {
        var texte = _serviceActif
            ? $"MelTime — service actif — {_sessionsConnues.Count} client(s) actif(s)"
            : "MelTime — service injoignable";

        // NotifyIcon refuse les info-bulles de plus de 63 caracteres.
        _icone.Text = texte.Length > 63 ? texte[..63] : texte;
    }

    // ------------------------------------------------------------------
    // Actions
    // ------------------------------------------------------------------

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

    private void DemanderArretService()
    {
        var confirmation = MessageBox.Show(
            "Arrêter le service MelTime ?" + Environment.NewLine + Environment.NewLine
            + "L'administration et cette icône seront fermées.",
            "MelTime",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);

        if (confirmation == DialogResult.Yes)
        {
            _ = ArreterServiceAsync();
        }
    }

    private async Task ArreterServiceAsync()
    {
        try
        {
            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _pipe.EnvoyerAsync(new Requete { Commande = Commandes.ServiceArreter }, limite.Token);
        }
        catch (Exception)
        {
            // Le service peut disparaitre avant d'avoir repondu : c'est le resultat attendu.
        }

        Fermer();
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

    private void Fermer()
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
