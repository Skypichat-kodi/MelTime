using System.Text.Json.Serialization;
using MelTime.Core.Agenda;

namespace MelTime.Core.Modeles;

/// <summary>
/// Compte client tel qu'il circule entre le service et ses interfaces.
/// Ne contient AUCUN secret : le mot de passe hache reste confine a la base.
/// </summary>
public sealed class Client
{
    /// <summary>Identifiant technique, attribue par la base. 0 tant que le client n'est pas cree.</summary>
    public int Id { get; set; }

    /// <summary>Identifiant de connexion, unique et insensible a la casse.</summary>
    public string Identifiant { get; set; } = string.Empty;

    public string Nom { get; set; } = string.Empty;

    public string Prenom { get; set; } = string.Empty;

    /// <summary>Un client inactif ne peut pas se connecter, mais ses donnees sont conservees.</summary>
    public bool Actif { get; set; } = true;

    /// <summary>
    /// Contrat hebdomadaire, exprime en minutes pour rester sans ambiguite
    /// dans la base et dans les messages. 2100 minutes = 35 heures.
    /// C'est la valeur par defaut d'une nouvelle semaine ; elle reste ajustable semaine par semaine.
    /// </summary>
    public int ContratHebdomadaireMinutes { get; set; } = 2100;

    /// <summary>
    /// Premier jour du contrat. Les semaines anterieures ne comptent pas dans
    /// le solde : un client embauche en mars ne doit pas trainer un manque
    /// pour janvier et fevrier. Null pour un client dont on ignore la date,
    /// auquel cas seules les semaines reellement prevues sont comptees.
    /// </summary>
    public DateOnly? DebutContrat { get; set; }

    /// <summary>
    /// Pays de rattachement, qui determine les jours feries appliques.
    /// Le nom complet du type est necessaire : la propriete porte le meme nom.
    /// </summary>
    public Pays Pays { get; set; } = MelTime.Core.Modeles.Pays.France;

    public DateTimeOffset CreeLe { get; set; }

    public DateTimeOffset? DerniereConnexion { get; set; }

    [JsonIgnore]
    public string NomComplet => $"{Prenom} {Nom}".Trim();

    /// <summary>Contrat hebdomadaire sous forme de duree.</summary>
    [JsonIgnore]
    public TimeSpan ContratHebdomadaire => TimeSpan.FromMinutes(ContratHebdomadaireMinutes);

    /// <summary>Contrat hebdomadaire mis en forme pour l'affichage, par exemple « 35:00 ».</summary>
    [JsonIgnore]
    public string ContratAffiche => Durees.Formater(ContratHebdomadaire);

    /// <summary>Libelle du pays, pour l'affichage dans les listes.</summary>
    [JsonIgnore]
    public string PaysLibelle => Pays switch
    {
        MelTime.Core.Modeles.Pays.France => "France",
        MelTime.Core.Modeles.Pays.RoyaumeUni => "Royaume-Uni",
        _ => "Inconnu"
    };

    public override string ToString() => $"{Identifiant} ({NomComplet})";

    /// <summary>
    /// Verifie la coherence des donnees d'identite (hors mot de passe, valide separement).
    /// </summary>
    /// <returns>La liste des anomalies constatees ; vide si tout est correct.</returns>
    public IReadOnlyList<string> Valider()
    {
        var erreurs = new List<string>();

        if (string.IsNullOrWhiteSpace(Identifiant))
        {
            erreurs.Add("L'identifiant est obligatoire.");
        }
        else if (Identifiant.Trim().Length < 3 || Identifiant.Trim().Length > 50)
        {
            erreurs.Add("L'identifiant doit compter entre 3 et 50 caracteres.");
        }
        else if (Identifiant.Any(char.IsWhiteSpace))
        {
            erreurs.Add("L'identifiant ne doit pas contenir d'espace.");
        }

        if (string.IsNullOrWhiteSpace(Nom))
        {
            erreurs.Add("Le nom est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(Prenom))
        {
            erreurs.Add("Le prenom est obligatoire.");
        }

        if (ContratHebdomadaireMinutes < 60 || ContratHebdomadaireMinutes > 3600)
        {
            erreurs.Add("Le contrat hebdomadaire doit etre compris entre 1 heure et 60 heures.");
        }

        if (!Enum.IsDefined(Pays))
        {
            erreurs.Add("Le pays indique n'est pas reconnu.");
        }

        return erreurs;
    }
}
