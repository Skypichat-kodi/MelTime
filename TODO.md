# MelTime — tâches à venir

État au 20 novembre 2026, version **1.0.0** publiée.

---

## Améliorations rapides

### Android — alléger l'APK

L'APK pèse 30 Mo parce qu'il embarque quatre architectures (ARM, ARM64, x86, x64)
alors qu'un téléphone n'en utilise qu'une.

- [ ] Ne garder que `android-arm64` et `android-arm` dans le `.csproj`
- [ ] Vérifier que l'installation par `adb` fonctionne toujours
- [ ] Gain attendu : environ 12 Mo (APK sous les 18 Mo)

Contrepartie : plus d'installation sur un émulateur x86.

### Android — reprendre la session au démarrage

Aujourd'hui le mot de passe est redemandé (ou l'empreinte) à chaque lancement.

- [ ] Réutiliser le jeton enregistré, comme le fait le client Windows
- [ ] Ne revenir à la connexion que si le serveur refuse le jeton
- [ ] Prévoir le cas hors ligne : afficher la feuille avec les données en cache,
      ou un message clair

### Android — contraste des heures vides

Sur les lignes du week-end (fond vert sapin), les `--:--` en gris pâle se lisent mal.

- [ ] Éclaircir la couleur des valeurs vides sur fond vert
- [ ] Vérifier également les lignes en congé (fond bleu) et en arrêt (fond rouge)

### Client Windows — finir les boîtes de dialogue

L'accueil utilise la nouvelle boîte au thème, mais huit `MessageBox` subsistent.

- [ ] `App.xaml.cs` — erreurs fatales au démarrage (à laisser en `MessageBox` :
      le thème n'est pas encore chargé)
- [ ] `FenetrePrincipale` — confirmations de déconnexion, erreurs diverses
- [ ] `FenetreHorairesDefaut` — expiration de session

---

## Sujets de fond

### Sécurité du transport

Les échanges HTTP circulent **en clair** : mots de passe et jetons compris.

- [ ] Décider : HTTPS avec certificat auto-signé, ou tunnel (WireGuard, Tailscale)
- [ ] Si certificat : prévoir son renouvellement et son installation sur les clients
- [ ] Documenter le choix, même si c'est « assumé pour un usage personnel »

### Nom de domaine

`skypichat.mooo.com` est intermittent, et le *hairpin NAT* ajoute une dizaine de
secondes depuis le réseau local.

- [ ] Choisir : IP fixe, DNS dynamique plus fiable, ou VPN
- [ ] Adapter la documentation de connexion en conséquence

### Sauvegarde des bases

Un fichier SQLite par client, dans `C:\ProgramData\MelTime`.

- [ ] Prévoir une copie de sauvegarde périodique
- [ ] Vérifier que la restauration fonctionne réellement

---

## Idées, non planifiées

- [ ] Export PDF ou CSV d'un mois de feuille de temps
- [ ] Vue annuelle sous forme de calendrier
- [ ] Notification Android à la fin de journée récapitulant les heures
- [ ] Choix de la langue (français / anglais) pour le Royaume-Uni

---

## Ce qui est fait

Pour mémoire, la version 1.0.0 couvre :

**Serveur** — service Windows, tray d'état, interface d'administration,
un fichier SQLite par client, mots de passe PBKDF2, API HTTP, pipe nommé,
archivage, outils de vérification.

**Client Windows** — feuille de temps hebdomadaire, semaine ISO, thème sombre
sans cadre, cinq modèles d'horaires en rotation, statistiques complètes
(sup, manquantes, différence, brut, net, salaire moyen), congés et arrêts,
horaires par défaut, accueil guidé, version Git affichée.

**Client Android** — connexion par empreinte, saisie jour par jour,
orientation par écran, rappels d'horaires (avant et à l'heure, application
fermée, replanifiés chaque nuit et au démarrage), calendriers par défaut,
contrat, salaires, statistiques, accueil guidé, version Git affichée.

**Publication** — GitVersion, installeurs Inno Setup, release GitHub avec
les trois fichiers.
