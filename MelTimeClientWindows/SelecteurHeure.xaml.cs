using System.Globalization;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MelTime.ClientWindows;

/// <summary>
/// Saisie d'une heure au format HH:mm.
/// WPF ne fournit aucun controle d'heure : celui-ci combine une zone de texte
/// libre et deux fleches pour ajuster par quart d'heure.
/// Champ vide = aucune heure saisie, ce qui correspond a « --:-- » dans la capture.
/// </summary>
public partial class SelecteurHeure : UserControl
{
    private TimeOnly? _heure;
    private bool _rafraichissement;

    public SelecteurHeure()
    {
        InitializeComponent();
        Rafraichir();
    }

    /// <summary>Survient lorsque la valeur change reellement.</summary>
    public event EventHandler? HeureModifiee;

    /// <summary>
    /// Contour qui signale la nature de la journee : rouge pour un arret,
    /// bleu pour un conge, transparent le reste du temps. Il ne depend pas
    /// du contenu du champ : la marque reste visible meme vide.
    /// </summary>
    public Brush? Contour
    {
        set => CadreContour.BorderBrush = value ?? Brushes.Transparent;
    }

    /// <summary>Heure saisie, ou null si le champ est vide.</summary>
    public TimeOnly? Heure
    {
        get => _heure;
        set
        {
            _heure = value;
            Rafraichir();
        }
    }

    /// <summary>Force la prise en compte du texte saisi. A appeler avant toute lecture.</summary>
    public void Valider()
    {
        ValiderSaisie();
    }

    private void Rafraichir()
    {
        _rafraichissement = true;
        Champ.Text = _heure?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "--:--";
        _rafraichissement = false;
    }

    private void Champ_LostFocus(object sender, System.Windows.RoutedEventArgs e) => ValiderSaisie();

    private void Champ_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ValiderSaisie();
            Keyboard.ClearFocus();
        }
    }

    private void BoutonPlus_Click(object sender, System.Windows.RoutedEventArgs e) => Ajuster(15);

    private void BoutonMoins_Click(object sender, System.Windows.RoutedEventArgs e) => Ajuster(-15);

    private void Ajuster(int minutes)
    {
        var depart = _heure ?? new TimeOnly(8, 0);
        var total = depart.ToTimeSpan().Add(TimeSpan.FromMinutes(minutes));

        if (total < TimeSpan.Zero)
        {
            total = TimeSpan.Zero;
        }
        else if (total >= TimeSpan.FromHours(24))
        {
            total = TimeSpan.FromHours(24) - TimeSpan.FromMinutes(1);
        }

        Definir(TimeOnly.FromTimeSpan(total));
    }

    private void ValiderSaisie()
    {
        if (_rafraichissement)
        {
            return;
        }

        var texte = Champ.Text.Trim();

        if (texte.Length == 0 || texte == "--:--")
        {
            Definir(null);
            return;
        }

        if (TimeOnly.TryParse(texte, CultureInfo.CurrentCulture, DateTimeStyles.None, out var heure)
            || TimeOnly.TryParseExact(texte, "HHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out heure))
        {
            Definir(heure);
            return;
        }

        // Saisie illisible : on revient a la valeur precedente plutot que d'inventer.
        Rafraichir();
    }

    private void Definir(TimeOnly? heure)
    {
        var change = !Nullable.Equals(_heure, heure);
        _heure = heure;
        Rafraichir();

        if (change)
        {
            HeureModifiee?.Invoke(this, EventArgs.Empty);
        }
    }
}
