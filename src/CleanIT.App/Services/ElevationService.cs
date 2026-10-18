using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using CleanIT.App.Logging;
using CleanIT.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace CleanIT.App.Services;

public interface IElevationService
{
    bool IsElevated { get; }

    /// <summary>
    /// Relaunches CleanIT as administrator (UAC prompt) with the given categories checked,
    /// then closes the current instance. Returns false if the user declined the prompt.
    /// </summary>
    bool RestartElevated(IEnumerable<string> ruleIds);
}

public sealed class ElevationService : IElevationService
{
    private const int ErrorCancelled = 1223;

    private readonly IEnvironmentInfo _environment;
    private readonly ILogger<ElevationService> _logger;

    public ElevationService(IEnvironmentInfo environment, ILogger<ElevationService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public bool IsElevated => _environment.IsElevated;

    public bool RestartElevated(IEnumerable<string> ruleIds)
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrEmpty(executable))
        {
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = true,
                Verb = "runas",
                Arguments = StartupOptions.FormatSelect(ruleIds),
            };
            using (Process.Start(startInfo))
            {
            }

            _logger.ElevatedRestart();
            Application.Current.Shutdown();
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            _logger.ElevationDeclined();
            return false;
        }
    }
}
