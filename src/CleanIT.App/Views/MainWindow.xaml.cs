using System.Windows;
using CleanIT.App.ViewModels;

namespace CleanIT.App.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
