using System.IO.Abstractions;
using CleanIT.App.Services;
using CleanIT.App.ViewModels;
using CleanIT.App.Views;
using CleanIT.Core.Abstractions;
using CleanIT.Core.Cleaning;
using CleanIT.Core.DiskAnalysis;
using CleanIT.Core.Platform.Windows;
using CleanIT.Core.Rules;
using CleanIT.Core.Safety;
using CleanIT.Core.Scanning;
using CleanIT.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace CleanIT.App;

/// <summary>Composition root.</summary>
internal static class ServiceRegistration
{
    public static ServiceProvider Build(StartupOptions options)
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.ClearProviders().AddSerilog(dispose: false));
        services.AddSingleton(options);

        // Core
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IEnvironmentInfo, WindowsEnvironmentInfo>();
        services.AddSingleton<IProtectedPathPolicy, ProtectedPathPolicy>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IRecycleBin, WindowsRecycleBin>();
        services.AddSingleton<IReadOnlyList<ICleanerRule>>(sp => DefaultRules.Create(sp.GetRequiredService<IRecycleBin>()));
        services.AddSingleton<IEnumerable<ICleanerRule>>(sp => sp.GetRequiredService<IReadOnlyList<ICleanerRule>>());
        services.AddSingleton<IFileDeleter, RecycleBinFileDeleter>();
        services.AddSingleton<IFileDeleter, PermanentFileDeleter>();
        services.AddSingleton<ScanEngine>();
        services.AddSingleton<CleanupEngine>();
        services.AddSingleton<DiskAnalyzer>();
        services.AddSingleton(sp => new SettingsStore(
            sp.GetRequiredService<IFileSystem>(), AppPaths.SettingsFile, sp.GetRequiredService<ILogger<SettingsStore>>()));

        // App
        services.AddSingleton<SettingsService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IElevationService, ElevationService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
}
