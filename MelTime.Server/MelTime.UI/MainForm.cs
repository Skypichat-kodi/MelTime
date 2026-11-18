using System.Diagnostics;
using MelTime.Core.Exploitation;
using MelTime.Core.Modeles;
using MelTime.Core.Protocole;

namespace MelTime.UI;

/// <summary>
/// Fenêtre d'administration, coupée en deux :
///  - à gauche, la gestion des comptes clients ;
///  - à droite, les paramètres du serveur (démarrage automatique, état, emplacements).
///
/// Toutes les opérations sur les comptes passent par le service via le pipe nommé :
/// cette fenêtre ne touche jamais directement la base.
/// </summary>
public sealed class MainForm : Form
{
    /// <summary>Signature des méthodes de création d'une tâche planifiée.</summary>
    private delegate bool CreateurTache(string cheminExecutable, out string message);

    // Palette de l'administration, accordée au bleu du client Windows.
    private static readonly Color CouleurAccent = Color.FromArgb(0x00, 0x78, 0xD4);
    private static readonly Color CouleurAccentFonce = Color.FromArgb(0x00, 0x5A, 0x9E);
    private static readonly Color CouleurAlternance = Color.FromArgb(0xF4, 0xF8, 0xFC);
    private static readonly Color CouleurLigne = Color.FromArgb(0xDD, 0xE4, 0xEC);
    private static readonly Color CouleurFondPanneau = Color.FromArgb(0xF7, 0xF9, 0xFB);

    /// <summary>Selection d'une ligne : bleu pale, pour ne pas se confondre avec
    /// le bleu plein des en-tetes.</summary>
    private static readonly Color CouleurSelection = Color.FromArgb(0xCB, 0xE1, 0xF7);

    private readonly ClientPipe _pipe = new();

    // --- Partie gauche : comptes clients ---
    private readonly DataGridView _grille = new();
    private readonly Button _boutonNouveau = new() { Text = "Nouveau", Width = 110, Height = 30 };
    private readonly Button _boutonModifier = new() { Text = "Modifier", Width = 110, Height = 30 };
    private readonly Button _boutonMotDePasse = new() { Text = "Mot de passe", Width = 120, Height = 30 };
    private readonly Button _boutonActiver = new() { Text = "Activer / Désactiver", Width = 150, Height = 30 };
    private readonly Button _boutonSupprimer = new() { Text = "Supprimer", Width = 110, Height = 30 };
    private readonly Button _boutonActualiser = new() { Text = "Actualiser", Width = 110, Height = 30 };

    // --- Partie droite : paramètres du serveur ---
    private readonly CheckBox _caseDemarrageAuto = new()
    {
        Text = "Démarrer MelTime avec Windows",
        AutoSize = false,
        Width = 290,
        Height = 24
    };

    private readonly Button _boutonAppliquer = new()
    {
        Text = "Appliquer",
        Width = 130,
        Height = 30,
        FlatStyle = FlatStyle.System
    };

    private readonly Label _etatDemarrage = new()
    {
        AutoSize = false,
        Location = new Point(16, 300),
        Size = new Size(300, 120)
    };

    private readonly Label _etatConnexion = new() { AutoSize = true, Location = new Point(16, 66) };
    private readonly Label _etatDonnees = new() { AutoSize = true, Location = new Point(16, 92) };

    // --- Réseau ---
    private readonly NumericUpDown _champPort = new() { Minimum = 1, Maximum = 65535, Width = 90 };
    private readonly Button _boutonPort = new()
    {
        Text = "Appliquer",
        Width = 110,
        Height = 26,
        FlatStyle = FlatStyle.System
    };

    private readonly Label _etatReseau = new()
    {
        AutoSize = false,
        Location = new Point(16, 194),
        Size = new Size(300, 44),
        ForeColor = SystemColors.GrayText
    };

    /// <summary>Derniere configuration lue : on la modifie, on ne la remplace pas.</summary>
    private ConfigurationServeur? _configurationServeur;

    private readonly ToolStripStatusLabel _etatService = new() { Text = "Connexion au service…" };

    private readonly System.Windows.Forms.Timer _surveillance = new();

    private List<Client> _clients = new();
    private bool _occupe;
    private int _echecsService;

    public MainForm()
    {
        Text = "MelTime — Administration du serveur";
        Icon = IconeApplication.Obtenir();
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1200, 620);
        MinimumSize = new Size(1000, 520);
        Font = new Font("Segoe UI", 9F);

        ConstruireInterface();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _ = SequenceDemarrageAsync();
    }

    // ------------------------------------------------------------------
    // Construction de l'interface
    // ------------------------------------------------------------------

    private void ConstruireInterface()
    {
        var separateur = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.Panel2,
            SplitterWidth = 6,
            BackColor = Color.FromArgb(0xDD, 0xE4, 0xEC)
        };

        separateur.Panel2.BackColor = CouleurFondPanneau;

        ConstruirePartieClients(separateur.Panel1);
        ConstruirePartieServeur(separateur.Panel2);

        var barreEtat = new StatusStrip();

        _etatService.Spring = true;
        _etatService.TextAlign = ContentAlignment.MiddleLeft;

        // La version vient de Git, via Directory.Build.props : elle ne peut pas mentir.
        var version = new ToolStripStatusLabel { Text = $"version {Application.ProductVersion}" };

        barreEtat.Items.Add(_etatService);
        barreEtat.Items.Add(version);

        Controls.Add(separateur);
        Controls.Add(barreEtat);

        // La largeur du panneau de droite doit être connue avant la division.
        Load += (_, _) => separateur.SplitterDistance = separateur.Width - 340;

        MettreAJourBoutons();
    }

    private void ConstruirePartieClients(Control parent)
    {
        var barreOutils = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 46,
            Padding = new Padding(8, 8, 8, 4),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        foreach (var bouton in new[]
                 {
                     _boutonNouveau, _boutonModifier, _boutonMotDePasse,
                     _boutonActiver, _boutonSupprimer, _boutonActualiser
                 })
        {
            bouton.Margin = new Padding(0, 0, 6, 0);
            bouton.FlatStyle = FlatStyle.System;
            barreOutils.Controls.Add(bouton);
        }

        _boutonNouveau.Click += async (_, _) => await NouveauAsync();
        _boutonModifier.Click += async (_, _) => await ModifierAsync();
        _boutonMotDePasse.Click += async (_, _) => await ChangerMotDePasseAsync();
        _boutonActiver.Click += async (_, _) => await BasculerActifAsync();
        _boutonSupprimer.Click += async (_, _) => await SupprimerAsync();
        _boutonActualiser.Click += async (_, _) => await ActualiserAsync();

        ConstruireGrille();
        _grille.SelectionChanged += (_, _) => MettreAJourBoutons();

        parent.Controls.Add(_grille);
        parent.Controls.Add(barreOutils);
    }

    private void ConstruirePartieServeur(Control parent)
    {
        var titre = new Label
        {
            Text = "Paramètres du serveur",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = CouleurAccent,
            AutoSize = true,
            Location = new Point(16, 14)
        };

        var titreEtat = new Label
        {
            Text = "État",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = CouleurAccentFonce,
            AutoSize = true,
            Location = new Point(16, 44)
        };

        var titreReseau = new Label
        {
            Text = "Réseau",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = CouleurAccentFonce,
            AutoSize = true,
            Location = new Point(16, 124)
        };

        var etiquettePort = new Label
        {
            Text = "Port d'écoute :",
            AutoSize = true,
            Location = new Point(16, 158)
        };

        _champPort.Location = new Point(120, 154);
        _boutonPort.Location = new Point(220, 153);
        _boutonPort.Click += async (_, _) => await AppliquerPortAsync();

        var aidePort = new Label
        {
            Text = "Un redémarrage du service est nécessaire pour changer de port.",
            AutoSize = false,
            Location = new Point(16, 178),
            Size = new Size(300, 18),
            ForeColor = SystemColors.GrayText
        };

        var titreDemarrage = new Label
        {
            Text = "Démarrage automatique",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = CouleurAccentFonce,
            AutoSize = true,
            Location = new Point(16, 250)
        };

        var explication = new Label
        {
            Text = "Le service tourne en arrière-plan ; l'icône de notification est sa seule "
                   + "partie visible, elle est donc toujours installée avec lui. "
                   + "L'opération demande les droits d'administrateur.",
            AutoSize = false,
            Location = new Point(16, 274),
            Size = new Size(300, 62),
            ForeColor = SystemColors.GrayText
        };

        _caseDemarrageAuto.Location = new Point(16, 344);
        _boutonAppliquer.Location = new Point(16, 380);
        _boutonAppliquer.Click += (_, _) => AppliquerDemarrage();

        _etatDemarrage.Location = new Point(16, 418);
        _etatDonnees.ForeColor = SystemColors.GrayText;

        parent.Controls.Add(titre);
        parent.Controls.Add(titreEtat);
        parent.Controls.Add(_etatConnexion);
        parent.Controls.Add(_etatDonnees);
        parent.Controls.Add(titreReseau);
        parent.Controls.Add(etiquettePort);
        parent.Controls.Add(_champPort);
        parent.Controls.Add(_boutonPort);
        parent.Controls.Add(aidePort);
        parent.Controls.Add(_etatReseau);
        parent.Controls.Add(titreDemarrage);
        parent.Controls.Add(explication);
        parent.Controls.Add(_caseDemarrageAuto);
        parent.Controls.Add(_boutonAppliquer);
        parent.Controls.Add(_etatDemarrage);

        MettreAJourEtatConnexion(false);
    }

    private void ConstruireGrille()
    {
        _grille.Dock = DockStyle.Fill;
        _grille.ReadOnly = true;
        _grille.AllowUserToAddRows = false;
        _grille.AllowUserToDeleteRows = false;
        _grille.AllowUserToResizeRows = false;
        _grille.MultiSelect = false;
        _grille.RowHeadersVisible = false;
        _grille.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grille.AutoGenerateColumns = false;
        _grille.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grille.BackgroundColor = SystemColors.Window;
        _grille.BorderStyle = BorderStyle.None;
        _grille.EnableHeadersVisualStyles = false;

        // En-tetes bleus : ils se detachent franchement des lignes, alors que
        // le gris systeme les confondait avec le contenu.
        _grille.ColumnHeadersDefaultCellStyle.BackColor = CouleurAccent;
        _grille.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _grille.ColumnHeadersDefaultCellStyle.SelectionBackColor = CouleurAccent;
        _grille.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
        _grille.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _grille.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 0, 0);
        _grille.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grille.ColumnHeadersHeight = 34;

        // Lignes alternees, selection bleue, separateurs horizontaux discrets.
        _grille.AlternatingRowsDefaultCellStyle.BackColor = CouleurAlternance;
        _grille.DefaultCellStyle.SelectionBackColor = CouleurSelection;
        _grille.DefaultCellStyle.SelectionForeColor = Color.FromArgb(0x14, 0x14, 0x14);
        _grille.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
        _grille.RowTemplate.Height = 28;
        _grille.GridColor = CouleurLigne;
        _grille.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;

        _grille.Columns.Add(CreerColonneTexte(nameof(Client.Identifiant), "Identifiant", 22));
        _grille.Columns.Add(CreerColonneTexte(nameof(Client.Nom), "Nom", 22));
        _grille.Columns.Add(CreerColonneTexte(nameof(Client.Prenom), "Prénom", 22));
        _grille.Columns.Add(CreerColonneTexte(nameof(Client.ContratAffiche), "Contrat", 11));
        _grille.Columns.Add(CreerColonneTexte(nameof(Client.PaysLibelle), "Pays", 11));
        _grille.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(Client.Actif),
            HeaderText = "Actif",
            FillWeight = 9,
            ReadOnly = true
        });

        var colonneConnexion = CreerColonneTexte(nameof(Client.DerniereConnexion), "Dernière connexion", 18);
        colonneConnexion.DefaultCellStyle.Format = "g";
        colonneConnexion.DefaultCellStyle.NullValue = "—";
        _grille.Columns.Add(colonneConnexion);
    }

    private static DataGridViewTextBoxColumn CreerColonneTexte(string propriete, string entete, float poids) => new()
    {
        DataPropertyName = propriete,
        HeaderText = entete,
        FillWeight = poids,
        ReadOnly = true
    };

    // ------------------------------------------------------------------
    // Démarrage : le service d'abord, puis le Tray
    // ------------------------------------------------------------------

    private async Task SequenceDemarrageAsync()
    {
        if (!await ServiceDisponibleAsync())
        {
            if (!await DemarrerServiceAsync())
            {
                MessageBox.Show(this,
                    "Le service MelTime n'a pas pu démarrer." + Environment.NewLine + Environment.NewLine
                    + "Vérifiez que MelTime.Service.exe est présent, puis relancez l'administration.",
                    "MelTime", MessageBoxButtons.OK, MessageBoxIcon.Error);

                Application.Exit();
                return;
            }
        }

        AssurerTray();
        RelireDemarrage();

        await ActualiserAsync();
        await ChargerConfigurationAsync();

        _surveillance.Interval = 5000;
        _surveillance.Tick += async (_, _) => await SurveillerServiceAsync();
        _surveillance.Start();
    }

    private async Task<bool> ServiceDisponibleAsync()
    {
        try
        {
            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var reponse = await _pipe.EnvoyerAsync(
                new Requete { Commande = Commandes.ServiceVivant },
                limite.Token);

            return reponse.Succes;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<bool> DemarrerServiceAsync()
    {
        var chemin = TachesPlanifiees.Localiser("MelTime.Service.exe");

        if (chemin is null)
        {
            MessageBox.Show(this,
                "MelTime.Service.exe est introuvable." + Environment.NewLine
                + "Il doit se trouver dans C:\\ProgramData\\MelTime.",
                "MelTime", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        using var attente = new FormulaireAttenteService();
        attente.Show(this);

        try
        {
            Process.Start(new ProcessStartInfo(chemin)
            {
                // Aucune fenetre noire : le service n'a pas d'interface,
                // c'est le Tray qui represente sa partie visible.
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(chemin) ?? AppContext.BaseDirectory
            });
        }
        catch (Exception ex)
        {
            attente.Close();

            MessageBox.Show(this,
                $"Impossible de lancer le service :{Environment.NewLine}{ex.Message}",
                "MelTime", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        try
        {
            for (var essai = 0; essai < 30; essai++)
            {
                await Task.Delay(1000);

                if (await ServiceDisponibleAsync())
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            attente.Close();
        }
    }

    private void AssurerTray()
    {
        if (InstanceUnique.EstEnCours("MelTime.Tray"))
        {
            return;
        }

        var chemin = TachesPlanifiees.Localiser("MelTime.Tray.exe");

        if (chemin is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(chemin)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(chemin) ?? AppContext.BaseDirectory
            });
        }
        catch (Exception)
        {
            // Sans Tray, l'administration reste utilisable.
        }
    }

    private async Task SurveillerServiceAsync()
    {
        if (_occupe)
        {
            return;
        }

        try
        {
            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var reponse = await _pipe.EnvoyerAsync(
                new Requete { Commande = Commandes.ServiceVivant },
                limite.Token);

            _echecsService = reponse.Succes ? 0 : _echecsService + 1;
        }
        catch (Exception)
        {
            _echecsService++;
        }

        if (_echecsService < 2)
        {
            return;
        }

        // Le service a disparu : on se ferme sans rien demander.
        _surveillance.Stop();
        Application.Exit();
    }

    // ------------------------------------------------------------------
    // Réseau
    // ------------------------------------------------------------------

    private async Task ChargerConfigurationAsync() => await ExecuterAsync(LireConfigurationAsync);

    /// <summary>Lecture de la configuration. A n'appeler qu'a l'interieur d'un ExecuterAsync.</summary>
    private async Task LireConfigurationAsync()
    {
        var reponse = await _pipe.EnvoyerAsync(new Requete { Commande = Commandes.ConfigurationLire });

        if (!reponse.Succes || reponse.Configuration is null)
        {
            IndiquerReseau("Configuration illisible.", true);
            return;
        }

        _champPort.Value = Math.Clamp(reponse.Configuration.Port, 1, 65535);
        _configurationServeur = reponse.Configuration;

        IndiquerReseau(
            $"En écoute sur le port {reponse.Configuration.Port} ({reponse.Configuration.Hote}).",
            false);
    }

    private async Task AppliquerPortAsync()
    {
        var port = (int)_champPort.Value;

        // On repart de la configuration lue : envoyer le seul port ecraserait
        // l'hote d'ecoute et la duree de session par leurs valeurs par defaut.
        var configuration = _configurationServeur ?? new ConfigurationServeur();
        configuration.Port = port;

        await ExecuterAsync(async () =>
        {
            var reponse = await _pipe.EnvoyerAsync(new Requete
            {
                Commande = Commandes.ConfigurationEcrire,
                Configuration = configuration
            });

            if (!reponse.Succes)
            {
                IndiquerReseau(reponse.Erreur ?? "Enregistrement impossible.", true);
                return;
            }

            _configurationServeur = reponse.Configuration ?? configuration;

            // Le pare-feu de l'installeur ne connait que le port 5080 :
            // sans cette ouverture, un nouveau port resterait bloque.
            OuvrirPortDansPareFeu(port);

            IndiquerReseau($"Port {port} enregistré.", false);

            var redemarrer = MessageBox.Show(this,
                $"Le port {port} sera actif après redémarrage du service."
                + Environment.NewLine + Environment.NewLine
                + "Redémarrer le service maintenant ?"
                + Environment.NewLine
                + "(l'administration et l'icône resteront en place)",
                "Redémarrage nécessaire",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1);

            if (redemarrer == DialogResult.Yes)
            {
                await RedemarrerServiceAsync();
            }
            else
            {
                IndiquerReseau(
                    $"Port {port} enregistré. Redémarrez le service pour l'appliquer.",
                    false);
            }
        });
    }

    /// <summary>
    /// Redemarre le service sans fermer l'administration.
    /// La surveillance est suspendue le temps de l'operation : sans cela,
    /// elle conclurait a une panne et fermerait la fenetre.
    /// </summary>
    private async Task RedemarrerServiceAsync()
    {
        _surveillance.Stop();
        _echecsService = 0;

        try
        {
            IndiquerReseau("Arrêt du service…", false);

            try
            {
                using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _pipe.EnvoyerAsync(new Requete { Commande = Commandes.ServiceArreter }, limite.Token);
            }
            catch (Exception)
            {
                // Le service peut disparaitre avant d'avoir repondu : c'est le but.
            }

            // Attendre que le pipe soit ferme.
            for (var essai = 0; essai < 20; essai++)
            {
                await Task.Delay(250);

                if (!await ServiceDisponibleAsync())
                {
                    break;
                }
            }

            var chemin = TachesPlanifiees.Localiser("MelTime.Service.exe");

            if (chemin is null)
            {
                IndiquerReseau("MelTime.Service.exe introuvable : démarrage manuel nécessaire.", true);
                return;
            }

            IndiquerReseau("Démarrage du service…", false);

            try
            {
                Process.Start(new ProcessStartInfo(chemin)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(chemin) ?? AppContext.BaseDirectory
                });
            }
            catch (Exception ex)
            {
                IndiquerReseau($"Démarrage impossible : {ex.Message}", true);
                return;
            }

            var demarre = false;

            for (var essai = 0; essai < 40; essai++)
            {
                await Task.Delay(500);

                if (await ServiceDisponibleAsync())
                {
                    demarre = true;
                    break;
                }
            }

            if (!demarre)
            {
                IndiquerReseau("Le service n'a pas répondu après redémarrage.", true);
                return;
            }

            await RechargerAsync();
            await LireConfigurationAsync();

            // Le Tray s'est ferme en meme temps que le service : on le fait revenir.
            await AssurerTrayApresRedemarrageAsync();

            IndiquerReseau($"Service redémarré, en écoute sur le port {_champPort.Value}.", false);
        }
        finally
        {
            _echecsService = 0;
            _surveillance.Start();
        }
    }

    /// <summary>
    /// Le Tray met quelques secondes a constater la disparition du service.
    /// On attend qu'il ait fini de se fermer avant de le relancer,
    /// sinon son verrou d'instance unique l'empecherait de redemarrer.
    /// </summary>
    private async Task AssurerTrayApresRedemarrageAsync()
    {
        for (var essai = 0; essai < 30 && InstanceUnique.EstEnCours("MelTime.Tray"); essai++)
        {
            await Task.Delay(500);
        }

        AssurerTray();
    }

    /// <summary>
    /// Ouvre le port dans le pare-feu Windows. L'administration etant elevee,
    /// l'operation reussit sans invite supplementaire.
    /// </summary>
    private static void OuvrirPortDansPareFeu(int port)
    {
        try
        {
            var arguments =
                $"advfirewall firewall add rule name=MelTime-Service-TCP-{port} dir=in action=allow protocol=TCP localport={port} profile=any";

            using var processus = Process.Start(new ProcessStartInfo("netsh.exe", arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false
            });

            processus?.WaitForExit(5000);
        }
        catch (Exception)
        {
            // Sans droits suffisants, on n'empeche pas l'enregistrement du port.
        }
    }

    private void IndiquerReseau(string texte, bool echec)
    {
        _etatReseau.Text = texte;
        _etatReseau.ForeColor = echec ? Color.FromArgb(0xC4, 0x2B, 0x1C) : SystemColors.GrayText;
    }

    // ------------------------------------------------------------------
    // Paramètres du serveur
    // ------------------------------------------------------------------

    private void RelireDemarrage()
    {
        // Une seule notion : MelTime démarre avec Windows, service et icône ensemble.
        _caseDemarrageAuto.Checked = TachesPlanifiees.Existe(TachesPlanifiees.NomService)
                                     || TachesPlanifiees.Existe(TachesPlanifiees.NomTray);

        IndiquerDemarrage("État relu depuis le Planificateur de tâches.", SystemColors.GrayText);
    }

    private void AppliquerDemarrage()
    {
        var messages = new List<string>();

        AppliquerTache(
            TachesPlanifiees.NomService,
            _caseDemarrageAuto.Checked,
            "MelTime.Service.exe",
            TachesPlanifiees.CreerService,
            messages);

        AppliquerTache(
            TachesPlanifiees.NomTray,
            _caseDemarrageAuto.Checked,
            "MelTime.Tray.exe",
            TachesPlanifiees.CreerTray,
            messages);

        // On relit l'état réel : si une opération a échoué, la case doit le montrer.
        _caseDemarrageAuto.Checked = TachesPlanifiees.Existe(TachesPlanifiees.NomService)
                                     || TachesPlanifiees.Existe(TachesPlanifiees.NomTray);

        var echec = messages.Any(m => m.Contains("Échec", StringComparison.Ordinal)
                                      || m.Contains("introuvable", StringComparison.Ordinal));

        IndiquerDemarrage(
            string.Join(Environment.NewLine, messages),
            echec ? Color.FromArgb(0xC4, 0x2B, 0x1C) : SystemColors.GrayText);
    }

    private static void AppliquerTache(
        string nom,
        bool souhaitee,
        string nomExecutable,
        CreateurTache creer,
        List<string> messages)
    {
        if (!souhaitee)
        {
            if (!TachesPlanifiees.Existe(nom))
            {
                messages.Add($"{nom} : déjà absente.");
                return;
            }

            TachesPlanifiees.Supprimer(nom, out var messageSuppression);
            messages.Add(messageSuppression);
            return;
        }

        var chemin = TachesPlanifiees.Localiser(nomExecutable);

        if (chemin is null)
        {
            messages.Add($"{nom} : {nomExecutable} est introuvable, tâche non créée.");
            return;
        }

        creer(chemin, out var messageCreation);
        messages.Add(messageCreation);
    }

    private void IndiquerDemarrage(string texte, Color couleur)
    {
        _etatDemarrage.Text = texte;
        _etatDemarrage.ForeColor = couleur;
    }

    private void MettreAJourEtatConnexion(bool actif)
    {
        _etatConnexion.Text = actif ? "● Service actif" : "● Service injoignable";
        _etatConnexion.ForeColor = actif
            ? Color.FromArgb(46, 160, 67)
            : Color.FromArgb(0xC4, 0x2B, 0x1C);

        _etatDonnees.Text = $"Données : {CheminDonneesAffiche}";
    }

    private static string CheminDonneesAffiche =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "MelTime");

    // ------------------------------------------------------------------
    // État des boutons
    // ------------------------------------------------------------------

    private Client? ClientSelectionne() => _grille.CurrentRow?.DataBoundItem as Client;

    private void MettreAJourBoutons()
    {
        var selection = ClientSelectionne();
        var disponible = !_occupe;

        _boutonNouveau.Enabled = disponible;
        _boutonActualiser.Enabled = disponible;
        _boutonModifier.Enabled = disponible && selection is not null;
        _boutonMotDePasse.Enabled = disponible && selection is not null;
        _boutonActiver.Enabled = disponible && selection is not null;
        _boutonSupprimer.Enabled = disponible && selection is not null;

        _boutonActiver.Text = selection is null
            ? "Activer / Désactiver"
            : selection.Actif ? "Désactiver" : "Activer";
    }

    // ------------------------------------------------------------------
    // Opérations sur les comptes
    // ------------------------------------------------------------------

    private async Task ActualiserAsync() => await ExecuterAsync(RechargerAsync);

    private async Task RechargerAsync()
    {
        var reponse = await _pipe.EnvoyerAsync(new Requete { Commande = Commandes.ClientsLister });

        if (!reponse.Succes)
        {
            Avertir(reponse.Erreur ?? "Le service a refusé la demande.");
            _etatService.Text = "Le service a refusé la demande.";
            return;
        }

        _clients = reponse.Clients ?? new List<Client>();
        _grille.DataSource = null;
        _grille.DataSource = _clients;
        _etatService.Text = $"Connecté au service — {_clients.Count} client(s).";

        MettreAJourEtatConnexion(true);
    }

    private async Task NouveauAsync()
    {
        using var formulaire = new FormulaireClient(null);
        if (formulaire.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var succes = false;

        await ExecuterAsync(async () =>
        {
            var reponse = await _pipe.EnvoyerAsync(new Requete
            {
                Commande = Commandes.ClientsCreer,
                Client = formulaire.Resultat,
                MotDePasse = formulaire.MotDePasseSaisi
            });

            if (!reponse.Succes)
            {
                Avertir(reponse.Erreur ?? "Création impossible.");
                return;
            }

            succes = true;
            await RechargerAsync();
        });

        if (succes)
        {
            _etatService.Text += "  Dernière action : création effectuée.";
        }
    }

    private async Task ModifierAsync()
    {
        var selection = ClientSelectionne();
        if (selection is null)
        {
            return;
        }

        using var formulaire = new FormulaireClient(selection);
        if (formulaire.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await ExecuterAsync(async () =>
        {
            var reponse = await _pipe.EnvoyerAsync(new Requete
            {
                Commande = Commandes.ClientsModifier,
                Client = formulaire.Resultat
            });

            if (!reponse.Succes)
            {
                Avertir(reponse.Erreur ?? "Modification impossible.");
                return;
            }

            await RechargerAsync();
        });
    }

    private async Task ChangerMotDePasseAsync()
    {
        var selection = ClientSelectionne();
        if (selection is null)
        {
            return;
        }

        using var formulaire = new FormulaireMotDePasse(selection);
        if (formulaire.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await ExecuterAsync(async () =>
        {
            var reponse = await _pipe.EnvoyerAsync(new Requete
            {
                Commande = Commandes.ClientsChangerMotDePasse,
                Id = selection.Id,
                MotDePasse = formulaire.MotDePasseSaisi
            });

            if (!reponse.Succes)
            {
                Avertir(reponse.Erreur ?? "Changement de mot de passe impossible.");
                return;
            }

            MessageBox.Show(this,
                $"Le mot de passe de « {selection.Identifiant} » a été modifié.",
                "MelTime", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    private async Task BasculerActifAsync()
    {
        var selection = ClientSelectionne();
        if (selection is null)
        {
            return;
        }

        var cible = new Client
        {
            Id = selection.Id,
            Identifiant = selection.Identifiant,
            Nom = selection.Nom,
            Prenom = selection.Prenom,
            Actif = !selection.Actif,
            ContratHebdomadaireMinutes = selection.ContratHebdomadaireMinutes
        };

        await ExecuterAsync(async () =>
        {
            var reponse = await _pipe.EnvoyerAsync(new Requete
            {
                Commande = Commandes.ClientsModifier,
                Client = cible
            });

            if (!reponse.Succes)
            {
                Avertir(reponse.Erreur ?? "Modification impossible.");
                return;
            }

            await RechargerAsync();
        });
    }

    private async Task SupprimerAsync()
    {
        var selection = ClientSelectionne();
        if (selection is null)
        {
            return;
        }

        var confirmation = MessageBox.Show(this,
            $"Supprimer définitivement le client « {selection.Identifiant} » ({selection.NomComplet}) ?"
            + Environment.NewLine + Environment.NewLine
            + "Ses feuilles de temps seront archivées, pas effacées."
            + Environment.NewLine
            + "Pour suspendre un accès sans perdre les données, utilisez plutôt « Désactiver ».",
            "Confirmer la suppression",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        await ExecuterAsync(async () =>
        {
            var reponse = await _pipe.EnvoyerAsync(new Requete
            {
                Commande = Commandes.ClientsSupprimer,
                Id = selection.Id
            });

            if (!reponse.Succes)
            {
                Avertir(reponse.Erreur ?? "Suppression impossible.");
                return;
            }

            await RechargerAsync();
        });
    }

    // ------------------------------------------------------------------
    // Exécution protégée d'un échange avec le service
    // ------------------------------------------------------------------

    private async Task ExecuterAsync(Func<Task> action)
    {
        _occupe = true;
        MettreAJourBoutons();
        UseWaitCursor = true;

        try
        {
            await action();
        }
        catch (TimeoutException ex)
        {
            _etatService.Text = "Service injoignable.";
            MettreAJourEtatConnexion(false);

            MessageBox.Show(this,
                ex.Message + Environment.NewLine + Environment.NewLine
                + "Vérifiez que MelTime.Service est démarré.",
                "MelTime", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (UnauthorizedAccessException)
        {
            _etatService.Text = "Accès refusé par le service.";

            MessageBox.Show(this,
                "L'accès au service a été refusé." + Environment.NewLine
                + "Cette fenêtre doit être lancée en tant qu'administrateur.",
                "MelTime", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (IOException ex)
        {
            _etatService.Text = "Communication interrompue.";

            MessageBox.Show(this,
                "Communication impossible avec le service :" + Environment.NewLine + ex.Message,
                "MelTime", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _occupe = false;
            UseWaitCursor = false;
            MettreAJourBoutons();
        }
    }

    private void Avertir(string message) =>
        MessageBox.Show(this, message, "MelTime", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
