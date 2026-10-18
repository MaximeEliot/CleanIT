using System.IO.Abstractions;
using System.Security;

namespace CleanIT.Core.Scanning;

internal enum EntryKind
{
    File,

    /// <summary>Junction or symbolic link: reported, never followed.</summary>
    ReparsePoint,

    /// <summary>Directory or file whose metadata could not be read.</summary>
    Inaccessible,
}

internal readonly record struct EnumeratedEntry(EntryKind Kind, string FullPath, long SizeBytes, DateTime LastWriteUtc, string? Error);

/// <summary>
/// Depth-first file enumeration that never throws for I/O problems and never follows
/// reparse points (junctions, symbolic links, mount points). Following them could lead
/// a temp-folder cleanup into an arbitrary location, e.g. a junction to "Documents".
/// </summary>
internal sealed class SafeFileEnumerator
{
    private readonly IFileSystem _fileSystem;

    public SafeFileEnumerator(IFileSystem fileSystem) => _fileSystem = fileSystem;

    public IEnumerable<EnumeratedEntry> Enumerate(string root, bool recursive, CancellationToken cancellationToken)
    {
        var rootInfo = _fileSystem.DirectoryInfo.New(root);
        if (!rootInfo.Exists)
        {
            yield break;
        }

        if (TryGetAttributes(rootInfo, out var rootAttributes) && rootAttributes.HasFlag(FileAttributes.ReparsePoint))
        {
            yield return new EnumeratedEntry(EntryKind.ReparsePoint, rootInfo.FullName, 0, default, null);
            yield break;
        }

        var pending = new Stack<IDirectoryInfo>();
        pending.Push(rootInfo);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();

            List<IFileSystemInfo>? entries = null;
            string? error = null;
            try
            {
                entries = directory.EnumerateFileSystemInfos().ToList();
            }
            catch (Exception ex) when (IsExpectedIoError(ex))
            {
                error = ex.Message;
            }

            if (entries is null)
            {
                yield return new EnumeratedEntry(EntryKind.Inaccessible, directory.FullName, 0, default, error);
                continue;
            }

            foreach (var entry in entries)
            {
                if (!TryGetAttributes(entry, out var attributes))
                {
                    yield return new EnumeratedEntry(EntryKind.Inaccessible, entry.FullName, 0, default, "Attributs illisibles.");
                    continue;
                }

                if (attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    yield return new EnumeratedEntry(EntryKind.ReparsePoint, entry.FullName, 0, default, null);
                    continue;
                }

                if (entry is IDirectoryInfo subDirectory)
                {
                    if (recursive)
                    {
                        pending.Push(subDirectory);
                    }

                    continue;
                }

                if (entry is IFileInfo file)
                {
                    long size;
                    DateTime lastWrite;
                    try
                    {
                        size = file.Length;
                        lastWrite = file.LastWriteTimeUtc;
                    }
                    catch (Exception ex) when (IsExpectedIoError(ex))
                    {
                        error = ex.Message;
                        size = -1;
                        lastWrite = default;
                    }

                    yield return size < 0
                        ? new EnumeratedEntry(EntryKind.Inaccessible, file.FullName, 0, default, error)
                        : new EnumeratedEntry(EntryKind.File, file.FullName, size, lastWrite, null);
                }
            }
        }
    }

    internal static bool IsExpectedIoError(Exception ex) =>
        ex is UnauthorizedAccessException or IOException or SecurityException;

    private static bool TryGetAttributes(IFileSystemInfo info, out FileAttributes attributes)
    {
        try
        {
            attributes = info.Attributes;
            return true;
        }
        catch (Exception ex) when (IsExpectedIoError(ex))
        {
            attributes = default;
            return false;
        }
    }
}
