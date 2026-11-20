using System.Reflection;

namespace MelTime.Core;

/// <summary>
/// Version de l'application, lue dans les metadonnees de l'assembly.
///
/// GitVersion les renseigne a la compilation, via Directory.Build.props : la
/// version affichee ne peut donc pas mentir, puisqu'elle vient de l'historique
/// Git et non d'une valeur saisie a la main.
///
/// Le serveur s'appuie sur Application.ProductVersion, qui lit la meme donnee ;
/// ce point d'entree sert aux interfaces qui n'ont pas cet equivalent, comme
/// MAUI.
/// </summary>
public static class VersionApplication
{
    /// <summary>Version de l'assembly appelante.</summary>
    public static string Lire() =>
        Lire(Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly());

    /// <summary>Version d'une assembly donnee.</summary>
    public static string Lire(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var information = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(information))
        {
            return information;
        }

        return assembly.GetName().Version?.ToString() ?? "—";
    }
}
