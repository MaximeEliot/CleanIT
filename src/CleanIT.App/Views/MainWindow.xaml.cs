using System.ComponentModel;
using System.Windows;
using CleanIT.App.Services;
using CleanIT.App.ViewModels;

namespace CleanIT.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IDialogService _dialogs;

    public MainWindow(MainViewModel viewModel, IDialogService dialogs)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _dialogs = dialogs;
        DataContext = viewModel;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (_viewModel.IsBusy)
        {
            if (!_dialogs.Confirm("Quitter CleanIT", "Une opération est en cours. Voulez-vous l'annuler et quitter ?", isWarning: true, confirmLabel: "Quitter", cancelLabel: "Rester"))
            {
                e.Cancel = true;
                return;
            }

            _viewModel.CancelAll();
        }

        base.OnClosing(e);
    }
}
