using System.Diagnostics;
using System.IO.Abstractions;
using CleanIT.Core.Abstractions;
using CleanIT.Core.Logging;
using CleanIT.Core.Models;
using CleanIT.Core.Safety;
using CleanIT.Core.Scanning;
using Microsoft.Extensions.Logging;

namespace CleanIT.Core.Cleaning;

/// <summary>Result of the pre-check of a file the user wants to delete.</summary>
public sealed record ManualDeletionCheck(string FullPath, bool IsAllowed, string? RefusalReason, bool IsApplicationData);

/// <summary>
/// Deletes files explicitly picked by the user (e.g. from the largest files list).
/// Same guarantees as the cleanup engine: explicit confirmation, Recycle Bin by default,
/// hard-coded protection of system locations, user exclusions, links never followed,
/// failures reported without stopping.
/// </summary>
public sealed class ManualDeletionService
{
    public const string RuleId = "manual";

    private readonly IFileSystem _fileSystem;
    private readonly IProtectedPathPolicy _policy;
    private readonly Dictionary<CleanupMode, IFileDeleter> _deleters;
    private readonly ILogger<ManualDeletionService> _logger;

    public ManualDeletionService(
        IFileSystem fileSystem,
        IProtectedPathPolicy policy,
        IEnumerable<IFileDeleter> deleters,
        ILogger<ManualDeletionService> logger)
    {
        _fileSystem = fileSystem;
        _policy = policy;
        _deleters = deleters.ToDictionary(d => d.Mode);
        _logger = logger;
    }

    /// <summary>Tells, before asking for confirmation, whether a file may be deleted.</summary>
    public ManualDeletionCheck Check(string fullPath, IReadOnlyList<string> exclusions)
    {
        var refusal = GetRefusal(fullPath, new ExclusionList(exclusions));
        return new ManualDeletionCheck(fullPath, refusal is null, refusal?.Message, _policy.IsInApplicationData(fullPath));
    }

    /// <summary>Deletes the files on a background thread.</summary>
    /// <exception cref="InvalidOperationException">Not confirmed, or no deleter registered for the mode.</exception>
    /// <exception cref="ArgumentException"><paramref name="mode"/> is <see cref="CleanupMode.DryRun"/>.</exception>
    public Task<CleanupResult> DeleteAsync(
        IReadOnlyList<string> paths,
        CleanupMode mode,
        bool userConfirmed,
        IReadOnlyList<string> exclusions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (mode == CleanupMode.DryRun)
        {
            throw new ArgumentException("La simulation n'est pas prise en charge pour la suppression manuelle.", nameof(mode));
        }

        if (!userConfirmed)
        {
            throw new InvalidOperationException("La confirmation explicite de l'utilisateur est requise avant toute suppression.");
        }

        if (!_deleters.TryGetValue(mode, out var deleter))
        {
            throw new InvalidOperationException($"Aucun mode de suppression enregistré pour {mode}.");
        }

        return Task.Run(() => Delete(paths, deleter, new ExclusionList(exclusions), cancellationToken), CancellationToken.None);
    }

    private CleanupResult Delete(IReadOnlyList<string> paths, IFileDeleter deleter, ExclusionList exclusions, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var errors = new List<CleanupError>();
        long bytes = 0, deleted = 0;
        var cancelled = false;
        _logger.CleanupStarted(deleter.Mode, paths.Count, Guid.Empty);

        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (ct.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }

            var refusal = GetRefusal(path, exclusions);
            if (refusal is { } r)
            {
                errors.Add(new CleanupError(RuleId, path, r.Kind, r.Message));
                _logger.DeletionFailed(path, r.Kind, r.Message);
                continue;
            }

            long size;
            try
            {
                size = _fileSystem.FileInfo.New(path).Length;
            }
            catch (Exception ex) when (SafeFileEnumerator.IsExpectedIoError(ex))
            {
                var (kind, message) = ErrorClassifier.FromException(ex);
                errors.Add(new CleanupError(RuleId, path, kind, message));
                _logger.DeletionFailed(path, kind, message);
                continue;
            }

            var result = deleter.DeleteFile(path);
            if (result.Success)
            {
                bytes += size;
                deleted++;
                _logger.Deleted(path, size, deleter.Mode);
            }
            else
            {
                var kind = result.ErrorKind ?? CleanupErrorKind.Unknown;
                errors.Add(new CleanupError(RuleId, path, kind, result.Message ?? string.Empty));
                _logger.DeletionFailed(path, kind, result.Message ?? string.Empty);
            }
        }

        _logger.CleanupFinished(deleter.Mode, deleted, bytes, errors.Count, cancelled);
        return new CleanupResult(
            deleter.Mode,
            bytes,
            deleted,
            0,
            [new CategoryCleanupResult(RuleId, "Suppression manuelle", bytes, deleted, errors.Count)],
            errors,
            [],
            cancelled,
            stopwatch.Elapsed);
    }

    private (CleanupErrorKind Kind, string Message)? GetRefusal(string path, ExclusionList exclusions)
    {
        if (_policy.IsProtectedFromManualDeletion(path, out var reason))
        {
            return (CleanupErrorKind.Protected, reason);
        }

        var normalized = PathNormalizer.TryNormalize(path)!;
        if (exclusions.IsExcluded(normalized, out var entry))
        {
            return (CleanupErrorKind.Excluded, $"Exclu par l'utilisateur ({entry}).");
        }

        try
        {
            var file = _fileSystem.FileInfo.New(normalized);
            if (!file.Exists)
            {
                return _fileSystem.Directory.Exists(normalized)
                    ? (CleanupErrorKind.Protected, "Seuls les fichiers peuvent être supprimés ici, pas les dossiers.")
                    : (CleanupErrorKind.Vanished, "Le fichier n'existe plus.");
            }

            if (file.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                return (CleanupErrorKind.ReparsePoint, "Lien ou jonction : non supprimé.");
            }
        }
        catch (Exception ex) when (SafeFileEnumerator.IsExpectedIoError(ex))
        {
            return ErrorClassifier.FromException(ex);
        }

        return null;
    }
}
