using System.IO.Pipes;
using System.Text;

namespace MelTime.Core.Protocole;

/// <summary>
/// Client du pipe nomme. Ecrit une seule fois, utilise par l'UI et par le Tray :
/// ouvrir une connexion, envoyer une requete, lire la reponse, refermer.
/// </summary>
public sealed class ClientPipe
{
    private static readonly TimeSpan DelaiConnexion = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan DelaiReponse = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Envoie une requete au service et retourne sa reponse.
    /// </summary>
    /// <exception cref="TimeoutException">Le service ne repond pas dans le delai imparti.</exception>
    /// <exception cref="UnauthorizedAccessException">Droits insuffisants pour ouvrir le pipe.</exception>
    /// <exception cref="IOException">Communication interrompue ou reponse illisible.</exception>
    public async Task<Reponse> EnvoyerAsync(Requete requete, CancellationToken arret = default)
    {
        ArgumentNullException.ThrowIfNull(requete);

        using var pipe = new NamedPipeClientStream(
            serverName: ".",
            pipeName: PipeMelTime.Nom,
            direction: PipeDirection.InOut,
            options: PipeOptions.Asynchronous);

        using (var limite = CancellationTokenSource.CreateLinkedTokenSource(arret))
        {
            limite.CancelAfter(DelaiConnexion);
            try
            {
                await pipe.ConnectAsync(limite.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!arret.IsCancellationRequested)
            {
                throw new TimeoutException("Le service MelTime ne repond pas (connexion impossible).");
            }
        }

        using var lecteur = new StreamReader(pipe, new UTF8Encoding(false), false, 1024, leaveOpen: true);
        using var ecrivain = new StreamWriter(pipe, new UTF8Encoding(false), 1024, leaveOpen: true);

        await ecrivain.WriteLineAsync(ProtocoleJson.Serialiser(requete)).ConfigureAwait(false);
        await ecrivain.FlushAsync().ConfigureAwait(false);

        using var limiteReponse = CancellationTokenSource.CreateLinkedTokenSource(arret);
        limiteReponse.CancelAfter(DelaiReponse);

        string? ligne;
        try
        {
            ligne = await lecteur.ReadLineAsync(limiteReponse.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!arret.IsCancellationRequested)
        {
            throw new TimeoutException("Le service MelTime ne repond pas (delai de reponse depasse).");
        }

        if (ligne is null)
        {
            throw new IOException("Le service a ferme la connexion sans repondre.");
        }

        return ProtocoleJson.DeserialiserReponse(ligne)
               ?? throw new IOException("Reponse illisible recue du service.");
    }
}
