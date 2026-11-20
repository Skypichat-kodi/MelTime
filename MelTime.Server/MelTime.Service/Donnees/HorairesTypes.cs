using MelTime.Core.Agenda;
using MelTime.Core.Modeles;

namespace MelTime.Service.Donnees;

/// <summary>
/// Horaires types d'un client : jusqu'a cinq modeles notes de A a E, et un
/// point de depart de cycle.
///
/// Le modele applicable a une semaine se deduit de sa position dans le cycle :
/// la semaine de depart porte le premier modele actif, la suivante le deuxieme,
/// et ainsi de suite en recommencant. Les modeles desactives sont ignores,
/// comme s'ils n'existaient pas.
/// </summary>
public sealed class HorairesTypes
{
    public const int NombreDeModeles = 5;

    /// <summary>Noms des modeles, dans l'ordre.</summary>
    public const string Noms = "ABCDE";

    public HorairesTypes()
    {
        for (var index = 0; index < NombreDeModeles; index++)
        {
            Modeles[index] = Enumerable.Range(0, 7).Select(_ => new HorairesJour()).ToArray();

            // Seul le modele A est actif au depart : le comportement reste
            // celui d'avant l'arrivee des cycles.
            Actifs[index] = index == 0;
        }
    }

    /// <summary>Lundi de la semaine portant le premier modele actif.</summary>
    public DateOnly? DebutCycle { get; set; }

    /// <summary>Sept journees par modele.</summary>
    public HorairesJour[][] Modeles { get; } = new HorairesJour[NombreDeModeles][];

    /// <summary>Vrai pour chaque modele participant a la rotation.</summary>
    public bool[] Actifs { get; } = new bool[NombreDeModeles];

    /// <summary>Vrai si aucun modele n'est actif : aucune semaine n'est prereplie.</summary>
    public bool EstVide => !Actifs.Any(actif => actif);

    /// <summary>Indices des modeles actifs, dans l'ordre A, B, C, D, E.</summary>
    public IReadOnlyList<int> ModelesActifs =>
        Enumerable.Range(0, NombreDeModeles).Where(index => Actifs[index]).ToArray();

    /// <summary>
    /// Modele applicable a une semaine donnee. Null si aucun modele n'est actif.
    /// </summary>
    public HorairesJour[]? ModelePour(int anneeIso, int numero)
    {
        var actifs = ModelesActifs;

        if (actifs.Count == 0)
        {
            return null;
        }

        // Sans point de depart, le premier modele actif s'applique partout.
        if (DebutCycle is not { } debut)
        {
            return Modeles[actifs[0]];
        }

        var lundi = SemaineTravail.LundiDeLaSemaine(anneeIso, numero);
        var lundiDepart = SemaineTravail.LundiDeLaSemaine(
            SemaineTravail.SemaineIsoDe(debut).AnneeIso,
            SemaineTravail.SemaineIsoDe(debut).Numero);

        var ecart = (lundi.DayNumber - lundiDepart.DayNumber) / 7;
        var position = ecart % actifs.Count;

        // Une semaine anterieure au depart doit aussi tomber dans le cycle.
        if (position < 0)
        {
            position += actifs.Count;
        }

        return Modeles[actifs[position]];
    }
}
