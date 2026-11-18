namespace MelTime.Core.Api;

/// <summary>
/// Reglages personnels du client. Ils restent sur son compte, dans sa propre
/// base, et ne sont jamais visibles par un autre client.
/// </summary>
public sealed class ReglagesApi
{
    /// <summary>Salaire brut mensuel, en euros.</summary>
    public decimal? SalaireBrut { get; set; }

    /// <summary>Salaire net mensuel, en euros.</summary>
    public decimal? SalaireNet { get; set; }
}
