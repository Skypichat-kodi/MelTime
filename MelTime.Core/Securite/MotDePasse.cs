using System.Security.Cryptography;

namespace MelTime.Core.Securite;

/// <summary>
/// Hachage et verification des mots de passe.
/// Algorithme : PBKDF2-SHA256 avec sel aleatoire (fourni par .NET, aucune dependance externe).
/// Format stocke : PBKDF2-SHA256$iterations$sel_base64$hachage_base64
/// Un mot de passe n'est JAMAIS stocke en clair ni reversible.
/// </summary>
public static class MotDePasse
{
    /// <summary>Longueur minimale imposee a la saisie.</summary>
    public const int LongueurMinimale = 8;

    private const string Algorithme = "PBKDF2-SHA256";
    private const int Iterations = 210_000; // recommandation OWASP pour PBKDF2-SHA256
    private const int TailleSel = 16;
    private const int TailleHachage = 32;
    private const char Separateur = '$';

    /// <summary>Produit l'enregistrement hache a stocker en base.</summary>
    public static string Hacher(string motDePasse)
    {
        if (string.IsNullOrEmpty(motDePasse))
        {
            throw new ArgumentException("Le mot de passe ne peut pas etre vide.", nameof(motDePasse));
        }

        var sel = RandomNumberGenerator.GetBytes(TailleSel);
        var hachage = Deriver(motDePasse, sel, Iterations);

        return string.Join(
            Separateur,
            Algorithme,
            Iterations.ToString(),
            Convert.ToBase64String(sel),
            Convert.ToBase64String(hachage));
    }

    /// <summary>
    /// Compare un mot de passe saisi a un enregistrement hache.
    /// La comparaison se fait en temps constant pour ne pas reveler d'information.
    /// </summary>
    public static bool Verifier(string motDePasse, string enregistrement)
    {
        if (string.IsNullOrEmpty(motDePasse) || string.IsNullOrEmpty(enregistrement))
        {
            return false;
        }

        var parties = enregistrement.Split(Separateur);
        if (parties.Length != 4 || parties[0] != Algorithme)
        {
            return false;
        }

        if (!int.TryParse(parties[1], out var iterations) || iterations <= 0)
        {
            return false;
        }

        byte[] sel;
        byte[] attendu;
        try
        {
            sel = Convert.FromBase64String(parties[2]);
            attendu = Convert.FromBase64String(parties[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var calcule = Deriver(motDePasse, sel, iterations, attendu.Length);
        return CryptographicOperations.FixedTimeEquals(calcule, attendu);
    }

    private static byte[] Deriver(string motDePasse, byte[] sel, int iterations, int longueur = TailleHachage)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            password: motDePasse,
            salt: sel,
            iterations: iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: longueur);
    }
}
