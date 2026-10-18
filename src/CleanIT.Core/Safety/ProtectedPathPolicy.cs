using System.Text.RegularExpressions;
using CleanIT.Core.Abstractions;

namespace CleanIT.Core.Safety;

/// <summary>
/// Hard-coded blacklist of locations CleanIT must never delete. It is the last line of
/// defense: it is evaluated for every file at scan time and again right before deletion,
/// independently of the rules and of the user settings.
/// </summary>
/// <remarks>
/// Three levels, evaluated in this order:
/// <list type="number">
/// <item><b>Always protected</b> subtrees and file names (System32, Program Files, Documents,
/// browser cookies/passwords/history...). Nothing can override them.</item>
/// <item><b>Deny-by-default</b> subtrees (Windows, ProgramData, Users, the profile, LocalAppData):
/// everything inside is protected, except the explicit hard-coded <b>carve-outs</b>
/// (Windows\Temp, browser cache folders...). A carve-out only allows what is strictly
/// below it, never the carve-out folder itself.</item>
/// <item>Anything else (e.g. "D:\Games") is not protected by this policy; rules can still
/// only clean their own declared roots.</item>
/// </list>
/// Any change to these lists must come with a test in ProtectedPathPolicyTests.
/// </remarks>
public sealed partial class ProtectedPathPolicy : IProtectedPathPolicy
{
    /// <summary>Folders directly under any drive root that are always protected.</summary>
    private static readonly string[] AlwaysProtectedDriveFolders =
    [
        "Program Files",
        "Program Files (x86)",
        "System Volume Information",
        "$Recycle.Bin", // emptied through the Shell API only, never file by file
        "Recovery",
        "Boot",
        "EFI",
        "Config.Msi",
        @"Windows\System32",
        @"Windows\SysWOW64",
        @"Windows\SysArm32",
        @"Windows\WinSxS",
        @"Windows\Boot",
        @"Windows\Fonts",
        @"Windows\Installer",
        @"Windows\servicing",
        @"Windows\assembly",
        @"Windows\Microsoft.NET",
        @"Windows\SystemApps",
        @"Windows\SystemResources",
        @"Windows\INF",
        @"Windows\Panther",
    ];

    /// <summary>Folders directly under any drive root where everything is denied unless carved out.</summary>
    private static readonly string[] DenyByDefaultDriveFolders = ["Windows", "ProgramData", "Users"];

    /// <summary>User profile sub-folders holding personal data: always protected.</summary>
    private static readonly string[] ProtectedProfileFolders =
    [
        "Documents", "Desktop", "Pictures", "Music", "Videos", "Downloads", "Favorites",
        "Contacts", "Links", "Searches", "Saved Games", "OneDrive", @"AppData\Roaming", @"AppData\LocalLow",
    ];

    /// <summary>
    /// File names that are never deleted, wherever they are: browser secrets/history and
    /// Windows system files.
    /// </summary>
    private static readonly HashSet<string> ProtectedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Chromium (Chrome, Edge)
        "Cookies", "Cookies-journal", "Login Data", "Login Data-journal", "Login Data For Account",
        "Login Data For Account-journal", "Web Data", "Web Data-journal", "History", "History-journal",
        "Bookmarks", "Bookmarks.bak", "Preferences", "Secure Preferences", "Local State", "Top Sites",
        "Favicons", "Shortcuts", "Visited Links",

        // Firefox
        "places.sqlite", "cookies.sqlite", "logins.json", "logins-backup.json", "key3.db", "key4.db",
        "cert9.db", "formhistory.sqlite", "favicons.sqlite", "sessionstore.jsonlz4", "permissions.sqlite",

        // Windows
        "pagefile.sys", "hiberfil.sys", "swapfile.sys", "bootmgr", "BOOTNXT", "ntuser.dat", "ntuser.dat.LOG1",
        "ntuser.dat.LOG2", "ntuser.ini", "UsrClass.dat", "desktop.ini",
    };

    /// <summary>Directory names holding browser data other than cache: never entered.</summary>
    private static readonly HashSet<string> ProtectedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Local Storage", "Session Storage", "IndexedDB", "Extensions", "Network", "Sync Data",
        "databases", "storage",
    };

    private readonly List<string> _alwaysProtected = [];
    private readonly List<string> _denyByDefault = [];
    private readonly List<string[]> _carveOuts = [];
    private readonly List<string> _carveOutAnchors = [];

    public ProtectedPathPolicy(IEnvironmentInfo environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        var systemDrive = PathNormalizer.TryNormalize(environment.SystemDrive) ?? @"C:\";
        var windows = Normalize(environment.WindowsDirectory) ?? Path.Combine(systemDrive, "Windows");
        var programData = Normalize(environment.ProgramData) ?? Path.Combine(systemDrive, "ProgramData");
        var profile = Normalize(environment.UserProfile);
        var localAppData = Normalize(environment.LocalAppData);

        // --- Always protected -------------------------------------------------------------
        foreach (var folder in AlwaysProtectedDriveFolders)
        {
            Add(_alwaysProtected, Path.Combine(systemDrive, folder));
        }

        Add(_alwaysProtected, environment.ProgramFiles);
        Add(_alwaysProtected, environment.ProgramFilesX86);
        Add(_alwaysProtected, environment.RoamingAppData);
        foreach (var sub in new[] { "System32", "SysWOW64", "WinSxS", "Fonts", "Installer", "servicing" })
        {
            Add(_alwaysProtected, Path.Combine(windows, sub));
        }

        if (profile is not null)
        {
            foreach (var folder in ProtectedProfileFolders)
            {
                Add(_alwaysProtected, Path.Combine(profile, folder));
            }
        }

        // --- Deny by default ----------------------------------------------------------------
        foreach (var folder in DenyByDefaultDriveFolders)
        {
            Add(_denyByDefault, Path.Combine(systemDrive, folder));
        }

        Add(_denyByDefault, windows);
        Add(_denyByDefault, programData);
        Add(_denyByDefault, profile);
        Add(_denyByDefault, localAppData);
        if (profile is not null && Path.GetDirectoryName(profile) is { } usersRoot)
        {
            Add(_denyByDefault, usersRoot);
        }

        // --- Carve-outs: the only places inside deny-by-default trees that may be cleaned ---
        foreach (var win in new[] { windows, Path.Combine(systemDrive, "Windows") })
        {
            AddCarveOut(win, "Temp");
            AddCarveOut(win, @"SoftwareDistribution\Download");
        }

        foreach (var sub in new[] { "ReportArchive", "ReportQueue", "Temp" })
        {
            AddCarveOut(programData, $@"Microsoft\Windows\WER\{sub}");
        }

        if (localAppData is not null)
        {
            AddCarveOut(localAppData, @"Microsoft\Windows\Explorer");
            foreach (var sub in new[] { "ReportArchive", "ReportQueue", "Temp" })
            {
                AddCarveOut(localAppData, $@"Microsoft\Windows\WER\{sub}");
            }

            foreach (var userData in new[] { @"Google\Chrome\User Data", @"Microsoft\Edge\User Data" })
            {
                foreach (var cache in new[] { "Cache", "Code Cache", "GPUCache" })
                {
                    AddCarveOut(localAppData, $@"{userData}\*\{cache}");
                }

                AddCarveOut(localAppData, $@"{userData}\ShaderCache");
                AddCarveOut(localAppData, $@"{userData}\GrShaderCache");
            }

            AddCarveOut(localAppData, @"Mozilla\Firefox\Profiles\*\cache2");
        }

        // %TEMP% is user-controlled: only trusted when it cannot cover critical folders.
        var temp = Normalize(environment.UserTemp);
        if (temp is not null && IsSafeCleanupRoot(temp))
        {
            _carveOuts.Add(PathNormalizer.Segments(temp));
            _carveOutAnchors.Add(temp);
        }
    }

    public bool IsProtected(string fullPath, out string reason)
    {
        var path = PathNormalizer.TryNormalize(fullPath);
        if (path is null)
        {
            reason = "Chemin invalide, relatif ou réseau.";
            return true;
        }

        if (PathNormalizer.IsDriveRoot(path))
        {
            reason = "Racine de lecteur.";
            return true;
        }

        var segments = PathNormalizer.Segments(path);
        for (var i = 1; i < segments.Length; i++)
        {
            if (ShortNameRegex().IsMatch(segments[i]))
            {
                reason = "Nom court 8.3 non résolu.";
                return true;
            }

            if (i < segments.Length - 1 && ProtectedDirectoryNames.Contains(segments[i]))
            {
                reason = $"Dossier de données protégé « {segments[i]} ».";
                return true;
            }
        }

        if (ProtectedFileNames.Contains(segments[^1]) || ProtectedDirectoryNames.Contains(segments[^1]))
        {
            reason = $"Fichier protégé « {segments[^1]} ».";
            return true;
        }

        foreach (var root in _alwaysProtected.Concat(OnDrive(segments[0], AlwaysProtectedDriveFolders)))
        {
            if (PathNormalizer.IsSameOrUnder(path, root))
            {
                reason = $"Dossier système ou personnel protégé ({root}).";
                return true;
            }
        }

        foreach (var anchor in _carveOutAnchors)
        {
            if (PathNormalizer.AreSame(path, anchor))
            {
                reason = "Dossier racine d'une catégorie (seul son contenu peut être nettoyé).";
                return true;
            }
        }

        foreach (var root in _denyByDefault.Concat(OnDrive(segments[0], DenyByDefaultDriveFolders)))
        {
            if (PathNormalizer.IsSameOrUnder(path, root))
            {
                if (IsInCarveOut(segments))
                {
                    break;
                }

                reason = $"Dossier système ou de profil protégé ({root}).";
                return true;
            }
        }

        reason = string.Empty;
        return false;
    }

    /// <summary>
    /// A cleanup root is safe when it is not protected itself and does not contain
    /// any critical folder (a drive root, "C:\Users", the profile, ...).
    /// The engines refuse any rule root that fails this check.
    /// </summary>
    public bool IsSafeCleanupRoot(string root)
    {
        var path = PathNormalizer.TryNormalize(root);
        if (path is null || PathNormalizer.IsDriveRoot(path))
        {
            return false;
        }

        var drive = PathNormalizer.Segments(path)[0];
        var alwaysProtected = _alwaysProtected.Concat(OnDrive(drive, AlwaysProtectedDriveFolders)).ToList();
        var denyByDefault = _denyByDefault.Concat(OnDrive(drive, DenyByDefaultDriveFolders));

        foreach (var critical in alwaysProtected.Concat(denyByDefault))
        {
            // The root must neither be inside an always-protected tree nor contain a critical folder.
            if (PathNormalizer.IsSameOrUnder(critical, path))
            {
                return false;
            }
        }

        foreach (var critical in alwaysProtected)
        {
            if (PathNormalizer.IsSameOrUnder(path, critical))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Applies drive-relative folder names to the drive of the evaluated path (e.g. "D:\Program Files").</summary>
    private static IEnumerable<string> OnDrive(string drive, string[] relativeFolders) =>
        relativeFolders.Select(folder => drive + "\\" + folder);

    private bool IsInCarveOut(string[] pathSegments)
    {
        foreach (var pattern in _carveOuts)
        {
            if (pathSegments.Length <= pattern.Length)
            {
                continue;
            }

            var match = true;
            for (var i = 0; i < pattern.Length && match; i++)
            {
                match = pattern[i] == "*" || string.Equals(pattern[i], pathSegments[i], StringComparison.OrdinalIgnoreCase);
            }

            if (match)
            {
                return true;
            }
        }

        return false;
    }

    private void AddCarveOut(string? baseDir, string relative)
    {
        if (baseDir is null)
        {
            return;
        }

        var segments = PathNormalizer.Segments(baseDir).Concat(relative.Split('\\')).ToArray();
        _carveOuts.Add(segments);
        if (!relative.Contains('*', StringComparison.Ordinal))
        {
            _carveOutAnchors.Add(string.Join('\\', segments));
        }
    }

    private static void Add(List<string> list, string? path)
    {
        var normalized = Normalize(path);
        if (normalized is not null && !list.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            list.Add(normalized);
        }
    }

    private static string? Normalize(string? path) => PathNormalizer.TryNormalize(path);

    /// <summary>MS-DOS 8.3 alias such as "PROGRA~1": GetFullPath does not expand them, so they could hide a protected folder.</summary>
    [GeneratedRegex(@"^[^~]{1,6}~\d+(\.[^.]{0,3})?$", RegexOptions.CultureInvariant)]
    private static partial Regex ShortNameRegex();
}
