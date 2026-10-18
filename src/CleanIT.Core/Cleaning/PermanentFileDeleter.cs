using System.IO.Abstractions;
using CleanIT.Core.Abstractions;
using CleanIT.Core.Models;
using CleanIT.Core.Scanning;

namespace CleanIT.Core.Cleaning;

/// <summary>Deletes files permanently (no Recycle Bin). Only used when the user enabled it.</summary>
public sealed class PermanentFileDeleter : IFileDeleter
{
    private readonly IFileSystem _fileSystem;

    public PermanentFileDeleter(IFileSystem fileSystem) => _fileSystem = fileSystem;

    public CleanupMode Mode => CleanupMode.Permanent;

    public DeletionResult DeleteFile(string fullPath)
    {
        try
        {
            var file = _fileSystem.FileInfo.New(fullPath);
            if (!file.Exists)
            {
                return DeletionResult.Fail(CleanupErrorKind.Vanished, "Le fichier n'existe plus.");
            }

            // Read-only temp files are common (installers); the flag alone must not block the cleanup.
            if (file.Attributes.HasFlag(FileAttributes.ReadOnly))
            {
                file.Attributes &= ~FileAttributes.ReadOnly;
            }

            file.Delete();
            return DeletionResult.Ok;
        }
        catch (Exception ex) when (SafeFileEnumerator.IsExpectedIoError(ex))
        {
            var (kind, message) = ErrorClassifier.FromException(ex);
            return DeletionResult.Fail(kind, message);
        }
    }
}
