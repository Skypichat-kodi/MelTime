using MelTime.Core.Modeles;
using MelTime.Core.Securite;

namespace MelTime.UI;

/// <summary>
/// Boîte de dialogue de changement de mot de passe.
/// L'ancien mot de passe n'est pas demande : l'administrateur agit sur le compte d'autrui.
/// </summary>
public sealed class FormulaireMotDePasse : Form
{
    private readonly TextBox _motDePasse = new() { UseSystemPasswordChar = true };
    private readonly TextBox _confirmation = new() { UseSystemPasswordChar = true };

    private int _y = 18;

    public FormulaireMotDePasse(Client client)
    {
        ArgumentNullException.ThrowIfNull(client);

        Text = "Nouveau mot de passe";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9F);

        var rappel = new Label
        {
            Text = $"Compte : {client.Identifiant} ({client.NomComplet})",
            AutoSize = true,
            Location = new Point(18, _y)
        };
        Controls.Add(rappel);
        _y += 30;

        AjouterChamp("Mot de passe", _motDePasse);
        AjouterChamp("Confirmation", _confirmation);

        AjouterBoutons();

        ClientSize = new Size(480, _y + 60);
    }

    /// <summary>Le mot de passe saisi. Renseigne uniquement si le dialogue est valide.</summary>
    public string MotDePasseSaisi { get; private set; } = string.Empty;

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

    private void AjouterBoutons()
    {
        var boutonOk = new Button
        {
            Text = "Valider",
            Width = 110,
            Height = 30,
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
        var erreurs = new List<string>();

        if (_motDePasse.Text.Length < MotDePasse.LongueurMinimale)
        {
            erreurs.Add($"Le mot de passe doit compter au moins {MotDePasse.LongueurMinimale} caractères.");
        }
        else if (_motDePasse.Text != _confirmation.Text)
        {
            erreurs.Add("Les deux mots de passe ne correspondent pas.");
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

        MotDePasseSaisi = _motDePasse.Text;
        DialogResult = DialogResult.OK;
        Close();
    }
}
