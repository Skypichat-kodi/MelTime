# MelTime

Feuille de temps hebdomadaire : un serveur Windows, un client Windows et un client Android.

MelTime calcule les heures travaillées à partir des horaires saisis, applique le contrat
de chaque client, et présente les heures supplémentaires comme les manques — sans que
personne ait à les compter à la main.

---

## Fonctionnalités

**Feuille de temps** — sept jours, semaine ISO 8601, calcul `(départ − arrivée) − pause`.
Les horaires doivent se succéder dans l'ordre du jour : le travail de nuit n'est pas accepté.

**Contrat et heures** — chaque semaine porte son contrat. Au-delà de 35 h, les heures
supplémentaires ; en dessous du contrat, les manques. Les deux se cumulent séparément,
sans jamais se compenser d'une semaine à l'autre.

**Statistiques** — par semaine, par mois et sur 52 semaines glissantes : heures
supplémentaires, heures manquantes, différence, et le précalcul des montants brut et net
selon les salaires enregistrés.

**Horaires par défaut** — jusqu'à cinq modèles (A à E), qui alternent d'une semaine à
l'autre à partir d'une semaine de départ. Ils préremplissent les semaines jamais saisies.

**Absences** — congés payés et arrêts maladie. Une journée d'absence est payée au volume
prévu : elle ne crée aucun manque par rapport au contrat.

**Rappels d'horaires sur Android** — notification cinq minutes avant et à l'heure précise
de chaque changement de la journée : arrivée, pause, reprise, départ. Elles arrivent
application fermée, et sont replanifiées chaque nuit ainsi qu'au démarrage du téléphone.

**Connexion par empreinte** — sur Android, la boîte de dialogue d'Android autorise la
relecture du mot de passe, conservé dans le coffre chiffré du système.

---

## Architecture

| Composant | Rôle | Technologie |
|---|---|---|
| `MelTime.Core` | Calculs, protocole, jours fériés | .NET 8 |
| `MelTime.Service` | Service Windows, bases, API HTTP, pipe nommé | .NET 8 (console) |
| `MelTime.Tray` | Icône d'état, notifications de connexion | .NET 8 (WinForms) |
| `MelTime.UI` | Interface d'administration | .NET 8 (WinForms) |
| `MelTimeClientWindows` | Feuille de temps | .NET 8 (WPF) |
| `MelTimeClientAndroid` | Feuille de temps | .NET 10 (MAUI) |

**Un fichier SQLite par client** (`client-{id}.db`), plus `meltime.admin.db` pour les
comptes. Les mots de passe ne sont jamais conservés : seule leur empreinte PBKDF2-SHA256
l'est.

**Deux canaux de communication** : un pipe nommé local pour l'administration, et une API
HTTP JSON pour les clients.

---

## Prérequis

| Outil | Version | Pour |
|---|---|---|
| .NET SDK | 8.0 | Serveur et client Windows |
| .NET SDK | 10.0 | Client Android |
| Workload `maui-android` | 10.x | Client Android |
| JDK | 17 | Client Android |
| Android SDK | API 36, build-tools 36 | Client Android |
| Inno Setup | 6 | Installeurs |

Renseignez une fois pour toutes :

```
setx JAVA_HOME "C:\jdk"
setx ANDROID_HOME "C:\android-sdk"
```

> Le client Android cible .NET 10 alors que le reste de la solution reste en .NET 8 :
> les bibliothèques AndroidX récentes sont compilées en Java 21, que seul le SDK
> Android 36 sait traiter. Deux fichiers `global.json` cohabitent pour cette raison.

---

## Compilation

Trois scripts, à la racine du dépôt :

```
build-serveur.bat      ->  Installers\MelTimeServeur-{version}.exe
build-client.bat       ->  Installers\MelTimeClient-{version}.exe
build-android.bat      ->  Apk\MelTimeClientAndroid.apk
```

Chacun accepte `--auto` : pas de pause, pas d'ouverture de dossier, pour un enchaînement
automatique.

La version provient de **GitVersion**, qui lit l'historique et les tags Git. Publier une
version revient à poser un tag :

```
git tag -a v1.1.0 -m "Version 1.1.0"
git push origin v1.1.0
```

---

## Installation

**Serveur** — lancer l'installeur, qui place les fichiers dans `C:\ProgramData\MelTime`
et `C:\Program Files\MelTime`, puis crée deux tâches planifiées : le service (sous SYSTEM)
et le tray. L'interface d'administration demande l'élévation, nécessaire pour ouvrir le
pipe du service.

**Client Windows** — l'installeur suffit. La première ouverture demande l'adresse du
serveur, l'identifiant et le mot de passe.

**Android** — installer l'APK, puis saisir l'adresse du serveur (IP ou nom de domaine),
son port, l'identifiant et le mot de passe.

Au premier démarrage, les deux clients guident la saisie des salaires puis des horaires
par défaut, sans quoi les calculs resteraient vides.

---

## Configuration du serveur

`C:\ProgramData\MelTime\meltime.config.json`

```json
{
  "port": 5080,
  "hote": "+",
  "dureeSessionJours": 30
}
```

Les journaux se trouvent dans `logs\service-AAAAMMJJ.log`, tronqués au démarrage.

Outils de diagnostic, en ligne de commande :

```
MelTime.Service.exe --verifier-donnees
MelTime.Service.exe --verifier-pipe
MelTime.Service.exe --verifier-calcul
MelTime.Service.exe --verifier-http
MelTime.Service.exe --verifier-agenda
```

---

## Structure du dépôt

```
MelTime.Core/               calculs et protocole, sans dépendance à l'interface
MelTime.Server/
  MelTime.Service/          service Windows, bases, API
  MelTime.Tray/             icône d'état
  MelTime.UI/               administration
MelTimeClientWindows/       client WPF
MelTimeClientAndroid/       client MAUI
inno/                       scripts des installeurs
build-*.bat                 scripts de compilation
```

---

## Licence

[MIT](LICENSE) — utilisation, modification et redistribution libres, y compris
commerciales, sous réserve de conserver la mention de copyright.

Le logiciel est fourni en l'état, sans garantie d'aucune sorte.

---

## État du projet

Version **1.0.0**. Les tâches restantes sont consignées dans [TODO.md](TODO.md).
