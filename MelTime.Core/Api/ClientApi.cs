using System.Net.Http.Headers;
using System.Net.Http.Json;
using MelTime.Core.Modeles;
using MelTime.Core.Protocole;

namespace MelTime.Core.Api;

/// <summary>
/// Client de l'API HTTP du serveur, utilise par le client Windows.
/// Toutes les communications reseau passent par ici : aucun autre fichier
/// du client ne connait HTTP.
/// </summary>
public sealed class ClientApi : IDisposable
{
    private readonly HttpClient _http;

    public ClientApi(string adresseServeur)
    {
        if (string.IsNullOrWhiteSpace(adresseServeur)
            || !Uri.TryCreate(adresseServeur.TrimEnd('/') + "/", UriKind.Absolute, out var adresse))
        {
            throw new ArgumentException("Adresse du serveur invalide.", nameof(adresseServeur));
        }

        _http = new HttpClient
        {
            BaseAddress = adresse,
            Timeout = TimeSpan.FromSeconds(20)
        };
    }

    /// <summary>Jeton de session, presente a chaque requete protegee.</summary>
    public string? Jeton { get; set; }

    /// <summary>Vrai si le serveur repond, sans exiger d'authentification.</summary>
    public async Task<bool> EtatAsync(CancellationToken arret = default)
    {
        try
        {
            using var reponse = await _http.GetAsync("/api/etat", arret).ConfigureAwait(false);
            return reponse.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Authentifie le client et retourne la reponse du serveur.</summary>
    public async Task<ReponseConnexion> ConnecterAsync(string identifiant, string motDePasse, CancellationToken arret = default)
    {
        using var reponse = await _http
            .PostAsJsonAsync("/api/connexion", new DemandeConnexion { Identifiant = identifiant, MotDePasse = motDePasse }, ProtocoleJson.Options, arret)
            .ConfigureAwait(false);

        var contenu = await LireAsync<ReponseConnexion>(reponse, arret).ConfigureAwait(false);

        return contenu ?? new ReponseConnexion { Succes = false, Erreur = "Reponse illisible du serveur." };
    }

    /// <summary>Ferme la session cote serveur. Les erreurs sont ignorees : le jeton local est efface de toute facon.</summary>
    public async Task DeconnecterAsync(CancellationToken arret = default)
    {
        try
        {
            using var requete = CreerRequete(HttpMethod.Post, "/api/deconnexion");
            using var reponse = await _http.SendAsync(requete, arret).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Une deconnexion ne doit jamais echouer visiblement.
        }
    }

    /// <summary>Profil du client connecte. Leve une exception si la session n'est plus valide.</summary>
    public async Task<Client> ProfilAsync(CancellationToken arret = default)
    {
        using var requete = CreerRequete(HttpMethod.Get, "/api/profil");
        using var reponse = await _http.SendAsync(requete, arret).ConfigureAwait(false);
        await ExigerSuccesAsync(reponse, arret).ConfigureAwait(false);

        return await LireAsync<Client>(reponse, arret).ConfigureAwait(false)
               ?? throw new InvalidOperationException("Profil illisible.");
    }

    /// <summary>Charge une semaine. Une semaine jamais saisie revient vierge, au contrat du client.</summary>
    public async Task<SemaineApi> ChargerSemaineAsync(int anneeIso, int numero, CancellationToken arret = default)
    {
        using var requete = CreerRequete(HttpMethod.Get, $"/api/semaine/{anneeIso}/{numero}");
        using var reponse = await _http.SendAsync(requete, arret).ConfigureAwait(false);
        await ExigerSuccesAsync(reponse, arret).ConfigureAwait(false);

        return await LireAsync<SemaineApi>(reponse, arret).ConfigureAwait(false)
               ?? throw new InvalidOperationException("Semaine illisible.");
    }

    /// <summary>Enregistre une semaine et retourne la version recalculee par le serveur.</summary>
    public async Task<SemaineApi> EnregistrerSemaineAsync(SemaineApi semaine, CancellationToken arret = default)
    {
        ArgumentNullException.ThrowIfNull(semaine);

        using var requete = CreerRequete(HttpMethod.Put, $"/api/semaine/{semaine.AnneeIso}/{semaine.Numero}");
        requete.Content = JsonContent.Create(semaine, options: ProtocoleJson.Options);

        using var reponse = await _http.SendAsync(requete, arret).ConfigureAwait(false);
        await ExigerSuccesAsync(reponse, arret).ConfigureAwait(false);

        return await LireAsync<SemaineApi>(reponse, arret).ConfigureAwait(false)
               ?? throw new InvalidOperationException("Semaine illisible.");
    }

    /// <summary>Total des heures d'un mois civil. Leve une exception si la session n'est plus valide.</summary>
    public async Task<TotalMoisApi> TotalMoisAsync(int annee, int mois, CancellationToken arret = default)
    {
        using var requete = CreerRequete(HttpMethod.Get, $"/api/mois/{annee}/{mois}");
        using var reponse = await _http.SendAsync(requete, arret).ConfigureAwait(false);
        await ExigerSuccesAsync(reponse, arret).ConfigureAwait(false);

        return await LireAsync<TotalMoisApi>(reponse, arret).ConfigureAwait(false)
               ?? throw new InvalidOperationException("Total mensuel illisible.");
    }

    /// <summary>Modele d'horaires du client, qui prereplit ses semaines vides.</summary>
    public async Task<HorairesDefautApi> HorairesDefautAsync(CancellationToken arret = default)
    {
        using var requete = CreerRequete(HttpMethod.Get, "/api/horaires-defaut");
        using var reponse = await _http.SendAsync(requete, arret).ConfigureAwait(false);
        await ExigerSuccesAsync(reponse, arret).ConfigureAwait(false);

        return await LireAsync<HorairesDefautApi>(reponse, arret).ConfigureAwait(false)
               ?? throw new InvalidOperationException("Modele d'horaires illisible.");
    }

    /// <summary>Enregistre le modele d'horaires et retourne la version retenue par le service.</summary>
    public async Task<HorairesDefautApi> EnregistrerHorairesDefautAsync(HorairesDefautApi modele, CancellationToken arret = default)
    {
        ArgumentNullException.ThrowIfNull(modele);

        using var requete = CreerRequete(HttpMethod.Put, "/api/horaires-defaut");
        requete.Content = JsonContent.Create(modele, options: ProtocoleJson.Options);

        using var reponse = await _http.SendAsync(requete, arret).ConfigureAwait(false);
        await ExigerSuccesAsync(reponse, arret).ConfigureAwait(false);

        return await LireAsync<HorairesDefautApi>(reponse, arret).ConfigureAwait(false)
               ?? throw new InvalidOperationException("Modele d'horaires illisible.");
    }

    /// <summary>Reglages personnels du client (salaires).</summary>
    public async Task<ReglagesApi> ReglagesAsync(CancellationToken arret = default)
    {
        using var requete = CreerRequete(HttpMethod.Get, "/api/reglages");
        using var reponse = await _http.SendAsync(requete, arret).ConfigureAwait(false);
        await ExigerSuccesAsync(reponse, arret).ConfigureAwait(false);

        return await LireAsync<ReglagesApi>(reponse, arret).ConfigureAwait(false)
               ?? throw new InvalidOperationException("Reglages illisibles.");
    }

    /// <summary>Enregistre les reglages personnels du client.</summary>
    public async Task<ReglagesApi> EnregistrerReglagesAsync(ReglagesApi reglages, CancellationToken arret = default)
    {
        ArgumentNullException.ThrowIfNull(reglages);

        using var requete = CreerRequete(HttpMethod.Put, "/api/reglages");
        requete.Content = JsonContent.Create(reglages, options: ProtocoleJson.Options);

        using var reponse = await _http.SendAsync(requete, arret).ConfigureAwait(false);
        await ExigerSuccesAsync(reponse, arret).ConfigureAwait(false);

        return await LireAsync<ReglagesApi>(reponse, arret).ConfigureAwait(false)
               ?? throw new InvalidOperationException("Reglages illisibles.");
    }

    private HttpRequestMessage CreerRequete(HttpMethod methode, string chemin)
    {
        var requete = new HttpRequestMessage(methode, chemin);

        if (!string.IsNullOrEmpty(Jeton))
        {
            requete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Jeton);
        }

        return requete;
    }

    private static async Task<T?> LireAsync<T>(HttpResponseMessage reponse, CancellationToken arret)
    {
        try
        {
            return await reponse.Content.ReadFromJsonAsync<T>(ProtocoleJson.Options, arret).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return default;
        }
    }

    /// <summary>Transforme une reponse d'erreur en exception porteuse du message du serveur.</summary>
    private static async Task ExigerSuccesAsync(HttpResponseMessage reponse, CancellationToken arret)
    {
        if (reponse.IsSuccessStatusCode)
        {
            return;
        }

        var erreur = await LireAsync<ReponseErreur>(reponse, arret).ConfigureAwait(false);

        if (reponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException(erreur?.Erreur ?? "Session expirée, reconnectez-vous.");
        }

        throw new InvalidOperationException(erreur?.Erreur ?? $"Le serveur a repondu {(int)reponse.StatusCode}.");
    }

    public void Dispose() => _http.Dispose();
}
