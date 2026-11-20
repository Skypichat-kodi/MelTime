namespace MelTime.Core.Api;

/// <summary>
/// Elements du profil que le client peut corriger lui-meme.
/// Le reste de sa fiche reste du ressort de l'administration.
/// </summary>
public sealed class ProfilApi
{
    /// <summary>
    /// Premier jour du contrat, au format « yyyy-MM-dd ». Null efface la date,
    /// et le solde repart alors de la premiere semaine saisie.
    /// </summary>
    public string? DebutContrat { get; set; }
}
