using System.Diagnostics;

namespace MelTime.UI;

/// <summary>
/// Creation, suppression et controle des taches planifiees qui font demarrer
/// MelTime avec Windows.
///
/// Deux taches distinctes, parce que les deux programmes ont deux declencheurs differents :
///  - le service demarre avec la machine, sous le compte SYSTEM ;
///  - le Tray demarre a l'ouverture de session, avec les droits maximaux afin
///    de pouvoir joindre le service sans invite UAC.
/// </summary>
internal static class TachesPlanifiees
{
    public const string NomService = "MelTime Service";
    public const string NomTray = "MelTime Tray";

    /// <summary>Vrai si la tache existe deja.</summary>
    public static bool Existe(string nom)
    {
        var (code, _) = Executer($"/Query /TN \"{nom}\"");
        return code == 0;
    }

    /// <summary>Cree la tache du service : au demarrage de la machine, sous SYSTEM.</summary>
    public static bool CreerService(string cheminExecutable, out string message)
    {
        var (code, sortie) = Executer(
            $"/Create /TN \"{NomService}\" /TR \"\\\"{cheminExecutable}\\\"\" /SC ONSTART /RU SYSTEM /RL HIGHEST /F");

        message = code == 0
            ? "Le service démarrera avec la machine."
            : $"Échec de création de la tâche du service :{Environment.NewLine}{sortie}";

        return code == 0;
    }

    /// <summary>Cree la tache du Tray : a l'ouverture de session, avec les droits maximaux.</summary>
    public static bool CreerTray(string cheminExecutable, out string message)
    {
        var (code, sortie) = Executer(
            $"/Create /TN \"{NomTray}\" /TR \"\\\"{cheminExecutable}\\\"\" /SC ONLOGON /RL HIGHEST /F");

        message = code == 0
            ? "Le Tray démarrera à l'ouverture de session."
            : $"Échec de création de la tâche du Tray :{Environment.NewLine}{sortie}";

        return code == 0;
    }

    public static bool Supprimer(string nom, out string message)
    {
        var (code, sortie) = Executer($"/Delete /TN \"{nom}\" /F");

        message = code == 0
            ? "Tâche supprimée."
            : $"Échec de suppression :{Environment.NewLine}{sortie}";

        return code == 0;
    }

    /// <summary>
    /// Recherche un executable de MelTime, d'abord a l'emplacement d'installation,
    /// puis a cote de l'UI (cas des dossiers de publication).
    /// </summary>
    public static string? Localiser(string nomExecutable)
    {
        var programmeData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "MelTime");

        var candidats = new[]
        {
            Path.Combine(programmeData, nomExecutable),
            Path.Combine(AppContext.BaseDirectory, nomExecutable),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Service", nomExecutable)),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Tray", nomExecutable))
        };

        return candidats.FirstOrDefault(File.Exists);
    }

    private static (int Code, string Sortie) Executer(string arguments)
    {
        try
        {
            var info = new ProcessStartInfo("schtasks.exe", arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var processus = Process.Start(info);

            if (processus is null)
            {
                return (1, "Impossible de lancer schtasks.exe.");
            }

            var sortie = processus.StandardOutput.ReadToEnd() + processus.StandardError.ReadToEnd();
            processus.WaitForExit();

            return (processus.ExitCode, sortie.Trim());
        }
        catch (Exception ex)
        {
            return (1, ex.Message);
        }
    }
}
