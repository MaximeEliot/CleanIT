# CleanIT

Logiciel de nettoyage de disque pour Windows 10 et 11, écrit en C# / .NET 8 (WPF, MVVM).

CleanIT analyse les fichiers inutiles (temporaires, caches, rapports d'erreurs…), affiche ce qui peut être
récupéré **avant** toute suppression, puis nettoie uniquement après confirmation explicite. Il comprend aussi
un analyseur d'espace disque en lecture seule.

> ⚠️ **Avertissement de sécurité**
>
> CleanIT supprime des fichiers. Par défaut ils sont envoyés à la **corbeille** et restent restaurables ;
> la suppression définitive est une option à activer explicitement. Vider la catégorie « Corbeille »
> est, par nature, **irréversible**.
>
> Utilisez d'abord le **mode simulation** pour voir exactement ce qui serait supprimé. Le logiciel est
> fourni « tel quel », sans garantie : sauvegardez vos données importantes.

## Fonctionnalités

| Onglet | Description |
|---|---|
| **Nettoyage** | Catégories cochables, analyse, rapport par catégorie (taille, nombre, plus gros fichiers), confirmation, barre de progression, annulation, résumé final (espace libéré, erreurs) et export du rapport. |
| **Analyseur d'espace** | Arborescence d'un lecteur ou d'un dossier triée par taille, et les 50 plus gros fichiers (triables par taille, date ou chemin). Suppression des fichiers sélectionnés, après confirmation ; les emplacements système restent protégés. |
| **Paramètres** | Exclusions (dossiers, fichiers, motifs comme `*.docx`), suppression définitive, âge minimal des fichiers temporaires, accès aux journaux. |

Catégories de la V1 :

| Catégorie | Emplacement | Admin |
|---|---|---|
| Fichiers temporaires de l'utilisateur | `%TEMP%` (fichiers de plus de 24 h) | non |
| Fichiers temporaires du système | `C:\Windows\Temp` | oui |
| Corbeille | toutes les corbeilles (API Shell) | non |
| Cache de Chrome / Edge | `User Data\<profil>\Cache`, `Code Cache`, `GPUCache`, caches de shaders | non |
| Cache de Firefox | `%LOCALAPPDATA%\Mozilla\Firefox\Profiles\*\cache2` | non |
| Miniatures | `Explorer\thumbcache_*.db`, `iconcache_*.db` | non |
| Rapports d'erreurs Windows | WER utilisateur (`%LOCALAPPDATA%`) et système (`C:\ProgramData`, admin) | partiel |
| Anciens fichiers de mise à jour | `C:\Windows\SoftwareDistribution\Download` | oui |

Pas de nettoyage du registre en V1.

## Garanties de sécurité

- **Aucune suppression sans analyse ni confirmation.** Le moteur de nettoyage ne prend en entrée qu'un rapport
  d'analyse et refuse de s'exécuter sans le drapeau de confirmation de l'utilisateur (sauf en simulation).
- **Confirmation à l'épreuve des fausses manipulations** : la fenêtre de confirmation n'a pas de raccourci clavier,
  « Annuler » est le choix par défaut (Entrée, Échap) et le bouton de suppression n'est cliquable qu'après une seconde.
- **Suppression manuelle (analyseur d'espace)** : seuls les fichiers choisis par l'utilisateur, jamais les dossiers ni
  les liens. Les dossiers personnels sont autorisés, mais `Windows`, `Program Files`, `ProgramData`, les fichiers
  système (`pagefile.sys`, `hiberfil.sys`, `ntuser.dat`…) et les données sensibles des navigateurs restent interdits.
  Un avertissement s'affiche pour les fichiers d'`AppData`.
- **Corbeille par défaut** ; suppression définitive uniquement si elle est activée dans les paramètres, avec un avertissement.
- **Liste noire codée en dur et testée** (`ProtectedPathPolicy`) : `System32`, `WinSxS`, `Program Files`,
  racines de lecteurs, `C:\Users`, dossiers personnels (Documents, Bureau, Images, Téléchargements, OneDrive…),
  `AppData\Roaming`, fichiers sensibles des navigateurs (mots de passe, cookies, historique, favoris)
  et fichiers système (`pagefile.sys`, `ntuser.dat`…). `Windows`, `ProgramData` et le profil sont interdits
  par défaut, sauf quelques exceptions explicites (`Windows\Temp`, dossiers de cache…).
- **Triple contrôle de chaque fichier**, à l'analyse puis juste avant la suppression : il doit se trouver sous un
  dossier de la catégorie, ne pas être protégé, et ne pas être exclu par l'utilisateur.
- **Jonctions et liens symboliques jamais suivis** : un lien dans `%TEMP%` vers « Documents » ne peut pas vider « Documents ».
- **Fichiers verrouillés, accès refusés, chemins longs** : ignorés, journalisés, et le nettoyage continue.
- **Mode simulation (dry-run)** : liste ce qui serait supprimé sans rien toucher (exportable en `.txt`).
- **Élévation à la demande** : CleanIT démarre sans droits administrateur et ne propose de se relancer en
  administrateur que si une catégorie cochée l'exige.

## Prérequis

- Windows 10 ou 11 (x64)
- Pour compiler : [SDK .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0)
- Pour exécuter la version publiée : rien (exécutable autonome)

## Compilation et tests

```powershell
dotnet build
dotnet test
```

La solution compile sans avertissement (`TreatWarningsAsErrors` et analyseurs .NET activés).
Les tests (xUnit) utilisent un système de fichiers en mémoire (`System.IO.Abstractions.TestingHelpers`) et
ne touchent jamais au vrai disque.

Lancer l'application en développement :

```powershell
dotnet run --project src/CleanIT.App
```

## Publication

Depuis la racine du dépôt :

```powershell
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

Résultat : un unique exécutable autonome (~64 Mo, aucun runtime .NET requis chez l'utilisateur) dans
`src/CleanIT.App/bin/Release/net8.0-windows/win-x64/publish/CleanIT.exe`.

## Journaux et paramètres

| Fichier | Emplacement |
|---|---|
| Journaux (Serilog, rotation quotidienne, 10 Mo max par fichier, 14 fichiers conservés) | `%LOCALAPPDATA%\CleanIT\logs\cleanit-AAAAMMJJ.log` |
| Paramètres | `%APPDATA%\CleanIT\settings.json` |

Chaque fichier supprimé (ou qui le serait en simulation), chaque erreur et chaque confirmation sont journalisés.

## Architecture

```
src/CleanIT.Core    Logique métier, sans dépendance à l'interface (règles, analyse, nettoyage, sécurité)
src/CleanIT.App     Interface WPF, MVVM (CommunityToolkit.Mvvm), injection de dépendances, Serilog
tests/CleanIT.Tests Tests unitaires xUnit
```

Détails : [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

### Ajouter une catégorie

1. Créer une classe dans `src/CleanIT.Core/Rules` qui hérite de `DirectoryCleanerRule` (ou implémente `ICleanerRule`)
   et déclare ses dossiers cibles via `GetTargets`.
2. L'ajouter à `DefaultRules.Create`.
3. Si ses dossiers se trouvent dans `Windows`, `ProgramData` ou le profil, ajouter une exception explicite dans
   `ProtectedPathPolicy` **et** un test dans `ProtectedPathPolicyTests`. Le test
   `DefaultRules_AllTargetsAreSafeRootsWhoseContentIsCleanable` échoue tant que ce n'est pas fait.

## Limites connues

- **Corbeille et chemins longs** : l'API Shell ne sait pas envoyer à la corbeille un chemin de plus de 260 caractères ;
  ces fichiers sont ignorés (et journalisés) sauf en mode suppression définitive.
- **Fichier trop gros pour la corbeille** : Windows affiche alors sa propre demande de confirmation avant une
  suppression définitive (CleanIT ne la contourne pas).
- **Miniatures** : l'Explorateur garde certains fichiers `thumbcache_*.db` ouverts ; ils apparaissent comme verrouillés.
- **Navigateurs** : le cache n'est nettoyé que si le navigateur est fermé (Edge peut rester actif en arrière-plan).
- **Élévation avec un autre compte** : si l'invite UAC est validée avec un compte administrateur différent,
  les catégories « utilisateur » (`%TEMP%`, caches) concernent le profil de ce compte.
- **Microsoft Defender** : pendant le développement, une version du projet de tests (qui manipulait des chemins
  de fichiers de navigateurs) a été signalée à tort comme `Trojan:MSIL/Stealer`. Les données de test ont été
  adaptées ; si le problème réapparaît, il s'agit d'un faux positif à
  [signaler à Microsoft](https://www.microsoft.com/wdsi/filesubmission).
