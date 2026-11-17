namespace MelTime.Core.Protocole;

/// <summary>
/// Identite du canal de communication local entre le service et ses interfaces.
/// Defini une seule fois : le service ecoute ici, l'UI et le Tray s'y connectent.
/// Rappel : un pipe nomme ne franchit pas les frontieres d'une machine.
/// </summary>
public static class PipeMelTime
{
    public const string Nom = "MelTime.Service";

    /// <summary>Representation lisible, utile pour les journaux et le diagnostic.</summary>
    public static string CheminComplet => $@"\\.\pipe\{Nom}";
}
