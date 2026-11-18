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
    private readonly Action? _demanderArret;
    private readonly Configuration? _configuration;

    public TraitementRequetes(
        BaseAdministration baseAdministration,
        Action<string>? journaliser = null,
        Action? demanderArret = null,
        Configuration? configuration = null)
    {
        _baseAdministration = baseAdministration ?? throw new ArgumentNullException(nameof(baseAdministration));
        _journaliser = journaliser ?? (_ => { });
        _demanderArret = demanderArret;
        _configuration = configuration;
    }

    public Reponse Executer(Requete requete)
    {
        ArgumentNullException.ThrowIfNull(requete);

        try
        {
            return requete.Commande switch
            {
                Commandes.ServiceVivant => Reponse.Info("MelTime.Service actif."),
                Commandes.ServiceArreter => ArreterService(),
                Commandes.ConfigurationLire => LireConfiguration(),
                Commandes.ConfigurationEcrire => EcrireConfiguration(requete),
                Commandes.SessionsLister => Reponse.OkSessions(_baseAdministration.ListerSessionsActives()),
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

    private Reponse ArreterService()
    {
        _journaliser("Arret du service demande par une interface.");
        _demanderArret?.Invoke();
        return Reponse.Info("Arret du service demande.");
    }

    private Reponse LireConfiguration()
    {
        if (_configuration is null)
        {
            return Reponse.Echec("Configuration indisponible.");
        }

        return Reponse.OkConfiguration(new ConfigurationServeur
        {
            Port = _configuration.Port,
            Hote = _configuration.Hote,
            DureeSessionJours = _configuration.DureeSessionJours
        });
    }

    private Reponse EcrireConfiguration(Requete requete)
    {
        if (_configuration is null)
        {
            return Reponse.Echec("Configuration indisponible.");
        }

        if (requete.Configuration is not ConfigurationServeur demandee)
        {
            return Reponse.Echec("Configuration absente.");
        }

        if (demandee.Port is < 1 or > 65535)
        {
            return Reponse.Echec("Le port doit etre compris entre 1 et 65535.");
        }

        if (string.IsNullOrWhiteSpace(demandee.Hote))
        {
            return Reponse.Echec("L'hote d'ecoute est obligatoire.");
        }

        if (demandee.DureeSessionJours is < 1 or > 365)
        {
            return Reponse.Echec("La duree de session doit etre comprise entre 1 et 365 jours.");
        }

        _configuration.Port = demandee.Port;
        _configuration.Hote = demandee.Hote.Trim();
        _configuration.DureeSessionJours = demandee.DureeSessionJours;
        _configuration.Enregistrer();

        _journaliser($"Configuration enregistree : port {_configuration.Port}, hote « {_configuration.Hote} ».");
        _journaliser("Redemarrage du service necessaire pour appliquer le nouveau port.");

        return Reponse.OkConfiguration(new ConfigurationServeur
        {
            Port = _configuration.Port,
            Hote = _configuration.Hote,
            DureeSessionJours = _configuration.DureeSessionJours
        });
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
