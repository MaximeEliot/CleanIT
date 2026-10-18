using CleanIT.Core.Abstractions;
using CleanIT.Core.Cleaning;
using CleanIT.Core.Models;

namespace CleanIT.Core.Platform.Windows;

/// <summary>
/// Default deleter: moves files to the Recycle Bin through the Shell, without any UI,
/// so the user can restore them.
/// </summary>
public sealed class RecycleBinFileDeleter : IFileDeleter
{
    public CleanupMode Mode => CleanupMode.RecycleBin;

    public DeletionResult DeleteFile(string fullPath)
    {
        ArgumentNullException.ThrowIfNull(fullPath);

        if (fullPath.Length >= NativeMethods.MaxPath)
        {
            // The Shell API cannot recycle long paths; we skip rather than silently deleting permanently.
            return DeletionResult.Fail(
                CleanupErrorKind.PathTooLong,
                "Chemin trop long pour la corbeille (activez la suppression définitive pour le traiter).");
        }

        if (!File.Exists(fullPath))
        {
            return DeletionResult.Fail(CleanupErrorKind.Vanished, "Le fichier n'existe plus.");
        }

        var operation = new NativeMethods.ShFileOpStruct
        {
            WFunc = NativeMethods.FoDelete,
            PFrom = fullPath + "\0\0",
            FFlags = (ushort)(NativeMethods.FofAllowUndo | NativeMethods.FofNoConfirmation | NativeMethods.FofSilent
                | NativeMethods.FofNoErrorUi | NativeMethods.FofNoConfirmMkDir | NativeMethods.FofWantNukeWarning),
        };

        var code = NativeMethods.SHFileOperation(ref operation);
        if (code == 0 && !operation.FAnyOperationsAborted)
        {
            return DeletionResult.Ok;
        }

        if (code == 0)
        {
            return DeletionResult.Fail(CleanupErrorKind.Unknown, "Opération annulée par le système.");
        }

        var kind = ErrorClassifier.FromWin32Code(code);
        return DeletionResult.Fail(kind, $"{ErrorClassifier.Describe(kind)} (code 0x{code:X}).");
    }
}
