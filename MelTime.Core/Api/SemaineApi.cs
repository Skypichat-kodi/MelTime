using System.Globalization;
using MelTime.Core.Agenda;
using MelTime.Core.Modeles;

namespace MelTime.Core.Api;

/// <summary>
/// Representation d'une feuille de temps telle qu'elle circule en JSON
/// entre le service et les clients.
/// Les heures sont ecrites « HH:mm » et les durees « hh:mm » : un format
/// explicite, lisible dans un navigateur, sans dependance a un serialiseur.
/// </summary>
public sealed class SemaineApi
{
    public const string FormatHeure = "HH:mm";
    public const string FormatDate = "yyyy-MM-dd";

    public int AnneeIso { get; set; }

    public int Numero { get; set; }

    public string Lundi { get; set; } = string.Empty;

    public int ContratHebdomadaireMinutes { get; set; } = 2100;

    public List<JourApi> Jours { get; set; } = new();

    /// <summary>Total de la semaine, par exemple « 40:00 ».</summary>
    public string TotalTravaille { get; set; } = "00:00";

    /// <summary>Heures au-dela du contrat de la semaine, par exemple « 05:00 ».</summary>
    public string HeuresSupplementaires { get; set; } = "00:00";

    public static SemaineApi Depuis(SemaineTravail semaine, Pays pays = Pays.France)
    {
        ArgumentNullException.ThrowIfNull(semaine);

        var api = new SemaineApi
        {
            AnneeIso = semaine.AnneeIso,
            Numero = semaine.Numero,
            Lundi = semaine.Lundi.ToString(FormatDate, CultureInfo.InvariantCulture),
            ContratHebdomadaireMinutes = semaine.ContratHebdomadaireMinutes,
            TotalTravaille = Durees.Formater(semaine.TotalTravaille),
            HeuresSupplementaires = Durees.Formater(semaine.HeuresSupplementaires())
        };

        for (var index = 0; index < semaine.Jours.Count; index++)
        {
            var jour = semaine.Jours[index];

            api.Jours.Add(new JourApi
            {
                Index = index,
                Date = jour.Date.ToString(FormatDate, CultureInfo.InvariantCulture),
                Ferie = JoursFeries.EstFerie(pays, jour.Date),
                Arrivee = EnTexte(jour.Arrivee),
                PauseDebut = EnTexte(jour.PauseDebut),
                PauseFin = EnTexte(jour.PauseFin),
                Depart = EnTexte(jour.Depart),
                Travaille = Durees.Formater(jour.DureeTravaillee)
            });
        }

        return api;
    }

    /// <summary>Reconstruit le modele de calcul a partir des donnees recues.</summary>
    public SemaineTravail VersModele()
    {
        var semaine = SemaineTravail.Creer(AnneeIso, Numero);
        semaine.ContratHebdomadaireMinutes = ContratHebdomadaireMinutes;

        foreach (var jour in Jours)
        {
            if (jour.Index < 0 || jour.Index >= semaine.Jours.Count)
            {
                continue;
            }

            var cible = semaine.Jours[jour.Index];
            cible.Arrivee = EnHeure(jour.Arrivee);
            cible.PauseDebut = EnHeure(jour.PauseDebut);
            cible.PauseFin = EnHeure(jour.PauseFin);
            cible.Depart = EnHeure(jour.Depart);
        }

        return semaine;
    }

    private static string? EnTexte(TimeOnly? heure) =>
        heure?.ToString(FormatHeure, CultureInfo.InvariantCulture);

    private static TimeOnly? EnHeure(string? texte) =>
        string.IsNullOrWhiteSpace(texte)
            ? null
            : TimeOnly.TryParseExact(texte, FormatHeure, CultureInfo.InvariantCulture, DateTimeStyles.None, out var heure)
                ? heure
                : null;
}

/// <summary>Une journee dans les echanges JSON.</summary>
public sealed class JourApi
{
    /// <summary>0 = lundi, 6 = dimanche.</summary>
    public int Index { get; set; }

    public string Date { get; set; } = string.Empty;

    /// <summary>
    /// Vrai si la journee est un jour ferie.
    /// Calcule par le serveur selon le pays du client, jamais par le client
    /// lui-meme : la regle vit ainsi a un seul endroit, et un futur client
    /// Android en beneficie sans rien reimplementer.
    /// </summary>
    public bool Ferie { get; set; }

    public string? Arrivee { get; set; }

    public string? PauseDebut { get; set; }

    public string? PauseFin { get; set; }

    public string? Depart { get; set; }

    /// <summary>Duree travailee, par exemple « 08:00 », ou « — » si la journee est incomplete.</summary>
    public string Travaille { get; set; } = "—";
}
