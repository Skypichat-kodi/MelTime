using MelTime.Core.Agenda;
using MelTime.Core.Api;
using MelTime.Core.Modeles;

namespace MelTimeClientAndroid;

/// <summary>
/// Feuille de temps hebdomadaire.
///
/// La grille est en lecture seule : un appui sur une journee ouvre le panneau
/// d'edition, ou quatre selecteurs natifs Android remplacent les champs texte
/// du client Windows, impraticables au doigt.
/// La validation d'une journee est envoyee immediatement au serveur.
/// </summary>
public partial class PageFeuilleTemps : ContentPage
{
    private static readonly string[] NomsJours =
    {
        "Lundi", "Mardi", "Mercredi", "Jeudi", "Vendredi", "Samedi", "Dimanche"
    };

    private static readonly Color CouleurFondFerie = Color.FromArgb("#5A2B2B");
    private static readonly Color CouleurFondWeekend = Color.FromArgb("#2A5238");
    private static readonly Color CouleurFondMaladie = Color.FromArgb("#4A2B2B");
    private static readonly Color CouleurFondConge = Color.FromArgb("#1B3A52");

    private readonly ClientApi _api;
    private readonly SessionAndroid _session = new();

    private readonly Label[] _etiquettesJours = new Label[7];
    private readonly Label[][] _cellulesValeurs = new Label[7][];
    private readonly Label[] _travaille = new Label[7];
    private readonly Border[] _lignes = new Border[7];

    private SemaineApi _semaine = new();
    private bool _occupation;

    /// <summary>L'accueil n'est propose qu'une fois par session.</summary>
    private bool _accueilFait;

    public PageFeuilleTemps(ClientApi api, Client profil)
    {
        InitializeComponent();

        _api = api;
        Profil = profil;

        TexteUtilisateur.Text = $"{profil.Prenom} {profil.Nom}".Trim();

        ConstruireGrille();

        Loaded += async (_, _) =>
        {
            NotificationsMelTime.CreerCanal();
            await NotificationsMelTime.DemanderAutorisationAsync();
            await ChargerSemaineCouranteAsync();
        };
    }

    public Client Profil { get; }

    /// <summary>La feuille de temps exige la largeur du paysage.</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        OrientationEcran.Paysage();

        // L'accueil attend que la page soit affichee : lance depuis le
        // chargement, il se heurtait a la navigation encore en cours.
        _ = AccueillirAsync();
    }

    /// <summary>
    /// Accompagne un client qui decouvre l'application : sans salaires ni
    /// horaires types, les calculs resteraient vides sans qu'il comprenne
    /// pourquoi. On l'emmene donc directement la ou il faut saisir.
    /// </summary>
    private async Task AccueillirAsync()
    {
        if (_accueilFait)
        {
            return;
        }

        _accueilFait = true;

        // Laisse l'ecran s'installer avant d'ouvrir quoi que ce soit.
        await Task.Delay(400);

        // Les calendriers ne sont proposes qu'une fois les salaires enregistres :
        // sans eux, les calculs de gains resteraient vides.
        if (!await DemanderSalairesAsync())
        {
            return;
        }

        await DemanderCalendriersAsync();
    }

    /// <summary>
    /// Propose la saisie des salaires. Retourne vrai s'ils sont renseignes,
    /// en tenant compte de ce qui vient d'etre enregistre dans la page.
    /// </summary>
    private async Task<bool> DemanderSalairesAsync()
    {
        try
        {
            var reglages = await _api.ReglagesAsync();

            if (SalaireComplet(reglages))
            {
                return true;
            }

            await DisplayAlertAsync(
                "Bienvenue dans MelTime",
                "Commençons par vos salaires. Les montants brut et net servent à calculer ce que vous gagnez, "
                + "sur la semaine comme sur l'année. Ils restent sur votre compte et ne sont visibles par personne d'autre.",
                "D'accord");

            // L'alerte doit se refermer avant que la page ne s'ouvre.
            await Task.Delay(200);

            await Navigation.PushModalAsync(new PageReglages(_api, Profil));

            // La page vient d'etre fermee : on regarde ce qui a ete enregistre.
            reglages = await _api.ReglagesAsync();

            return SalaireComplet(reglages);
        }
        catch (Exception ex)
        {
            // Un accueil rate ne doit pas empecher d'utiliser l'application.
            System.Diagnostics.Debug.WriteLine("Accueil salaires : " + ex.Message);
            return false;
        }
    }

    private static bool SalaireComplet(ReglagesApi reglages) =>
        reglages.SalaireBrut is > 0 && reglages.SalaireNet is > 0;

    private async Task DemanderCalendriersAsync()
    {
        try
        {
            var types = await _api.HorairesDefautAsync();

            // Un modele suffit : il doit etre actif et porter au moins une journee.
            var renseigne = types.Modeles.Any(m => m.Actif && m.Jours.Any(j => !string.IsNullOrWhiteSpace(j.Arrivee)));

            if (renseigne)
            {
                return;
            }

            await DisplayAlertAsync(
                "Vos horaires habituels",
                "Dernière étape : définissez vos horaires de travail. Ils préremplissent automatiquement vos semaines, "
                + "et alimentent les calculs d'heures. Vous pouvez créer jusqu'à cinq modèles qui alternent d'une semaine à l'autre.",
                "D'accord");

            await Task.Delay(200);

            await Navigation.PushModalAsync(new PageHorairesDefaut(_api));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Accueil calendriers : " + ex.Message);
        }
    }

    // ------------------------------------------------------------------
    // Construction de la grille
    // ------------------------------------------------------------------

    private void ConstruireGrille()
    {
        GrilleJours.ColumnDefinitions.Clear();
        GrilleJours.RowDefinitions.Clear();

        GrilleJours.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });

        for (var index = 0; index < 4; index++)
        {
            GrilleJours.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        GrilleJours.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var entetes = new[] { "Jour", "Arrivée", "Pause début", "Pause fin", "Départ", "Travaillé" };

        GrilleJours.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (var colonne = 0; colonne < entetes.Length; colonne++)
        {
            var estTravaille = colonne == 5;

            AjouterCellule(0, colonne, new Label
            {
                Text = entetes[colonne],
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = estTravaille ? Color.FromArgb("#0078D4") : Color.FromArgb("#A0A0A0"),
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = estTravaille ? LayoutOptions.Center : LayoutOptions.Start
            }, Color.FromArgb("#303030"));
        }

        for (var jour = 0; jour < 7; jour++)
        {
            GrilleJours.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var ligne = jour + 1;
            var fond = jour % 2 == 1 ? Color.FromArgb("#2B2B2B") : Colors.Transparent;

            _etiquettesJours[jour] = new Label
            {
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#F2F2F2"),
                VerticalOptions = LayoutOptions.Center
            };

            _lignes[jour] = AjouterCellule(ligne, 0, _etiquettesJours[jour], fond);

            _cellulesValeurs[jour] = new Label[4];

            for (var champ = 0; champ < 4; champ++)
            {
                _cellulesValeurs[jour][champ] = new Label
                {
                    Text = "--:--",
                    FontSize = 14,
                    TextColor = Color.FromArgb("#F2F2F2"),
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.Center
                };

                AjouterCellule(ligne, champ + 1, _cellulesValeurs[jour][champ], fond);
            }

            _travaille[jour] = new Label
            {
                Text = "—",
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#0078D4"),
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center
            };

            AjouterCellule(ligne, 5, _travaille[jour], fond);

            var index = jour;
            var geste = new TapGestureRecognizer();
            geste.Tapped += async (_, _) => await ModifierJourAsync(index);
            _lignes[jour].GestureRecognizers.Add(geste);
        }
    }

    private Border AjouterCellule(int ligne, int colonne, View contenu, Color fond)
    {
        var cellule = new Border
        {
            BackgroundColor = fond,
            Stroke = Color.FromArgb("#3D3D3D"),
            StrokeThickness = 0.5,
            Padding = new Thickness(6, 2),
            Content = contenu
        };

        // Le geste porte aussi sur les cellules, pour que toute la ligne reponde.
        var index = ligne - 1;
        if (index >= 0 && index < 7)
        {
            var geste = new TapGestureRecognizer();
            geste.Tapped += async (_, _) => await ModifierJourAsync(index);
            cellule.GestureRecognizers.Add(geste);
        }

        Grid.SetRow(cellule, ligne);
        Grid.SetColumn(cellule, colonne);
        GrilleJours.Children.Add(cellule);

        return cellule;
    }

    // ------------------------------------------------------------------
    // Chargement et affichage
    // ------------------------------------------------------------------

    private async Task ChargerSemaineCouranteAsync()
    {
        var (anneeIso, numero) = SemaineTravail.SemaineIsoDe(DateOnly.FromDateTime(DateTime.Today));
        await ChargerSemaineAsync(anneeIso, numero);
    }

    private async Task ChargerSemaineAsync(int anneeIso, int numero)
    {
        if (_occupation)
        {
            return;
        }

        await ExecuterAsync(async () =>
        {
            _semaine = await _api.ChargerSemaineAsync(anneeIso, numero);
            AfficherSemaine();
            PlanifierAlertes();
        }, "Chargement…");
    }

    /// <summary>
    /// Repose les rappels d'horaires. Ils ne concernent que la semaine en
    /// cours : consulter une semaine passee ne doit pas declencher d'alerte.
    /// </summary>
    private void PlanifierAlertes()
    {
        try
        {
            var maintenant = DateTime.Now;
            var aujourdhui = maintenant.ToString(SemaineApi.FormatDate, System.Globalization.CultureInfo.InvariantCulture);

            if (!_semaine.Jours.Any(j => j.Date == aujourdhui))
            {
                return;
            }

            NotificationsMelTime.CreerCanal();
            AlertesHoraires.Planifier(AlertesHoraires.Calculer(_semaine, maintenant));

            // Sans ce reveil, les alertes ne seraient reposees qu'a la prochaine
            // ouverture de l'application.
            Replanificateur.PlanifierReveilQuotidien();
        }
        catch (Exception ex)
        {
            // Les rappels sont un confort : leur echec ne doit rien interrompre.
            System.Diagnostics.Debug.WriteLine("Planification des alertes : " + ex.Message);
        }
    }

    private void AfficherSemaine()
    {
        for (var jour = 0; jour < 7; jour++)
        {
            var donnee = jour < _semaine.Jours.Count ? _semaine.Jours[jour] : null;

            var date = string.Empty;
            if (donnee is not null
                && DateOnly.TryParseExact(donnee.Date, SemaineApi.FormatDate, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var valeur))
            {
                date = valeur.ToString("dd/MM");
            }

            var mention = donnee?.Maladie == true ? " (maladie)"
                : donnee?.Conge == true ? " (congé)"
                : string.Empty;

            _etiquettesJours[jour].Text = $"{NomsJours[jour]} {date}{mention}";

            _cellulesValeurs[jour][0].Text = donnee?.Arrivee ?? "--:--";
            _cellulesValeurs[jour][1].Text = donnee?.PauseDebut ?? "--:--";
            _cellulesValeurs[jour][2].Text = donnee?.PauseFin ?? "--:--";
            _cellulesValeurs[jour][3].Text = donnee?.Depart ?? "--:--";
            _travaille[jour].Text = donnee?.Travaille ?? "—";

            var fond = FondDeLaLigne(jour, donnee);
            AppliquerFond(jour, fond);
        }

        var lundi = _semaine.Jours.FirstOrDefault()?.Date ?? string.Empty;
        var dimanche = _semaine.Jours.LastOrDefault()?.Date ?? string.Empty;
        // Libelle court : l'annee est portee par la semaine ISO, et le texte
        // complet debordait de sa cellule en paysage.
        TexteSemaine.Text = $"S{_semaine.Numero} · {Court(lundi)} → {Court(dimanche)}";
        TexteTotalSemaine.Text = _semaine.TotalTravaille;

        TexteStatut.Text = string.Empty;
    }

    /// <summary>Convertit une duree « hh:mm » du service en TimeSpan.</summary>
    private static TimeSpan? EnDuree(string? texte)
    {
        if (string.IsNullOrWhiteSpace(texte))
        {
            return null;
        }

        var parties = texte.Split(':');

        if (parties.Length != 2
            || !int.TryParse(parties[0], out var heures)
            || !int.TryParse(parties[1], out var minutes))
        {
            return null;
        }

        return new TimeSpan(heures, minutes, 0);
    }

    private static string Court(string dateIso) =>
        DateOnly.TryParseExact(dateIso, SemaineApi.FormatDate, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d)
            ? d.ToString("dd/MM")
            : string.Empty;

    private static Color FondDeLaLigne(int jour, JourApi? donnee)
    {
        // Maladie et conges passent avant le ferie et le week-end : c'est
        // l'information la plus utile a voir d'un coup d'oeil.
        if (donnee?.Maladie == true)
        {
            return CouleurFondMaladie;
        }

        if (donnee?.Conge == true)
        {
            return CouleurFondConge;
        }

        if (donnee?.Ferie == true)
        {
            return CouleurFondFerie;
        }

        if (jour >= 5)
        {
            return CouleurFondWeekend;
        }

        return jour % 2 == 1 ? Color.FromArgb("#2B2B2B") : Colors.Transparent;
    }

    private void AppliquerFond(int jour, Color fond)
    {
        foreach (var enfant in GrilleJours.Children)
        {
            if (enfant is Border cellule
                && Grid.GetRow(cellule) == jour + 1)
            {
                cellule.BackgroundColor = fond;
            }
        }
    }

    // ------------------------------------------------------------------
    // Edition d'une journee
    // ------------------------------------------------------------------

    private async Task ModifierJourAsync(int index)
    {
        if (_occupation || index >= _semaine.Jours.Count)
        {
            return;
        }

        var jour = _semaine.Jours[index];
        var terminee = new TaskCompletionSource<bool>();

        await Navigation.PushModalAsync(new PageJournee(jour, NomsJours[index], terminee));

        if (!await terminee.Task)
        {
            return;
        }

        // Validation immediate : la journee part sur le serveur sans bouton.
        await ExecuterAsync(async () =>
        {
            var modele = DepuisApi(_semaine);
            _semaine = await _api.EnregistrerSemaineAsync(modele);
            AfficherSemaine();
            PlanifierAlertes();
        }, "Enregistrement…");
    }

    /// <summary>Reconstruit le modele de calcul a partir des donnees recues.</summary>
    private static SemaineApi DepuisApi(SemaineApi semaine)
    {
        var modele = new SemaineApi
        {
            AnneeIso = semaine.AnneeIso,
            Numero = semaine.Numero,
            ContratHebdomadaireMinutes = semaine.ContratHebdomadaireMinutes
        };

        foreach (var jour in semaine.Jours)
        {
            modele.Jours.Add(new JourApi
            {
                Index = jour.Index,
                Date = jour.Date,
                Ferie = jour.Ferie,
                Maladie = jour.Maladie,
                Conge = jour.Conge,
                Arrivee = jour.Arrivee,
                PauseDebut = jour.PauseDebut,
                PauseFin = jour.PauseFin,
                Depart = jour.Depart,
                Travaille = jour.Travaille
            });
        }

        return modele;
    }

    // ------------------------------------------------------------------
    // Navigation
    // ------------------------------------------------------------------

    private async void BoutonPrecedent_Clicked(object? sender, EventArgs e) => await NaviguerAsync(-1);

    private async void BoutonSuivant_Clicked(object? sender, EventArgs e) => await NaviguerAsync(1);

    private async Task NaviguerAsync(int decalage)
    {
        if (_semaine.AnneeIso <= 0)
        {
            return;
        }

        var lundi = SemaineTravail.LundiDeLaSemaine(_semaine.AnneeIso, _semaine.Numero).AddDays(7 * decalage);
        var (anneeIso, numero) = SemaineTravail.SemaineIsoDe(lundi);

        await ChargerSemaineAsync(anneeIso, numero);
    }

    private async void BoutonAujourdhui_Clicked(object? sender, EventArgs e) => await ChargerSemaineCouranteAsync();

    private async void BoutonReglages_Clicked(object? sender, EventArgs e)
    {
        await Navigation.PushModalAsync(new PageReglages(_api, Profil));
    }

    private async void BoutonDefauts_Clicked(object? sender, EventArgs e)
    {
        await Navigation.PushModalAsync(new PageHorairesDefaut(_api));
    }

    /// <summary>Le bilan des heures s'ouvre en paysage, comme le tableau.</summary>
    private async void BoutonStats_Clicked(object? sender, EventArgs e)
    {
        await Navigation.PushModalAsync(new PageStats(_api, _semaine));
    }

    /// <summary>
    /// Ouvre la page du contrat hebdomadaire. Le contrat est stocke avec la
    /// semaine : le modifier ici n'affecte pas les semaines anterieures.
    /// </summary>
    private async void BoutonContrat_Clicked(object? sender, EventArgs e)
    {
        if (_occupation)
        {
            return;
        }

        var resultat = new TaskCompletionSource<int?>();

        await Navigation.PushModalAsync(new PageContrat(_api, Profil, resultat));

        var minutes = await resultat.Task;

        if (minutes is null || minutes == _semaine.ContratHebdomadaireMinutes)
        {
            return;
        }

        _semaine.ContratHebdomadaireMinutes = minutes.Value;

        await ExecuterAsync(async () =>
        {
            var modele = DepuisApi(_semaine);
            _semaine = await _api.EnregistrerSemaineAsync(modele);
            AfficherSemaine();
            PlanifierAlertes();
        }, "Enregistrement…");
    }

    private async void BoutonDeconnexion_Clicked(object? sender, EventArgs e)
    {
        // Les rappels suivent la session : se deconnecter les coupe.
        AlertesHoraires.AnnulerTout();

        await _api.DeconnecterAsync();
        _session.EffacerJeton();

        if (Application.Current?.Windows.FirstOrDefault() is { } fenetre)
        {
            // Apres une deconnexion, l'empreinte n'est pas redemandee :
            // l'utilisateur veut parfois changer de compte.
            fenetre.Page = new PageConnexion(empreinteAutomatique: false);
        }
    }

    // ------------------------------------------------------------------
    // Execution protegee d'un echange avec le service
    // ------------------------------------------------------------------

    private async Task ExecuterAsync(Func<Task> action, string message)
    {
        _occupation = true;
        TexteStatut.Text = message;

        try
        {
            await action();
            TexteStatut.Text = string.Empty;
        }
        catch (Exception ex)
        {
            // Une popup plutot qu'un message dans la barre : la colonne d'etat
            // est etroite, et un texte long s'y cassait mot par mot au point
            // de deformer tout l'ecran.
            TexteStatut.Text = string.Empty;

            await DisplayAlertAsync("MelTime", ex.Message, "Compris");
        }
        finally
        {
            _occupation = false;
        }
    }
}
