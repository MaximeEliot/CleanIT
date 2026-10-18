namespace CleanIT.App;

/// <summary>Where CleanIT stores its own files.</summary>
public static class AppPaths
{
    public static string LogDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CleanIT", "logs");

    public static string SettingsFile { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CleanIT", "settings.json");
}
