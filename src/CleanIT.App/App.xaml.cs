using System.Reflection;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using CleanIT.App.Logging;
using CleanIT.App.Services;
using CleanIT.App.Views;
using CleanIT.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace CleanIT.App;

public partial class App : Application
{
    private ServiceProvider? _services;
    private Microsoft.Extensions.Logging.ILogger? _logger;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(AppPaths.LogDirectory, "cleanit-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                formatProvider: System.Globalization.CultureInfo.InvariantCulture,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        // French number/date formatting in every binding.
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("fr-FR")));

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _logger?.Unhandled(args.Exception);
            args.SetObserved();
        };

        _services = ServiceRegistration.Build(StartupOptions.Parse(e.Args));
        _logger = _services.GetRequiredService<ILogger<App>>();
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";
        _logger.Started(version, _services.GetRequiredService<IEnvironmentInfo>().IsElevated);

        var window = _services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.Unhandled(e.Exception);
        MessageBox.Show(
            "Une erreur inattendue s'est produite. Aucune suppression n'a été effectuée en dehors de celles déjà confirmées.\n\n"
            + e.Exception.Message + "\n\nLe détail est disponible dans le journal : " + AppPaths.LogDirectory,
            "CleanIT",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
