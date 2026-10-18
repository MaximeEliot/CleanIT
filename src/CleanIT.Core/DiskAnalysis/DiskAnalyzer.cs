using System.Diagnostics;
using System.IO.Abstractions;
using CleanIT.Core.Logging;
using CleanIT.Core.Scanning;
using Microsoft.Extensions.Logging;

namespace CleanIT.Core.DiskAnalysis;

/// <summary>
/// Read-only disk usage analysis: builds the directory tree sorted by size and keeps the
/// largest files. Junctions are not followed (they would count the same data twice, or loop).
/// </summary>
public sealed class DiskAnalyzer
{
    public const int DefaultLargestFileCount = 50;
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(150);

    private readonly IFileSystem _fileSystem;
    private readonly ILogger<DiskAnalyzer> _logger;

    public DiskAnalyzer(IFileSystem fileSystem, ILogger<DiskAnalyzer> logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    /// <exception cref="DirectoryNotFoundException"><paramref name="rootPath"/> does not exist.</exception>
    /// <exception cref="OperationCanceledException">The analysis was cancelled.</exception>
    public Task<DiskAnalysisResult> AnalyzeAsync(
        string rootPath,
        int largestFileCount = DefaultLargestFileCount,
        IProgress<DiskAnalysisProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentOutOfRangeException.ThrowIfNegative(largestFileCount);

        return Task.Run(() => Analyze(rootPath, largestFileCount, progress, cancellationToken), cancellationToken);
    }

    private DiskAnalysisResult Analyze(string rootPath, int largestFileCount, IProgress<DiskAnalysisProgress>? progress, CancellationToken ct)
    {
        var rootInfo = _fileSystem.DirectoryInfo.New(rootPath);
        if (!rootInfo.Exists)
        {
            throw new DirectoryNotFoundException($"Le dossier « {rootPath} » n'existe pas.");
        }

        _logger.DiskAnalysisStarted(rootInfo.FullName);
        var state = new AnalysisState(largestFileCount, progress, Stopwatch.StartNew());
        var root = new DirectoryNode(string.IsNullOrEmpty(rootInfo.Name) ? rootInfo.FullName : rootInfo.Name, rootInfo.FullName);
        Visit(rootInfo, root, state, ct);

        var largest = state.Largest.UnorderedItems
            .Select(x => x.Element)
            .OrderByDescending(f => f.SizeBytes)
            .ToList();

        _logger.DiskAnalysisFinished(rootInfo.FullName, root.SizeBytes, state.Files, state.Inaccessible);
        progress?.Report(new DiskAnalysisProgress(state.Files, state.Bytes, rootInfo.FullName));
        return new DiskAnalysisResult(root, largest, state.Files, state.Inaccessible, state.Clock.Elapsed);
    }

    private void Visit(IDirectoryInfo directory, DirectoryNode node, AnalysisState state, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        List<IFileSystemInfo> entries;
        try
        {
            entries = directory.EnumerateFileSystemInfos().ToList();
        }
        catch (Exception ex) when (SafeFileEnumerator.IsExpectedIoError(ex))
        {
            node.IsAccessible = false;
            state.Inaccessible++;
            _logger.Inaccessible(directory.FullName, ex.Message);
            return;
        }

        foreach (var entry in entries)
        {
            FileAttributes attributes;
            try
            {
                attributes = entry.Attributes;
            }
            catch (Exception ex) when (SafeFileEnumerator.IsExpectedIoError(ex))
            {
                continue;
            }

            if (entry is IDirectoryInfo subDirectory)
            {
                var child = new DirectoryNode(subDirectory.Name, subDirectory.FullName);
                if (attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    child.IsReparsePoint = true;
                }
                else
                {
                    Visit(subDirectory, child, state, ct);
                }

                node.AddChild(child);
                node.SizeBytes += child.SizeBytes;
                node.FileCount += child.FileCount;
            }
            else if (entry is IFileInfo file && !attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                long size;
                try
                {
                    size = file.Length;
                }
                catch (Exception ex) when (SafeFileEnumerator.IsExpectedIoError(ex))
                {
                    continue;
                }

                node.SizeBytes += size;
                node.FileCount++;
                state.AddFile(file, size);
            }
        }

        node.SortChildren();
        state.ReportProgress(directory.FullName);
    }

    private sealed class AnalysisState(int capacity, IProgress<DiskAnalysisProgress>? progress, Stopwatch clock)
    {
        private TimeSpan _lastReport;

        /// <summary>Min-heap on size: the smallest of the current top N is evicted first.</summary>
        public PriorityQueue<FileEntry, long> Largest { get; } = new();

        public Stopwatch Clock { get; } = clock;

        public long Files { get; private set; }

        public long Bytes { get; private set; }

        public int Inaccessible { get; set; }

        public void AddFile(IFileInfo file, long size)
        {
            Files++;
            Bytes += size;
            if (capacity == 0)
            {
                return;
            }

            if (Largest.Count < capacity)
            {
                Largest.Enqueue(new FileEntry(file.FullName, size, SafeLastWrite(file)), size);
            }
            else if (Largest.TryPeek(out _, out var smallest) && size > smallest)
            {
                Largest.EnqueueDequeue(new FileEntry(file.FullName, size, SafeLastWrite(file)), size);
            }
        }

        public void ReportProgress(string currentDirectory)
        {
            if (progress is not null && Clock.Elapsed - _lastReport >= ProgressInterval)
            {
                _lastReport = Clock.Elapsed;
                progress.Report(new DiskAnalysisProgress(Files, Bytes, currentDirectory));
            }
        }

        private static DateTime SafeLastWrite(IFileInfo file)
        {
            try
            {
                return file.LastWriteTimeUtc;
            }
            catch (Exception ex) when (SafeFileEnumerator.IsExpectedIoError(ex))
            {
                return default;
            }
        }
    }
}
