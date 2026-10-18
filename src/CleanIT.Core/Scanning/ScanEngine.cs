using System.IO.Abstractions;
using System.IO.Enumeration;
using CleanIT.Core.Abstractions;
using CleanIT.Core.Logging;
using CleanIT.Core.Models;
using CleanIT.Core.Safety;
using Microsoft.Extensions.Logging;

namespace CleanIT.Core.Scanning;

/// <summary>
/// Runs the analysis: asks each rule for its targets, enumerates them safely and filters every
/// file through the <see cref="CleanupGuard"/>. Never modifies the file system.
/// </summary>
public sealed class ScanEngine
{
    private const int ProgressInterval = 250;

    private readonly IFileSystem _fileSystem;
    private readonly IEnvironmentInfo _environment;
    private readonly IProtectedPathPolicy _policy;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ScanEngine> _logger;
    private readonly SafeFileEnumerator _enumerator;

    public ScanEngine(
        IFileSystem fileSystem,
        IEnvironmentInfo environment,
        IProtectedPathPolicy policy,
        TimeProvider timeProvider,
        ILogger<ScanEngine> logger)
    {
        _fileSystem = fileSystem;
        _environment = environment;
        _policy = policy;
        _timeProvider = timeProvider;
        _logger = logger;
        _enumerator = new SafeFileEnumerator(fileSystem);
    }

    /// <summary>
    /// Scans the given rules on a background thread.
    /// </summary>
    /// <exception cref="OperationCanceledException">The scan was cancelled; no partial report is produced.</exception>
    public Task<ScanReport> ScanAsync(
        IReadOnlyList<ICleanerRule> rules,
        ScanOptions options,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(options);

        return Task.Run(() => Scan(rules, options, progress, cancellationToken), cancellationToken);
    }

    private ScanReport Scan(IReadOnlyList<ICleanerRule> rules, ScanOptions options, IProgress<ScanProgress>? progress, CancellationToken ct)
    {
        _logger.ScanStarted(rules.Count);
        var guard = new CleanupGuard(_policy, new ExclusionList(options.Exclusions));
        var results = new List<CategoryScanResult>(rules.Count);

        for (var i = 0; i < rules.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var rule = rules[i];
            progress?.Report(new ScanProgress(rule.Id, rule.DisplayName, i, rules.Count, 0, 0));

            CategoryScanResult result;
            try
            {
                result = ScanRule(rule, guard, options, i, rules.Count, progress, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A faulty rule must not abort the whole analysis.
                _logger.RuleScanFailed(ex, rule.Id);
                result = Empty(rule, RuleAvailability.Blocked("Erreur pendant l'analyse : " + ex.Message));
            }

            _logger.RuleScanned(rule.Id, result.ItemCount, result.TotalBytes, result.SkippedCount, result.Availability.Status);

            results.Add(result);
        }

        var report = new ScanReport(_timeProvider.GetUtcNow(), results);
        _logger.ScanFinished(report.TotalBytes);
        return report;
    }

    private CategoryScanResult ScanRule(
        ICleanerRule rule,
        CleanupGuard guard,
        ScanOptions options,
        int ruleIndex,
        int ruleCount,
        IProgress<ScanProgress>? progress,
        CancellationToken ct)
    {
        var context = new RuleContext(_fileSystem, _environment);

        var availability = rule.CheckAvailability(context);
        if (availability.IsAvailable && rule.RequiresElevation && !_environment.IsElevated)
        {
            availability = RuleAvailability.NeedsElevation();
        }

        if (!availability.IsAvailable)
        {
            return Empty(rule, availability);
        }

        if (rule is ICustomCleanupRule custom)
        {
            var summary = custom.Measure(context);
            return new CategoryScanResult(rule.Id, rule.DisplayName, availability, [], [], [], 0, summary);
        }

        var skipped = new SkippedCollector(options.MaxSkippedEntriesPerCategory);
        var targets = new List<(ScanTarget Target, string Root)>();
        foreach (var target in rule.GetTargets(context))
        {
            var root = PathNormalizer.TryNormalize(target.Root);
            if (root is null || !_policy.IsSafeCleanupRoot(root))
            {
                // Defense in depth: a rule pointing at "C:\Users" or a drive root is ignored.
                _logger.UnsafeRuleRoot(rule.Id, target.Root);
                skipped.Add(new SkippedEntry(target.Root, SkipReason.Protected, "Racine refusée par la politique de sécurité."));
                continue;
            }

            targets.Add((target, root));
        }

        var allowedRoots = targets.Select(t => t.Root).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var items = new List<CleanupItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        long bytes = 0;

        foreach (var (target, root) in targets)
        {
            foreach (var entry in _enumerator.Enumerate(root, target.Recursive, ct))
            {
                switch (entry.Kind)
                {
                    case EntryKind.ReparsePoint:
                        skipped.Add(new SkippedEntry(entry.FullPath, SkipReason.ReparsePoint, "Lien ou jonction : jamais suivi."));
                        continue;
                    case EntryKind.Inaccessible:
                        _logger.Inaccessible(entry.FullPath, entry.Error);
                        skipped.Add(new SkippedEntry(entry.FullPath, SkipReason.Inaccessible, entry.Error ?? "Accès impossible."));
                        continue;
                }

                if (!FileSystemName.MatchesSimpleExpression(target.SearchPattern, Path.GetFileName(entry.FullPath), ignoreCase: true))
                {
                    continue;
                }

                if (target.ApplyMinimumAge && now - entry.LastWriteUtc < options.MinimumFileAge)
                {
                    skipped.Add(new SkippedEntry(entry.FullPath, SkipReason.TooRecent, "Modifié récemment."));
                    continue;
                }

                var verdict = guard.Evaluate(entry.FullPath, allowedRoots);
                if (!verdict.IsAllowed)
                {
                    skipped.Add(new SkippedEntry(entry.FullPath, verdict.Reason!.Value, verdict.Detail));
                    continue;
                }

                if (!seen.Add(entry.FullPath))
                {
                    continue;
                }

                items.Add(new CleanupItem(entry.FullPath, entry.SizeBytes, entry.LastWriteUtc));
                bytes += entry.SizeBytes;
                if (items.Count % ProgressInterval == 0)
                {
                    progress?.Report(new ScanProgress(rule.Id, rule.DisplayName, ruleIndex, ruleCount, items.Count, bytes));
                }
            }
        }

        progress?.Report(new ScanProgress(rule.Id, rule.DisplayName, ruleIndex, ruleCount, items.Count, bytes));
        return new CategoryScanResult(rule.Id, rule.DisplayName, availability, allowedRoots, items, skipped.Entries, skipped.Count);
    }

    private static CategoryScanResult Empty(ICleanerRule rule, RuleAvailability availability) =>
        new(rule.Id, rule.DisplayName, availability, [], [], [], 0);

    /// <summary>Counts every skipped entry but keeps only a bounded sample.</summary>
    private sealed class SkippedCollector(int capacity)
    {
        private readonly List<SkippedEntry> _entries = [];

        public IReadOnlyList<SkippedEntry> Entries => _entries;

        public int Count { get; private set; }

        public void Add(SkippedEntry entry)
        {
            Count++;
            if (_entries.Count < capacity)
            {
                _entries.Add(entry);
            }
        }
    }
}
