using System.Diagnostics;
using System.Text;

namespace MelTime.UI;

/// <summary>
/// Creation, suppression et controle des taches planifiees qui font demarrer
/// MelTime avec Windows.
///
/// Deux taches distinctes, parce que les deux programmes ont deux declencheurs differents :
///  - le service demarre avec la machine, sous le compte SYSTEM ;
///  - le Tray demarre a l'ouverture de session, avec les droits maximaux afin
///    de pouvoir joindre le service sans invite UAC.
///
/// Depuis cette version, les taches sont creees par IMPORT DE XML et non plus par
/// les options de la ligne de commande. Trois problemes disparaissent ainsi :
///
///  1. `schtasks /Create` sans XML impose par defaut une duree d'execution
///     maximale de 72 heures. Un service permanent serait arrete par Windows au
///     bout de trois jours, sans le moindre message. Le XML fixe
///     `ExecutionTimeLimit` a `PT0S`, c'est-a-dire aucune limite.
///  2. Par defaut, Windows ne demarre pas une tache sur batterie et l'arrete des
///     qu'on debranche l'ordinateur. Inacceptable pour un service permanent, et
///     genant pour l'icone de notification.
///  3. Le XML est le seul moyen de demander une relance automatique en cas d'echec,
///     et de fixer proprement le compte d'execution.
///
/// Encodage : `schtasks /Create /XML` exige un fichier Unicode, d'ou l'ecriture
/// en UTF-16 avec BOM.
/// </summary>
internal static class TachesPlanifiees
{
    public const string NomService = "MelTime Service";
    public const string NomTray = "MelTime Tray";

    /// <summary>SID bien connu du compte SYSTEM.</summary>
    private const string SidSystem = "S-1-5-18";

    /// <summary>Vrai si la tache existe deja.</summary>
    public static bool Existe(string nom)
    {
        var (code, _) = Executer($"/Query /TN \"{nom}\"");
        return code == 0;
    }

    /// <summary>
    /// Cree la tache du service : au demarrage de la machine, sous SYSTEM, sans
    /// limite de duree, avec relance automatique en cas d'echec.
    /// </summary>
    public static bool CreerService(string cheminExecutable, out string message)
    {
        var (ok, detail) = Installer(NomService, XmlService(cheminExecutable));

        message = ok
            ? "Le service démarrera avec la machine."
            : $"Échec de création de la tâche du service :{Environment.NewLine}{detail}";

        return ok;
    }

    /// <summary>
    /// Cree la tache du Tray : a l'ouverture de session, avec les droits maximaux
    /// afin de pouvoir joindre le service sans invite UAC.
    /// </summary>
    public static bool CreerTray(string cheminExecutable, out string message)
    {
        var (ok, detail) = Installer(NomTray, XmlTray(cheminExecutable));

        message = ok
            ? "Le Tray démarrera à l'ouverture de session."
            : $"Échec de création de la tâche du Tray :{Environment.NewLine}{detail}";

        return ok;
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

    // ------------------------------------------------------------------
    // Fabrique XML
    // ------------------------------------------------------------------

    private static string XmlService(string cheminExecutable)
    {
        var dossier = Path.GetDirectoryName(cheminExecutable) ?? string.Empty;

        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Author>MelTime</Author>
                <Description>Service MelTime : gestion des feuilles de temps.</Description>
              </RegistrationInfo>
              <Triggers>
                <BootTrigger>
                  <Enabled>true</Enabled>
                  <Delay>PT30S</Delay>
                </BootTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{SidSystem}</UserId>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <AllowHardTerminate>true</AllowHardTerminate>
                <StartWhenAvailable>true</StartWhenAvailable>
                <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
                <IdleSettings>
                  <StopOnIdleEnd>false</StopOnIdleEnd>
                  <RestartOnIdle>false</RestartOnIdle>
                </IdleSettings>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <Enabled>true</Enabled>
                <Hidden>false</Hidden>
                <RunOnlyIfIdle>false</RunOnlyIfIdle>
                <WakeToRun>false</WakeToRun>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>7</Priority>
                <RestartOnFailure>
                  <Interval>PT1M</Interval>
                  <Count>3</Count>
                </RestartOnFailure>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{Echapper(cheminExecutable)}</Command>
                  <WorkingDirectory>{Echapper(dossier)}</WorkingDirectory>
                </Exec>
              </Actions>
            </Task>
            """;
    }

    private static string XmlTray(string cheminExecutable)
    {
        var dossier = Path.GetDirectoryName(cheminExecutable) ?? string.Empty;
        var utilisateur = $"{Environment.UserDomainName}\\{Environment.UserName}";

        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Author>MelTime</Author>
                <Description>Icone de notification MelTime, ouverte a l'ouverture de session.</Description>
              </RegistrationInfo>
              <Triggers>
                <LogonTrigger>
                  <Enabled>true</Enabled>
                  <UserId>{Echapper(utilisateur)}</UserId>
                </LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{Echapper(utilisateur)}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <AllowHardTerminate>true</AllowHardTerminate>
                <StartWhenAvailable>true</StartWhenAvailable>
                <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
                <IdleSettings>
                  <StopOnIdleEnd>false</StopOnIdleEnd>
                  <RestartOnIdle>false</RestartOnIdle>
                </IdleSettings>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <Enabled>true</Enabled>
                <Hidden>false</Hidden>
                <RunOnlyIfIdle>false</RunOnlyIfIdle>
                <WakeToRun>false</WakeToRun>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>7</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{Echapper(cheminExecutable)}</Command>
                  <WorkingDirectory>{Echapper(dossier)}</WorkingDirectory>
                </Exec>
              </Actions>
            </Task>
            """;
    }

    /// <summary>
    /// Ecrit le XML dans un fichier temporaire puis l'importe avec schtasks.
    /// </summary>
    private static (bool Ok, string Detail) Installer(string nom, string xml)
    {
        var fichierTemporaire = Path.Combine(Path.GetTempPath(), $"meltime-{Guid.NewGuid():N}.xml");

        try
        {
            // Unicode obligatoire : schtasks refuse un XML UTF-8.
            File.WriteAllText(fichierTemporaire, xml, new UnicodeEncoding(false, true));

            var (code, sortie) = Executer($"/Create /TN \"{nom}\" /XML \"{fichierTemporaire}\" /F");

            return (code == 0, sortie);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
        finally
        {
            try
            {
                File.Delete(fichierTemporaire);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Fichier temporaire : Windows le nettoiera.
            }
        }
    }

    /// <summary>Echappe le texte pour insertion dans un element ou un attribut XML.</summary>
    private static string Echapper(string valeur) => valeur
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal)
        .Replace("'", "&apos;", StringComparison.Ordinal);

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
