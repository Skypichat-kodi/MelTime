using MelTime.Core.Modeles;

namespace MelTime.Core.Protocole;

/// <summary>
/// Message renvoye par le service.
/// Un seul de ces champs de donnees est renseigne selon la commande,
/// sauf Erreur qui n'est rempli qu'en cas d'echec.
/// </summary>
public sealed class Reponse
{
    public bool Succes { get; set; }

    /// <summary>Message d'erreur lisible, si Succes vaut false.</summary>
    public string? Erreur { get; set; }

    /// <summary>Texte libre pour les commandes de diagnostic (service.vivant).</summary>
    public string? Message { get; set; }

    /// <summary>Liste retournee par clients.lister.</summary>
    public List<Client>? Clients { get; set; }

    /// <summary>Client retourne par les autres commandes.</summary>
    public Client? Client { get; set; }

    public static Reponse Ok() => new() { Succes = true };

    public static Reponse Ok(Client client) => new() { Succes = true, Client = client };

    public static Reponse Ok(IEnumerable<Client> clients) => new() { Succes = true, Clients = clients.ToList() };

    public static Reponse Info(string message) => new() { Succes = true, Message = message };

    public static Reponse Echec(string erreur) => new() { Succes = false, Erreur = erreur };
}
