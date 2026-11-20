namespace MelTime.Core.Api;

/// <summary>
/// Horaires types d'un client : jusqu'a cinq modeles, notes de A a E.
///
/// Un modele peut etre desactive : il est alors ignore dans la rotation.
/// La date de debut indique quelle semaine porte le premier modele actif ;
/// les semaines suivantes avancent dans le cycle, puis recommencent.
/// </summary>
public sealed class HorairesDefautApi
{
    /// <summary>
    /// Lundi de la premiere semaine du cycle, au format « yyyy-MM-dd ».
    /// Null tant que le client n'a pas choisi de point de depart : le
    /// premier modele actif s'applique alors a toutes les semaines.
    /// </summary>
    public string? DebutCycle { get; set; }

    /// <summary>Les cinq modeles, dans l'ordre A, B, C, D, E.</summary>
    public List<ModeleApi> Modeles { get; set; } = new();
}

/// <summary>Un modele d'horaires parmi les cinq.</summary>
public sealed class ModeleApi
{
    /// <summary>0 = A, 1 = B, 2 = C, 3 = D, 4 = E.</summary>
    public int Index { get; set; }

    /// <summary>Vrai si ce modele participe a la rotation.</summary>
    public bool Actif { get; set; }

    /// <summary>Les sept journees, du lundi au dimanche.</summary>
    public List<JourDefautApi> Jours { get; set; } = new();

    /// <summary>Nom court du modele : A, B, C, D ou E.</summary>
    public string Nom => Index is >= 0 and < 5 ? "ABCDE"[Index].ToString() : "?";
}

/// <summary>Une journee type dans les echanges JSON.</summary>
public sealed class JourDefautApi
{
    /// <summary>0 = lundi, 6 = dimanche.</summary>
    public int Index { get; set; }

    public string? Arrivee { get; set; }

    public string? PauseDebut { get; set; }

    public string? PauseFin { get; set; }

    public string? Depart { get; set; }
}
