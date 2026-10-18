# Architecture de CleanIT

## Vue d'ensemble

```
CleanIT.App (WPF)                     CleanIT.Core (aucune dépendance UI)
┌──────────────────────┐              ┌──────────────────────────────────────────────┐
│ Views (XAML)         │              │ Rules/        ICleanerRule (déclaratives)     │
│ ViewModels (MVVM)  ──┼── appelle ──▶│ Scanning/     ScanEngine → ScanReport         │
│ Services (dialogues, │              │ Cleaning/     CleanupEngine → CleanupResult   │
│  élévation, réglages)│              │ Safety/       ProtectedPathPolicy, Guard      │
│ Serilog + DI         │              │ DiskAnalysis/ DiskAnalyzer                    │
└──────────────────────┘              │ Settings/     AppSettings, SettingsStore      │
                                      │ Platform/     Shell32 (corbeille), env.       │
                                      └──────────────────────────────────────────────┘
```

Le Core accède au disque uniquement via `IFileSystem` (System.IO.Abstractions), ce qui permet de tester
toute la logique sur un système de fichiers en mémoire.

## Flux de nettoyage

```
Règles cochées ──▶ ScanEngine ──▶ ScanReport ──▶ confirmation ──▶ CleanupEngine ──▶ CleanupResult
                    (lecture seule)   (rapport affiché)  (utilisateur)   (revalide chaque fichier)
```

1. **Règles déclaratives.** Une règle (`ICleanerRule`) ne supprime rien : elle décrit ses dossiers cibles
   (`ScanTarget` : racine, motif de nom, récursivité, âge minimal) et sa disponibilité (logiciel installé,
   navigateur fermé, droits administrateur). Seule la corbeille, qui se vide via l'API Shell, implémente
   `ICustomCleanupRule`.
2. **Analyse.** `ScanEngine` énumère les cibles avec `SafeFileEnumerator`, qui ne suit jamais les points
   d'analyse (jonctions, liens) et ignore les dossiers inaccessibles. Chaque fichier passe par `CleanupGuard`.
3. **Rapport.** `ScanReport` contient, par catégorie, les fichiers éligibles, la taille totale, les racines
   autorisées (capturées à l'analyse) et un échantillon des éléments ignorés avec leur raison.
4. **Nettoyage.** `CleanupEngine.CleanAsync` exige un `ScanReport` et `UserConfirmed = true` (hors simulation).
   Pour chaque fichier : nouveau passage par `CleanupGuard` (les exclusions ont pu changer), vérification que
   c'est toujours un fichier ordinaire, puis suppression via l'`IFileDeleter` du mode choisi. Les erreurs sont
   collectées sans interrompre le traitement. Les dossiers vidés sont retirés du plus profond au moins profond,
   par suppression non récursive, et jamais la racine d'une catégorie. En simulation, aucun `IFileDeleter`
   n'est appelé.

## Sécurité : `CleanupGuard` et `ProtectedPathPolicy`

Un chemin n'est supprimable que si les trois conditions sont réunies :

1. il est **strictement sous** une racine déclarée par la règle ;
2. il n'est **pas protégé** par la politique codée en dur ;
3. il n'est **pas exclu** par l'utilisateur.

`ProtectedPathPolicy` fonctionne sur trois niveaux :

| Niveau | Exemples | Effet |
|---|---|---|
| Toujours protégé | `System32`, `WinSxS`, `Program Files`, `Documents`, `AppData\Roaming`, `Cookies`, `Login Data`, `places.sqlite`, `pagefile.sys` | Rien ne peut lever la protection |
| Interdit par défaut | `Windows`, `ProgramData`, `Users`, profil, `AppData\Local` | Protégé sauf exceptions explicites |
| Exceptions explicites | `Windows\Temp`, `SoftwareDistribution\Download`, `WER\ReportArchive`, `Chrome\User Data\*\Cache`, `Firefox\Profiles\*\cache2`, `%TEMP%` | Seul le **contenu** est autorisé, jamais le dossier lui-même |

Les chemins sont normalisés avant comparaison (`PathNormalizer`) : chemins absolus locaux uniquement,
résolution de `..`, suppression du préfixe `\\?\`, points et espaces finaux, insensibilité à la casse.
Les chemins réseau, relatifs, avec flux de données alternatifs ou noms courts 8.3 (`PROGRA~1`) sont refusés.
`%TEMP%` n'est accepté comme exception que s'il ne contient aucun dossier critique (un `TEMP` mal configuré
vers `C:\Users\Alice` n'ouvre pas le profil).

## Modèle de données principal

| Type | Rôle |
|---|---|
| `ScanTarget` | Dossier ciblé par une règle (racine, motif, récursivité, âge minimal) |
| `CleanupItem` | Fichier éligible : chemin, taille, date de modification |
| `CategoryScanResult` | Résultat d'une règle : disponibilité, racines autorisées, éléments, ignorés, total |
| `ScanReport` | Ensemble des catégories analysées, horodaté |
| `CleanupRequest` | Catégories retenues, mode (`RecycleBin`, `Permanent`, `DryRun`), confirmation, exclusions |
| `CleanupResult` | Espace libéré, nombre supprimé, erreurs typées (`CleanupErrorKind`), liste dry-run, annulation |
| `DirectoryNode` / `FileEntry` | Arbre et plus gros fichiers de l'analyseur d'espace |
| `AppSettings` | Exclusions, suppression définitive, âge minimal, catégories cochées |

## Asynchronisme

Les moteurs exposent des méthodes `…Async` qui exécutent le travail sur le pool de threads (`Task.Run`),
acceptent un `CancellationToken` et publient leur progression via `IProgress<T>`. Côté UI, les commandes
`AsyncRelayCommand` (CommunityToolkit.Mvvm) génèrent les commandes d'annulation. Une analyse annulée lève
`OperationCanceledException` (aucun rapport partiel) ; un nettoyage annulé renvoie un résultat partiel.

## Élévation

L'application démarre en `asInvoker`. Les règles qui l'exigent (`RequiresElevation`) sont signalées.
À l'analyse, si l'une d'elles est cochée sans droits suffisants, CleanIT propose de se relancer via `runas`
en conservant la sélection (`--select=id1,id2`). Sinon, ces catégories sont simplement ignorées.

## Journalisation

Le Core n'utilise que `Microsoft.Extensions.Logging.Abstractions`, avec des messages générés à la compilation
(`CoreLog`). L'application branche Serilog : un fichier par jour, 10 Mo maximum, 14 fichiers conservés.
