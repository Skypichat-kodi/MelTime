namespace MelTime.Core.Modeles;

/// <summary>
/// Pays de rattachement d'un client, qui determine la regle des jours feries appliquee.
///
/// La valeur numerique est stockee telle quelle en base : ne jamais la modifier
/// pour un pays deja utilise, seulement en ajouter.
/// </summary>
public enum Pays
{
    /// <summary>Jours feries legaux francais (11 dates).</summary>
    France = 0,

    /// <summary>
    /// Royaume-Uni. Aucun jour ferie pour l'instant : les bank holidays
    /// anglaises obeissent a des regles de report qui restent a implementer.
    /// Un client britannique n'aura donc aucune journee coloree.
    /// </summary>
    RoyaumeUni = 1
}
