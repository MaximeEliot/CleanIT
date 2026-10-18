using System.IO.Abstractions;
using System.Text.Json;
using CleanIT.Core.Logging;
using Microsoft.Extensions.Logging;

namespace CleanIT.Core.Settings;

/// <summary>Loads and saves <see cref="AppSettings"/>. A missing or corrupted file falls back to defaults.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly IFileSystem _fileSystem;
    private readonly ILogger<SettingsStore> _logger;

    public SettingsStore(IFileSystem fileSystem, string filePath, ILogger<SettingsStore> logger)
    {
        _fileSystem = fileSystem;
        FilePath = filePath;
        _logger = logger;
    }

    public string FilePath { get; }

    public AppSettings Load()
    {
        try
        {
            if (!_fileSystem.File.Exists(FilePath))
            {
                return new AppSettings();
            }

            var json = _fileSystem.File.ReadAllText(FilePath);
            return (JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings()).Sanitized();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.SettingsUnreadable(ex, FilePath);
            return new AppSettings();
        }
    }

    /// <summary>Saves atomically (temporary file then replace). Returns false on failure.</summary>
    public bool Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        try
        {
            if (_fileSystem.Path.GetDirectoryName(FilePath) is { Length: > 0 } directory)
            {
                _fileSystem.Directory.CreateDirectory(directory);
            }

            var temp = FilePath + ".tmp";
            _fileSystem.File.WriteAllText(temp, JsonSerializer.Serialize(settings.Sanitized(), JsonOptions));
            _fileSystem.File.Move(temp, FilePath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.SettingsNotSaved(ex, FilePath);
            return false;
        }
    }
}
