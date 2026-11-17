namespace MelTime.UI;

/// <summary>
/// Petite fenêtre d'attente affichée pendant le démarrage du service.
/// Elle ne fait rien d'autre que rassurer l'utilisateur : le travail réel
/// est mené par la fenêtre principale.
/// </summary>
public sealed class FormulaireAttenteService : Form
{
    public FormulaireAttenteService()
    {
        Text = "MelTime";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ControlBox = false;
        ClientSize = new Size(420, 120);
        Font = new Font("Segoe UI", 9F);

        var message = new Label
        {
            Text = "Démarrage du service MelTime…",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Location = new Point(20, 24),
            Size = new Size(380, 24)
        };

        var progression = new ProgressBar
        {
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30,
            Location = new Point(20, 60),
            Size = new Size(380, 16)
        };

        Controls.Add(message);
        Controls.Add(progression);
    }
}
