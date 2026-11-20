using MelTime.Core.Modeles;
using MelTime.Core.Securite;

namespace MelTime.UI;

/// <summary>
/// Boîte de dialogue de création ou de modification d'un compte client.
/// En modification, le mot de passe n'est pas affiché : il se change par le bouton dédié.
/// </summary>
public sealed class FormulaireClient : Form
{
    private readonly TextBox _identifiant = new();
    private readonly TextBox _nom = new();
    private readonly TextBox _prenom = new();
    private readonly NumericUpDown _contratHeures = new() { Minimum = 0, Maximum = 60, Width = 60, Value = 35 };
    private readonly NumericUpDown _contratMinutes = new() { Minimum = 0, Maximum = 45, Increment = 15, Width = 60 };
    private readonly ComboBox _pays = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };

    /// <summary>
    /// Premier jour du contrat. La case a cocher du controle permet de laisser
    /// la date vide, pour les clients dont on ne la connait pas.
    /// </summary>
    private readonly DateTimePicker _debutContrat = new()
    {
        Format = DateTimePickerFormat.Short,
        ShowCheckBox = true,
        Width = 200
    };

    /// <summary>Element de la liste des pays : separe la valeur technique de son libelle.</summary>
    private sealed record ItemPays(Pays Valeur, string Libelle)
    {
        public override string ToString() => Libelle;
    }
    private readonly TextBox _motDePasse = new() { UseSystemPasswordChar = true };
    private readonly TextBox _confirmation = new() { UseSystemPasswordChar = true };

    /// <summary>Devoile les deux champs de mot de passe pendant la saisie.</summary>
    private readonly CheckBox _afficherMotsDePasse = new()
    {
        Text = "Afficher les mots de passe",
        AutoSize = true
    };
    private readonly CheckBox _actif = new() { Text = "Compte actif", AutoSize = true, Checked = true };

    private readonly int _id;
    private readonly bool _creation;

    private int _y = 18;

    public FormulaireClient(Client? client)
    {
        _creation = client is null;
        _id = client?.Id ?? 0;

        Icon = IconeApplication.Obtenir();

        Text = _creation ? "Nouveau client" : "Modifier le client";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9F);

        AjouterChamp("Identifiant", _identifiant);
        AjouterChamp("Nom", _nom);
        AjouterChamp("Prénom", _prenom);
        AjouterChamp("Contrat / semaine", ConstruireContrat());
        AjouterChamp("Pays", ConstruirePays());
        AjouterChamp("Début de contrat", _debutContrat);

        if (_creation)
        {
            AjouterChamp("Mot de passe", _motDePasse);
            AjouterChamp("Confirmation", _confirmation);
            AjouterCaseAfficher();
        }

        _actif.Location = new Point(150, _y);
        _y += 32;
        Controls.Add(_actif);

        if (client is not null)
        {
            _identifiant.Text = client.Identifiant;
            _nom.Text = client.Nom;
            _prenom.Text = client.Prenom;
            _actif.Checked = client.Actif;
            _contratHeures.Value = client.ContratHebdomadaireMinutes / 60;
            _contratMinutes.Value = client.ContratHebdomadaireMinutes % 60;

            if (client.DebutContrat is { } debut)
            {
                _debutContrat.Value = debut.ToDateTime(TimeOnly.MinValue);
                _debutContrat.Checked = true;
            }

            foreach (var item in _pays.Items.OfType<ItemPays>())
            {
                if (item.Valeur == client.Pays)
                {
                    _pays.SelectedItem = item;
                    break;
                }
            }
        }

        AjouterBoutons();

        ClientSize = new Size(480, _y + 60);
    }

    /// <summary>Le client saisi. Renseigne uniquement si le dialogue est valide.</summary>
    public Client Resultat { get; private set; } = new();

    /// <summary>Le mot de passe saisi, en création uniquement.</summary>
    public string? MotDePasseSaisi { get; private set; }

    private FlowLayoutPanel ConstruireContrat()
    {
        var zone = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = Padding.Empty
        };

        zone.Controls.Add(_contratHeures);
        zone.Controls.Add(new Label { Text = "h", AutoSize = true, Margin = new Padding(4, 6, 12, 0) });
        zone.Controls.Add(_contratMinutes);
        zone.Controls.Add(new Label { Text = "min", AutoSize = true, Margin = new Padding(4, 6, 0, 0) });

        return zone;
    }

    /// <summary>
    /// Liste des pays. Le pays determine les jours feries appliques :
    /// la France les connait, le Royaume-Uni n'en a pas encore.
    /// </summary>
    private ComboBox ConstruirePays()
    {
        _pays.Items.Add(new ItemPays(Pays.France, "France"));
        _pays.Items.Add(new ItemPays(Pays.RoyaumeUni, "Royaume-Uni (sans jours fériés)"));
        _pays.SelectedIndex = 0;

        return _pays;
    }

    private void AjouterChamp(string libelle, Control champ)
    {
        var etiquette = new Label
        {
            Text = libelle,
            AutoSize = true,
            Location = new Point(18, _y + 4)
        };

        champ.Location = new Point(150, _y);
        champ.Width = 300;

        Controls.Add(etiquette);
        Controls.Add(champ);

        _y += 32;
    }

    /// <summary>
    /// Devoile les deux champs de mot de passe. A la creation d'un compte,
    /// l'administrateur doit pouvoir relire ce qu'il tape avant d'enregistrer :
    /// une fois valide, le mot de passe n'est plus consultable, seule son
    /// empreinte PBKDF2 est conservee.
    /// </summary>
    private void AjouterCaseAfficher()
    {
        _afficherMotsDePasse.Location = new Point(150, _y);

        _afficherMotsDePasse.CheckedChanged += (_, _) =>
        {
            var masque = !_afficherMotsDePasse.Checked;

            _motDePasse.UseSystemPasswordChar = masque;
            _confirmation.UseSystemPasswordChar = masque;
        };

        Controls.Add(_afficherMotsDePasse);

        _y += 28;
    }

    private void AjouterBoutons()
    {
        var boutonOk = new Button
        {
            Text = "Enregistrer",
            Width = 110,
            Height = 30,
            Location = new Point(ClientSize.Width, _y),
            FlatStyle = FlatStyle.System
        };

        var boutonAnnuler = new Button
        {
            Text = "Annuler",
            Width = 100,
            Height = 30,
            DialogResult = DialogResult.Cancel,
            FlatStyle = FlatStyle.System
        };

        // Positionnement calé sur la largeur finale de la fenêtre.
        boutonOk.Location = new Point(480 - 18 - 110, _y);
        boutonAnnuler.Location = new Point(boutonOk.Left - 8 - 100, _y);

        boutonOk.Click += (_, _) => ValiderEtFermer();

        Controls.Add(boutonOk);
        Controls.Add(boutonAnnuler);

        AcceptButton = boutonOk;
        CancelButton = boutonAnnuler;

        _y += 44;
    }

    private void ValiderEtFermer()
    {
        var client = new Client
        {
            Id = _id,
            Identifiant = _identifiant.Text.Trim(),
            Nom = _nom.Text.Trim(),
            Prenom = _prenom.Text.Trim(),
            Actif = _actif.Checked,
            ContratHebdomadaireMinutes = (int)_contratHeures.Value * 60 + (int)_contratMinutes.Value,
            DebutContrat = _debutContrat.Checked
                ? DateOnly.FromDateTime(_debutContrat.Value)
                : null,
            Pays = (_pays.SelectedItem as ItemPays)?.Valeur ?? Pays.France
        };

        var erreurs = client.Valider().ToList();

        if (_creation)
        {
            if (_motDePasse.Text.Length < MotDePasse.LongueurMinimale)
            {
                erreurs.Add($"Le mot de passe doit compter au moins {MotDePasse.LongueurMinimale} caractères.");
            }
            else if (_motDePasse.Text != _confirmation.Text)
            {
                erreurs.Add("Les deux mots de passe ne correspondent pas.");
            }
        }

        if (erreurs.Count > 0)
        {
            MessageBox.Show(this,
                string.Join(Environment.NewLine, erreurs),
                "Saisie incomplète",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        Resultat = client;
        MotDePasseSaisi = _creation ? _motDePasse.Text : null;

        DialogResult = DialogResult.OK;
        Close();
    }
}
