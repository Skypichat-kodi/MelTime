using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using MelTime.Core.Protocole;

namespace MelTime.Service.Pipe;

/// <summary>
/// Ecoute du pipe nomme : une connexion a la fois, une requete, une reponse.
/// Chaque requete est traitee dans le contexte du service (compte SYSTEM),
/// ce qui evite tout acces concurrent des interfaces a la base.
/// </summary>
public sealed class ServeurPipe
{
    private static readonly TimeSpan DelaiLecture = TimeSpan.FromSeconds(10);

    private readonly TraitementRequetes _traitement;
    private readonly Action<string> _journaliser;

    public ServeurPipe(TraitementRequetes traitement, Action<string> journaliser)
    {
        _traitement = traitement ?? throw new ArgumentNullException(nameof(traitement));
        _journaliser = journaliser ?? throw new ArgumentNullException(nameof(journaliser));
    }

    /// <summary>Boucle d'ecoute. Se termine quand le jeton d'arret est annule.</summary>
    public async Task ExecuterAsync(CancellationToken arret)
    {
        _journaliser($"Ecoute du pipe : {PipeMelTime.CheminComplet}");

        while (!arret.IsCancellationRequested)
        {
            NamedPipeServerStream? serveur = null;

            try
            {
                serveur = new NamedPipeServerStream(
                    PipeMelTime.Nom,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await serveur.WaitForConnectionAsync(arret).ConfigureAwait(false);
                await EchangerAsync(serveur, arret).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException ex)
            {
                // Client qui se coupe brutalement : on ne doit pas tuer le service pour autant.
                _journaliser($"Connexion interrompue : {ex.Message}");
                await Task.Delay(200).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _journaliser($"Erreur du serveur de pipe : {ex.Message}");
                await Task.Delay(500).ConfigureAwait(false);
            }
            finally
            {
                serveur?.Dispose();
            }
        }

        _journaliser("Ecoute du pipe arretee.");
    }

    private async Task EchangerAsync(NamedPipeServerStream serveur, CancellationToken arret)
    {
        using var lecteur = new StreamReader(serveur, new UTF8Encoding(false), false, 1024, leaveOpen: true);
        using var ecrivain = new StreamWriter(serveur, new UTF8Encoding(false), 1024, leaveOpen: true);

        string? json;

        // Un client muet ne doit pas bloquer le service indefiniment.
        using (var limite = CancellationTokenSource.CreateLinkedTokenSource(arret))
        {
            limite.CancelAfter(DelaiLecture);
            try
            {
                json = await lecteur.ReadLineAsync(limite.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        var reponse = Traiter(json);

        await ecrivain.WriteLineAsync(ProtocoleJson.Serialiser(reponse)).ConfigureAwait(false);
        await ecrivain.FlushAsync().ConfigureAwait(false);
    }

    private Reponse Traiter(string json)
    {
        try
        {
            var requete = ProtocoleJson.DeserialiserRequete(json);
            return requete is null
                ? Reponse.Echec("Requete vide ou illisible.")
                : _traitement.Executer(requete);
        }
        catch (JsonException)
        {
            return Reponse.Echec("Requete invalide : JSON mal forme.");
        }
        catch (Exception ex)
        {
            return Reponse.Echec($"Erreur inattendue : {ex.Message}");
        }
    }
}
