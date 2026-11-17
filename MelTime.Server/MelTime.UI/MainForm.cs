using MelTime.Core.Modeles;
using MelTime.Core.Protocole;

namespace MelTime.UI;

/// <summary>
/// Fenêtre principale de l'administration : liste des comptes clients.
/// Toutes les opérations passent par le service via le pipe nommé ;
/// cette fenêtre ne touche jamais directement la base.
/// </summary>
public sealed class MainForm : Form
{
    private readonly ClientPipe _pipe = new();

    private readonly DataGridView _grille = new();
    private readonly Button _boutonNouveau = new() { Text = "Nouveau", Width = 110, Height = 30 };
    private readonly Button _boutonModifier = new() { Text = "Modifier", Width = 110, Height = 30 };
    private readonly Button _boutonMotDePasse = new() { Text = "Mot de passe", Width = 120, Height = 30 };
    private readonly Button _boutonActiver = new() { Text = "Activer / Désactiver", Width = 150, Height = 30 };
    private readonly Button _boutonSupprimer = new() { Text = "Supprimer", Width = 110, Height = 30 };
    private readonly Button _boutonActualiser = new() { Text = "Actualiser", Width = 110, Height = 30 };
    private readonly ToolStripStatusLabel _etatService = new() { Text = "Connexion au service…" };

    private List<Client> _clients = new();
    private bool _occupe;

    public MainForm()
    {
        Text = "MelTime — Administration du serveur";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1000, 560);
        MinimumSize = new Size(880, 460);
        Font = new Font("Segoe UI", 9F);

        ConstruireInterface();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _ = ActualiserAsync();
    }

    // ------------------------------------------------------------------
    // Construction de l'interface
    // ------------------------------------------------------------------

    private void ConstruireInterface()
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

        var barreEtat = new StatusStrip();
        barreEtat.Items.Add(_etatService);

        Controls.Add(_grille);
        Controls.Add(barreOutils);
        Controls.Add(barreEtat);

        MettreAJourBoutons();
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
        _grille.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

        _grille.Columns.Add(CreerColonneTexte(nameof(Client.Identifiant), "Identifiant", 22));
        _grille.Columns.Add(CreerColonneTexte(nameof(Client.Nom), "Nom", 22));
        _grille.Columns.Add(CreerColonneTexte(nameof(Client.Prenom), "Prénom", 22));
        _grille.Columns.Add(CreerColonneTexte(nameof(Client.ContratAffiche), "Contrat", 12));
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
    // Etat des boutons
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
    // Operations
    // ------------------------------------------------------------------

    private async Task ActualiserAsync() => await ExecuterAsync(RechargerAsync);

    /// <summary>Recharge la liste depuis le service. A n'appeler qu'a l'interieur d'un ExecuterAsync.</summary>
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
            Actif = !selection.Actif
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
            + "Cette action est irréversible."
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
    // Execution protegee d'un echange avec le service
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
