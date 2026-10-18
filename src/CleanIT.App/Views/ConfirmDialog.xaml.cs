using System.Windows;
using System.Windows.Threading;

namespace CleanIT.App.Views;

/// <summary>
/// Confirmation window for destructive actions. Unlike the standard message box, it has no
/// single-letter access key ("O" for "Oui"), cancels on Enter/Space/Escape, and keeps the
/// confirm button disabled for a short delay so that a stray keystroke or click made while
/// the window appears cannot confirm a deletion.
/// </summary>
public partial class ConfirmDialog : Window
{
    private static readonly TimeSpan ArmingDelay = TimeSpan.FromSeconds(1);

    public ConfirmDialog(string title, string message, string confirmLabel, string cancelLabel, bool isWarning)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        // Plain text content (not an access-key string): underscores are displayed, not used as shortcuts.
        ConfirmButton.Content = new System.Windows.Controls.TextBlock { Text = confirmLabel };
        CancelButton.Content = new System.Windows.Controls.TextBlock { Text = cancelLabel };
        ConfirmButton.Style = (Style)FindResource(isWarning ? "DangerButton" : "PrimaryButton");
        Glyph.Text = isWarning ? "⚠" : "?";
        Glyph.Foreground = (System.Windows.Media.Brush)FindResource(isWarning ? "WarningBrush" : "AccentBrush");

        Loaded += (_, _) =>
        {
            CancelButton.Focus();
            var timer = new DispatcherTimer { Interval = ArmingDelay };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                ConfirmButton.IsEnabled = true;
            };
            timer.Start();
        };
    }

    private void OnConfirm(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
