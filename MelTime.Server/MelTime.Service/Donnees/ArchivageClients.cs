using MelTime.Core.Modeles;
using Microsoft.Data.Sqlite;

namespace MelTime.Service.Donnees;

/// <summary>
/// Archivage des donnees d'un client supprime.
/// Le fichier n'est jamais efface : il est deplace dans data\archives
/// avec un nom qui rappelle de qui il s'agit et quand il a ete archive.
/// </summary>
public static class ArchivageClients
{
    /// <summary>
    /// Deplace la base d'un client vers le dossier d'archives.
    /// Retourne le chemin de l'archive, ou null si le client n'avait aucune donnee.
    /// </summary>
    public static string? Archiver(string cheminBase, Client client)
    {
        ArgumentNullException.ThrowIfNull(client);

        if (!File.Exists(cheminBase))
        {
            return null;
        }

        Directory.CreateDirectory(CheminDonnees.DossierArchives);

        var nom = string.Join(
            "-",
            $"client-{client.Id}",
            Nettoyer(client.Nom),
            Nettoyer(client.Prenom),
            DateTime.Now.ToString("yyyyMMdd-HHmmss"));

        var destination = Path.Combine(CheminDonnees.DossierArchives, nom + ".db");

        // Le pool de connexions SQLite garde le fichier ouvert : il faut le vider
        // avant de pouvoir le deplacer.
        SqliteConnection.ClearAllPools();

        File.Move(cheminBase, destination);

        return destination;
    }

    private static string Nettoyer(string valeur)
    {
        if (string.IsNullOrWhiteSpace(valeur))
        {
            return "inconnu";
        }

        var interdits = Path.GetInvalidFileNameChars();
        var propre = new string(valeur
            .Where(caractere => !interdits.Contains(caractere) && !char.IsWhiteSpace(caractere))
            .ToArray());

        return string.IsNullOrEmpty(propre) ? "inconnu" : propre;
    }
}
