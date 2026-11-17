using MelTime.Core.Modeles;
using MelTime.Core.Protocole;
using MelTime.Service.Donnees;

namespace MelTime.Service.Pipe;

/// <summary>
/// Execute les commandes recues sur le pipe.
/// C'est le seul endroit ou les requetes touchent la base : le service reste
/// maitre des donnees, les interfaces ne font que demander.
/// </summary>
public sealed class TraitementRequetes
{
    private readonly BaseAdministration _baseAdministration;
    private readonly Action<string> _journaliser;

    public TraitementRequetes(BaseAdministration baseAdministration, Action<string>? journaliser = null)
    {
        _baseAdministration = baseAdministration ?? throw new ArgumentNullException(nameof(baseAdministration));
        _journaliser = journaliser ?? (_ => { });
    }

    public Reponse Executer(Requete requete)
    {
        ArgumentNullException.ThrowIfNull(requete);

        try
        {
            return requete.Commande switch
            {
                Commandes.ServiceVivant => Reponse.Info("MelTime.Service actif."),
                Commandes.ClientsLister => Reponse.Ok(_baseAdministration.Lister()),
                Commandes.ClientsObtenir => Obtenir(requete),
                Commandes.ClientsCreer => Creer(requete),
                Commandes.ClientsModifier => Modifier(requete),
                Commandes.ClientsChangerMotDePasse => ChangerMotDePasse(requete),
                Commandes.ClientsSupprimer => Supprimer(requete),
                _ => Reponse.Echec($"Commande inconnue : « {requete.Commande} ».")
            };
        }
        catch (InvalidOperationException ex)
        {
            // Erreur metier attendue (validation, doublon) : le message est destine a l'utilisateur.
            return Reponse.Echec(ex.Message);
        }
        catch (Exception ex)
        {
            return Reponse.Echec($"Erreur inattendue du service : {ex.Message}");
        }
    }

    private Reponse Obtenir(Requete requete)
    {
        if (requete.Id is not int id)
        {
            return Reponse.Echec("L'identifiant du client est obligatoire.");
        }

        var client = _baseAdministration.Obtenir(id);
        return client is null
            ? Reponse.Echec("Ce client n'existe pas ou plus.")
            : Reponse.Ok(client);
    }

    private Reponse Creer(Requete requete)
    {
        if (requete.Client is not Client client)
        {
            return Reponse.Echec("Les donnees du client sont obligatoires.");
        }

        if (string.IsNullOrEmpty(requete.MotDePasse))
        {
            return Reponse.Echec("Le mot de passe est obligatoire.");
        }

        var id = _baseAdministration.Creer(client, requete.MotDePasse);
        return Reponse.Ok(_baseAdministration.Obtenir(id)!);
    }

    private Reponse Modifier(Requete requete)
    {
        if (requete.Client is not Client client)
        {
            return Reponse.Echec("Les donnees du client sont obligatoires.");
        }

        if (client.Id <= 0)
        {
            return Reponse.Echec("L'identifiant du client est obligatoire.");
        }

        _baseAdministration.Modifier(client);
        return Reponse.Ok(_baseAdministration.Obtenir(client.Id)!);
    }

    private Reponse ChangerMotDePasse(Requete requete)
    {
        if (requete.Id is not int id)
        {
            return Reponse.Echec("L'identifiant du client est obligatoire.");
        }

        if (string.IsNullOrEmpty(requete.MotDePasse))
        {
            return Reponse.Echec("Le mot de passe est obligatoire.");
        }

        _baseAdministration.ChangerMotDePasse(id, requete.MotDePasse);
        return Reponse.Ok();
    }

    private Reponse Supprimer(Requete requete)
    {
        if (requete.Id is not int id)
        {
            return Reponse.Echec("L'identifiant du client est obligatoire.");
        }

        // Les donnees de travail sont archivees avant la disparition du compte :
        // une suppression ne doit jamais detruire d'heures declarees.
        var client = _baseAdministration.Obtenir(id);

        if (client is not null)
        {
            var archive = ArchivageClients.Archiver(CheminDonnees.BaseClient(id), client);

            if (archive is not null)
            {
                _journaliser($"Donnees de « {client.Identifiant} » archivees : {archive}");
            }
        }

        _baseAdministration.Supprimer(id);
        return Reponse.Ok();
    }
}
