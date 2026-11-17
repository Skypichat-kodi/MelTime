namespace MelTime.Service;

/// <summary>
/// Emplacements de stockage du serveur.
/// Tout ce qui est modifiable vit sous C:\ProgramData\MelTime\ :
/// le dossier du programme (Program Files) est en lecture seule pour un utilisateur standard.
/// </summary>
public static class CheminDonnees
{
    public const string NomDossier = "MelTime";

    /// <summary>C:\ProgramData\MelTime</summary>
    public static string DossierRacine =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), NomDossier);

    /// <summary>C:\ProgramData\MelTime\data</summary>
    public static string DossierDonnees => Path.Combine(DossierRacine, "data");

    /// <summary>C:\ProgramData\MelTime\data\archives : donnees des clients supprimes.</summary>
    public static string DossierArchives => Path.Combine(DossierDonnees, "archives");

    /// <summary>Base d'administration : comptes clients (identifiants, mots de passe haches, etat, contrat).</summary>
    public static string BaseAdministration => Path.Combine(DossierDonnees, "meltime.admin.db");

    /// <summary>
    /// Base de donnees d'un client, nommee par son identifiant technique.
    /// Nommer par identifiant technique et non par identifiant de connexion evite
    /// qu'un renommage de compte ne casse le lien avec les donnees.
    /// </summary>
    public static string BaseClient(int idClient) =>
        Path.Combine(DossierDonnees, $"client-{idClient}.db");

    /// <summary>Cree les dossiers necessaires s'ils n'existent pas.</summary>
    public static void Preparer()
    {
        Directory.CreateDirectory(DossierDonnees);
    }
}
