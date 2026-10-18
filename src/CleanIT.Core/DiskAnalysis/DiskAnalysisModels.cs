namespace CleanIT.Core.DiskAnalysis;

/// <summary>A directory with the cumulated size of everything below it.</summary>
public sealed class DirectoryNode
{
    private readonly List<DirectoryNode> _children = [];

    internal DirectoryNode(string name, string fullPath)
    {
        Name = name;
        FullPath = fullPath;
    }

    public string Name { get; }

    public string FullPath { get; }

    /// <summary>Total size of the files in this directory and all its sub-directories.</summary>
    public long SizeBytes { get; internal set; }

    /// <summary>Total number of files below this directory.</summary>
    public long FileCount { get; internal set; }

    /// <summary>False when the directory content could not be listed (size is then underestimated).</summary>
    public bool IsAccessible { get; internal set; } = true;

    /// <summary>True for junctions/symbolic links, which are shown but never followed.</summary>
    public bool IsReparsePoint { get; internal set; }

    /// <summary>Sub-directories, largest first.</summary>
    public IReadOnlyList<DirectoryNode> Children => _children;

    internal void AddChild(DirectoryNode child) => _children.Add(child);

    internal void SortChildren() => _children.Sort((a, b) => b.SizeBytes.CompareTo(a.SizeBytes));
}

/// <summary>A file of the "largest files" list.</summary>
public sealed record FileEntry(string FullPath, long SizeBytes, DateTime LastWriteUtc);

public sealed record DiskAnalysisProgress(long FilesScanned, long BytesScanned, string CurrentDirectory);

public sealed record DiskAnalysisResult(
    DirectoryNode Root,
    IReadOnlyList<FileEntry> LargestFiles,
    long TotalFiles,
    int InaccessibleDirectories,
    TimeSpan Duration);
