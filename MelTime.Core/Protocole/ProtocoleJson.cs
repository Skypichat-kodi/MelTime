using System.Text.Json;
using System.Text.Json.Serialization;

namespace MelTime.Core.Protocole;

/// <summary>
/// Serialisation commune aux deux extremites du pipe.
/// Une seule definition des options : service et UI lisent et ecrivent exactement pareil.
/// </summary>
public static class ProtocoleJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialiser(Requete requete) => JsonSerializer.Serialize(requete, Options);

    public static string Serialiser(Reponse reponse) => JsonSerializer.Serialize(reponse, Options);

    /// <summary>Retourne null si le texte n'est pas une requete exploitable.</summary>
    public static Requete? DeserialiserRequete(string json) =>
        JsonSerializer.Deserialize<Requete>(json, Options);

    /// <summary>Retourne null si le texte n'est pas une reponse exploitable.</summary>
    public static Reponse? DeserialiserReponse(string json) =>
        JsonSerializer.Deserialize<Reponse>(json, Options);
}
