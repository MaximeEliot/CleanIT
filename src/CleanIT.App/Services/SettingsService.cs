using CleanIT.Core.Settings;

namespace CleanIT.App.Services;

/// <summary>Single in-memory copy of the settings shared by the view models, saved on each change.</summary>
public sealed class SettingsService
{
    private readonly SettingsStore _store;

    public SettingsService(SettingsStore store)
    {
        _store = store;
        Current = store.Load();
    }

    public AppSettings Current { get; private set; }

    public string FilePath => _store.FilePath;

    public event EventHandler? Changed;

    public void Update(Action<AppSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var copy = Current.Sanitized();
        change(copy);
        Current = copy.Sanitized();
        _store.Save(Current);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
