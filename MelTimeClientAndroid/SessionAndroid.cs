namespace MelTimeClientAndroid;

/// <summary>
/// Memoire locale du client Android : adresse du serveur, dernier identifiant
/// et jeton de session. Remplace le %AppData% du client Windows.
///
/// Preferences de MAUI s'appuie sur les SharedPreferences d'Android : les
/// valeurs survivent a la fermeture de l'application.
/// </summary>
public sealed class SessionAndroid
{
    private const string CleAdresse = "serveur.adresse";
    private const string CleJeton = "serveur.jeton";
    private const string CleIdentifiant = "serveur.identifiant";

    public string AdresseServeur
    {
        get => Preferences.Default.Get(CleAdresse, string.Empty);
        set => Preferences.Default.Set(CleAdresse, value);
    }

    public string? Identifiant
    {
        get
        {
            var valeur = Preferences.Default.Get(CleIdentifiant, string.Empty);
            return string.IsNullOrWhiteSpace(valeur) ? null : valeur;
        }
        set => Preferences.Default.Set(CleIdentifiant, value ?? string.Empty);
    }

    public string? Jeton
    {
        get
        {
            var valeur = Preferences.Default.Get(CleJeton, string.Empty);
            return string.IsNullOrWhiteSpace(valeur) ? null : valeur;
        }
        set => Preferences.Default.Set(CleJeton, value ?? string.Empty);
    }

    public void EffacerJeton() => Jeton = null;

    /// <summary>
    /// Decoupe l'adresse memorisee en hote et port, pour remplir les champs.
    /// Retourne des valeurs par defaut si rien n'est enregistre.
    /// </summary>
    public (string Hote, string Port) DecouperAdresse()
    {
        var adresse = AdresseServeur;

        if (Uri.TryCreate(adresse, UriKind.Absolute, out var uri))
        {
            return (uri.Host, uri.Port.ToString());
        }

        return (string.Empty, "5080");
    }
}
