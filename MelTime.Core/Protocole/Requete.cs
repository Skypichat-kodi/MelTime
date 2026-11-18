using MelTime.Core.Modeles;

namespace MelTime.Core.Protocole;

/// <summary>
/// Message envoye par une interface (UI, Tray) vers le service.
/// Les champs inutilises par une commande restent a null.
/// </summary>
public sealed class Requete
{
    public string Commande { get; set; } = string.Empty;

    /// <summary>Identifiant technique du client vise (obtenir, modifier, supprimer).</summary>
    public int? Id { get; set; }

    /// <summary>Donnees du client (creer, modifier).</summary>
    public Client? Client { get; set; }

    /// <summary>Mot de passe en clair (creer, changerMotDePasse). Hache des son arrivee dans le service.</summary>
    public string? MotDePasse { get; set; }

    /// <summary>Configuration reseau envoyee par l'UI (config.ecrire).</summary>
    public ConfigurationServeur? Configuration { get; set; }
}
