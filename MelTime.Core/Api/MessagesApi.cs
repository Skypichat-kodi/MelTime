using MelTime.Core.Modeles;

namespace MelTime.Core.Api;

/// <summary>Corps de la requete POST /api/connexion.</summary>
public sealed class DemandeConnexion
{
    public string Identifiant { get; set; } = string.Empty;

    public string MotDePasse { get; set; } = string.Empty;
}

/// <summary>Reponse de POST /api/connexion.</summary>
public sealed class ReponseConnexion
{
    public bool Succes { get; set; }

    /// <summary>Jeton a presenter ensuite dans l'en-tete Authorization: Bearer.</summary>
    public string? Jeton { get; set; }

    public DateTimeOffset? ExpireLe { get; set; }

    public Client? Client { get; set; }

    public string? Erreur { get; set; }
}

/// <summary>Reponse d'erreur generique de l'API.</summary>
public sealed class ReponseErreur
{
    public bool Succes { get; set; }

    public string? Erreur { get; set; }
}
