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

    public PageFeuilleTemps(ClientApi api, Client profil)
    {
        InitializeComponent();

        _api = api;
        Profil = profil;

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
            await ChargerTotalMoisAsync();
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
        TexteSemaine.Text = $"Semaine {_semaine.Numero} · {_semaine.AnneeIso}   {Court(lundi)} → {Court(dimanche)}";

        TexteTotal.Text = $"Total semaine {_semaine.TotalTravaille}";
        TexteContrat.Text = $"Contrat {Durees.Formater(TimeSpan.FromMinutes(_semaine.ContratHebdomadaireMinutes))}";
        AfficherEcart();
        TexteStatut.Text = string.Empty;
    }

    /// <summary>
    /// Compare le total de la semaine au contrat : heures supplementaires
    /// s'il est depasse, manque dans le cas contraire. L'utilisateur voit
    /// ainsi d'un coup d'oeil ou il en est, sans calcul mental.
    /// </summary>
    private void AfficherEcart()
    {
        var total = EnDuree(_semaine.TotalTravaille);
        var contrat = TimeSpan.FromMinutes(_semaine.ContratHebdomadaireMinutes);

        if (total is null || contrat <= TimeSpan.Zero)
        {
            TexteEcart.Text = string.Empty;
            return;
        }

        var ecart = total.Value - contrat;

        if (ecart > TimeSpan.Zero)
        {
            TexteEcart.Text = $"Heures supp. +{Durees.Formater(ecart)}";
            TexteEcart.TextColor = Color.FromArgb("#3FB950");
        }
        else if (ecart < TimeSpan.Zero)
        {
            TexteEcart.Text = $"Manque {Durees.Formater(ecart.Negate())}";
            TexteEcart.TextColor = Color.FromArgb("#FF5B5B");
        }
        else
        {
            TexteEcart.Text = "Contrat atteint";
            TexteEcart.TextColor = Color.FromArgb("#A0A0A0");
        }
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

    private async Task ChargerTotalMoisAsync()
    {
        var premier = _semaine.Jours.FirstOrDefault();

        if (premier is null
            || !DateOnly.TryParseExact(premier.Date, SemaineApi.FormatDate, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date))
        {
            TexteTotalMois.Text = string.Empty;
            return;
        }

        try
        {
            var total = await _api.TotalMoisAsync(date.Year, date.Month);
            TexteTotalMois.Text = $"Mois {total.TotalTravaille}";
        }
        catch (Exception)
        {
            TexteTotalMois.Text = string.Empty;
        }

        await ChargerSoldeAsync();
    }

    /// <summary>
    /// Solde des heures sur les 52 dernieres semaines : positif, ce sont des
    /// heures a recuperer ; negatif, des heures manquantes. Il ne depend pas
    /// de la semaine affichee.
    /// </summary>
    private async Task ChargerSoldeAsync()
    {
        try
        {
            var solde = await _api.SoldeAsync();

            // Heures faites au-dela de 35 h, meme si le contrat est respecte.
            TexteSupplementairesAn.Text = $"Sup. 35h {solde.Supplementaires}";
            TexteSupplementairesAn.TextColor = Color.FromArgb("#0078D4");

            if (solde.Ecart == "00:00")
            {
                TexteSolde.Text = "Écart 00:00";
                TexteSolde.TextColor = Color.FromArgb("#A0A0A0");
                return;
            }

            TexteSolde.Text = solde.EcartPositif
                ? $"Écart +{solde.Ecart}"
                : $"Écart {solde.Ecart}";

            TexteSolde.TextColor = solde.EcartPositif
                ? Color.FromArgb("#3FB950")
                : Color.FromArgb("#FF5B5B");

            TexteConges.Text = $"Congés {solde.CongesPris}/{solde.CongesAcquis}";
        }
        catch (Exception)
        {
            TexteSupplementairesAn.Text = string.Empty;
            TexteSolde.Text = string.Empty;
            TexteConges.Text = string.Empty;
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
            await ChargerTotalMoisAsync();
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

    /// <summary>
    /// Change la duree du contrat pour la semaine affichee, puis l'enregistre.
    /// Le contrat est stocke avec la semaine : le modifier ici n'affecte pas
    /// les semaines deja enregistrees.
    /// </summary>
    private async void TexteContrat_Tapped(object? sender, TappedEventArgs e)
    {
        if (_occupation)
        {
            return;
        }

        var resultat = new TaskCompletionSource<int?>();

        await Navigation.PushModalAsync(new PageContrat(_semaine.ContratHebdomadaireMinutes, resultat));

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
            await ChargerTotalMoisAsync();
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
            fenetre.Page = new PageConnexion();
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

            await DisplayAlert("MelTime", ex.Message, "Compris");
        }
        finally
        {
            _occupation = false;
        }
    }
}
