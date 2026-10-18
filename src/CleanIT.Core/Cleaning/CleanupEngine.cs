using System.Diagnostics;
using System.IO.Abstractions;
using CleanIT.Core.Abstractions;
using CleanIT.Core.Logging;
using CleanIT.Core.Models;
using CleanIT.Core.Safety;
using CleanIT.Core.Scanning;
using Microsoft.Extensions.Logging;

namespace CleanIT.Core.Cleaning;

/// <summary>
/// Deletes the items of a <see cref="ScanReport"/>. Every file is re-validated right before
/// deletion (still allowed, still a regular file), errors never stop the run, and cancellation
/// returns a partial result instead of throwing.
/// </summary>
public sealed class CleanupEngine
{
    private const int ProgressInterval = 25;

    private readonly IFileSystem _fileSystem;
    private readonly IEnvironmentInfo _environment;
    private readonly IProtectedPathPolicy _policy;
    private readonly Dictionary<CleanupMode, IFileDeleter> _deleters;
    private readonly Dictionary<string, ICleanerRule> _rules;
    private readonly ILogger<CleanupEngine> _logger;

    public CleanupEngine(
        IFileSystem fileSystem,
        IEnvironmentInfo environment,
        IProtectedPathPolicy policy,
        IEnumerable<IFileDeleter> deleters,
        IEnumerable<ICleanerRule> rules,
        ILogger<CleanupEngine> logger)
    {
        _fileSystem = fileSystem;
        _environment = environment;
        _policy = policy;
        _deleters = deleters.ToDictionary(d => d.Mode);
        _rules = rules.ToDictionary(r => r.Id, StringComparer.Ordinal);
        _logger = logger;
    }

    /// <summary>Runs the cleanup on a background thread.</summary>
    /// <exception cref="InvalidOperationException">
    /// The user did not confirm (non dry-run), or no deleter is registered for the requested mode.
    /// </exception>
    public Task<CleanupResult> CleanAsync(
        ScanReport report,
        CleanupRequest request,
        IProgress<CleanupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Mode != CleanupMode.DryRun && !request.UserConfirmed)
        {
            throw new InvalidOperationException("La confirmation explicite de l'utilisateur est requise avant tout nettoyage.");
        }

        IFileDeleter? deleter = null;
        if (request.Mode != CleanupMode.DryRun && !_deleters.TryGetValue(request.Mode, out deleter))
        {
            throw new InvalidOperationException($"Aucun mode de suppression enregistré pour {request.Mode}.");
        }

        return Task.Run(() => Clean(report, request, deleter, progress, cancellationToken), CancellationToken.None);
    }

    private CleanupResult Clean(
        ScanReport report,
        CleanupRequest request,
        IFileDeleter? deleter,
        IProgress<CleanupProgress>? progress,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var dryRun = request.Mode == CleanupMode.DryRun;
        var guard = new CleanupGuard(_policy, new ExclusionList(request.Exclusions));

        // Custom rules (Recycle Bin) run first: emptying the Recycle Bin after having moved
        // the other categories into it would make those deletions permanent.
        var categories = report.Categories
            .Where(c => request.RuleIds.Contains(c.RuleId) && c.Availability.IsAvailable)
            .OrderBy(c => c.IsCustom ? 0 : 1)
            .ToList();

        _logger.CleanupStarted(request.Mode, categories.Count, report.Id);

        var totalItems = categories.Sum(c => c.IsCustom ? 1 : c.Items.Count);
        var errors = new List<CleanupError>();
        var dryRunEntries = new List<string>();
        var summaries = new List<CategoryCleanupResult>();
        long processed = 0, totalBytes = 0, totalDeleted = 0;
        var directoriesRemoved = 0;
        var cancelled = false;

        foreach (var category in categories)
        {
            if (ct.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }

            var errorsBefore = errors.Count;
            long bytes = 0, deleted = 0;
            progress?.Report(new CleanupProgress(processed, totalItems, totalBytes, category.DisplayName, null));

            if (category.IsCustom)
            {
                (bytes, deleted) = CleanCustom(category, dryRun, errors, dryRunEntries);
                processed++;
            }
            else
            {
                var parents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in category.Items)
                {
                    if (ct.IsCancellationRequested)
                    {
                        cancelled = true;
                        break;
                    }

                    var outcome = CleanItem(category, item, guard, deleter, dryRun, errors);
                    if (outcome.Deleted)
                    {
                        bytes += outcome.Bytes;
                        deleted++;
                        if (dryRun)
                        {
                            dryRunEntries.Add(item.FullPath);
                        }
                        else if (Path.GetDirectoryName(item.FullPath) is { } parent)
                        {
                            parents.Add(parent);
                        }
                    }

                    processed++;
                    if (processed % ProgressInterval == 0)
                    {
                        progress?.Report(new CleanupProgress(processed, totalItems, totalBytes + bytes, category.DisplayName, item.FullPath));
                    }
                }

                if (!dryRun && !cancelled)
                {
                    directoriesRemoved += RemoveEmptyDirectories(parents, category.AllowedRoots, guard);
                }
            }

            totalBytes += bytes;
            totalDeleted += deleted;
            summaries.Add(new CategoryCleanupResult(category.RuleId, category.DisplayName, bytes, deleted, errors.Count - errorsBefore));

            if (cancelled)
            {
                break;
            }
        }

        progress?.Report(new CleanupProgress(processed, totalItems, totalBytes, null, null));
        stopwatch.Stop();
        _logger.CleanupFinished(request.Mode, totalDeleted, totalBytes, errors.Count, cancelled);

        return new CleanupResult(
            request.Mode, totalBytes, totalDeleted, directoriesRemoved, summaries, errors, dryRunEntries, cancelled, stopwatch.Elapsed);
    }

    private (bool Deleted, long Bytes) CleanItem(
        CategoryScanResult category,
        CleanupItem item,
        CleanupGuard guard,
        IFileDeleter? deleter,
        bool dryRun,
        List<CleanupError> errors)
    {
        void Fail(CleanupErrorKind kind, string message)
        {
            errors.Add(new CleanupError(category.RuleId, item.FullPath, kind, message));
            _logger.DeletionFailed(item.FullPath, kind, message);
        }

        // 1. Re-check the safety rules: exclusions may have changed since the scan.
        var verdict = guard.Evaluate(item.FullPath, category.AllowedRoots);
        if (!verdict.IsAllowed)
        {
            Fail(ToErrorKind(verdict.Reason), verdict.Detail);
            return (false, 0);
        }

        // 2. The file must still be a regular file (it could have been replaced by a link).
        long size;
        try
        {
            var file = _fileSystem.FileInfo.New(item.FullPath);
            if (!file.Exists)
            {
                Fail(CleanupErrorKind.Vanished, "Le fichier n'existe plus.");
                return (false, 0);
            }

            if (file.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                Fail(CleanupErrorKind.ReparsePoint, "Le fichier est devenu un lien.");
                return (false, 0);
            }

            size = file.Length;
        }
        catch (Exception ex) when (SafeFileEnumerator.IsExpectedIoError(ex))
        {
            var (kind, message) = ErrorClassifier.FromException(ex);
            Fail(kind, message);
            return (false, 0);
        }

        // 3. Delete (or only record, in dry-run).
        if (dryRun)
        {
            _logger.WouldDelete(item.FullPath, size);
            return (true, size);
        }

        var result = deleter!.DeleteFile(item.FullPath);
        if (!result.Success)
        {
            Fail(result.ErrorKind ?? CleanupErrorKind.Unknown, result.Message ?? string.Empty);
            return (false, 0);
        }

        _logger.Deleted(item.FullPath, size, deleter.Mode);
        return (true, size);
    }

    private (long Bytes, long Items) CleanCustom(CategoryScanResult category, bool dryRun, List<CleanupError> errors, List<string> dryRunEntries)
    {
        if (!_rules.TryGetValue(category.RuleId, out var rule) || rule is not ICustomCleanupRule custom)
        {
            errors.Add(new CleanupError(category.RuleId, category.DisplayName, CleanupErrorKind.Unknown, "Règle introuvable."));
            return (0, 0);
        }

        if (dryRun)
        {
            dryRunEntries.Add($"[{category.DisplayName}] {category.ItemCount} élément(s) seraient supprimés définitivement");
            return (category.TotalBytes, category.ItemCount);
        }

        try
        {
            var outcome = custom.Execute(new RuleContext(_fileSystem, _environment));
            if (!outcome.Success)
            {
                errors.Add(new CleanupError(category.RuleId, category.DisplayName, CleanupErrorKind.Unknown, outcome.ErrorMessage ?? "Échec."));
                _logger.DeletionFailed(category.DisplayName, CleanupErrorKind.Unknown, outcome.ErrorMessage ?? string.Empty);
            }

            return (outcome.BytesFreed, outcome.ItemsDeleted);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            errors.Add(new CleanupError(category.RuleId, category.DisplayName, CleanupErrorKind.Unknown, ex.Message));
            _logger.CustomRuleFailed(ex, category.RuleId);
            return (0, 0);
        }
    }

    /// <summary>
    /// Removes directories left empty by the cleanup, deepest first, never the category roots.
    /// Uses a non-recursive delete, which fails (harmlessly) on any non-empty directory.
    /// </summary>
    private int RemoveEmptyDirectories(HashSet<string> parents, IReadOnlyList<string> roots, CleanupGuard guard)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parent in parents)
        {
            var dir = PathNormalizer.TryNormalize(parent);
            while (dir is not null && roots.Any(r => PathNormalizer.IsStrictlyUnder(dir, r)) && candidates.Add(dir))
            {
                dir = Path.GetDirectoryName(dir);
            }
        }

        var removed = 0;
        foreach (var dir in candidates.OrderByDescending(d => d.Length))
        {
            try
            {
                var info = _fileSystem.DirectoryInfo.New(dir);
                if (!guard.Evaluate(dir, roots).IsAllowed
                    || !info.Exists
                    || info.Attributes.HasFlag(FileAttributes.ReparsePoint)
                    || info.EnumerateFileSystemInfos().Any())
                {
                    continue;
                }

                info.Delete(recursive: false);
                removed++;
            }
            catch (Exception ex) when (SafeFileEnumerator.IsExpectedIoError(ex))
            {
                _logger.DirectoryNotRemoved(dir, ex.Message);
            }
        }

        return removed;
    }

    private static CleanupErrorKind ToErrorKind(SkipReason? reason) => reason switch
    {
        SkipReason.Excluded => CleanupErrorKind.Excluded,
        SkipReason.OutsideRuleRoots => CleanupErrorKind.OutsideRuleRoots,
        SkipReason.ReparsePoint => CleanupErrorKind.ReparsePoint,
        _ => CleanupErrorKind.Protected,
    };
}
