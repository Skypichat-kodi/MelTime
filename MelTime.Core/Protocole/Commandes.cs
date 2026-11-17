namespace MelTime.Core.Protocole;

/// <summary>
/// Noms des commandes echangees sur le pipe entre le service et ses interfaces.
/// Centralises ici pour que le service et l'UI ne puissent pas diverger.
/// </summary>
public static class Commandes
{
    /// <summary>Verifie que le service repond. Utilise par le Tray pour afficher son etat.</summary>
    public const string ServiceVivant = "service.vivant";

    public const string ClientsLister = "clients.lister";
    public const string ClientsObtenir = "clients.obtenir";
    public const string ClientsCreer = "clients.creer";
    public const string ClientsModifier = "clients.modifier";
    public const string ClientsChangerMotDePasse = "clients.changerMotDePasse";
    public const string ClientsSupprimer = "clients.supprimer";
}
