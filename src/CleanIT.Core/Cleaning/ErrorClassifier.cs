using System.Security;
using CleanIT.Core.Models;

namespace CleanIT.Core.Cleaning;

/// <summary>Maps I/O failures to <see cref="CleanupErrorKind"/> so they can be reported clearly.</summary>
public static class ErrorClassifier
{
    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;
    private const int ErrorAccessDenied = 5;
    private const int ErrorSharingViolation = 32;
    private const int ErrorLockViolation = 33;
    private const int ErrorFilenameExceedRange = 206;

    // Legacy SHFileOperation "DE_" codes.
    private const int DeAccessDeniedSrc = 0x78;
    private const int DeInvalidFiles = 0x7C;

    public static (CleanupErrorKind Kind, string Message) FromException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var kind = exception switch
        {
            FileNotFoundException or DirectoryNotFoundException => CleanupErrorKind.Vanished,
            UnauthorizedAccessException or SecurityException => CleanupErrorKind.AccessDenied,
            PathTooLongException => CleanupErrorKind.PathTooLong,
            IOException io => FromWin32Code(io.HResult & 0xFFFF),
            _ => CleanupErrorKind.Unknown,
        };

        return (kind, exception.Message);
    }

    public static CleanupErrorKind FromWin32Code(int code) => code switch
    {
        ErrorFileNotFound or ErrorPathNotFound => CleanupErrorKind.Vanished,
        ErrorAccessDenied or DeAccessDeniedSrc => CleanupErrorKind.AccessDenied,
        ErrorSharingViolation or ErrorLockViolation => CleanupErrorKind.Locked,
        ErrorFilenameExceedRange => CleanupErrorKind.PathTooLong,
        DeInvalidFiles => CleanupErrorKind.Unknown,
        _ => CleanupErrorKind.Unknown,
    };

    /// <summary>French label for the summary screen.</summary>
    public static string Describe(CleanupErrorKind kind) => kind switch
    {
        CleanupErrorKind.Locked => "Fichier verrouillé (en cours d'utilisation)",
        CleanupErrorKind.AccessDenied => "Accès refusé",
        CleanupErrorKind.PathTooLong => "Chemin trop long",
        CleanupErrorKind.Protected => "Emplacement protégé",
        CleanupErrorKind.Excluded => "Exclu par l'utilisateur",
        CleanupErrorKind.OutsideRuleRoots => "Hors du périmètre de la catégorie",
        CleanupErrorKind.ReparsePoint => "Lien ou jonction ignoré",
        CleanupErrorKind.Vanished => "Fichier déjà supprimé",
        _ => "Erreur inconnue",
    };
}
