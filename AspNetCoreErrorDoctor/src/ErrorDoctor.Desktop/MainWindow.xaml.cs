using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using ErrorDoctor.Desktop.Infrastructure;
using ErrorDoctor.Desktop.ViewModels;

namespace ErrorDoctor.Desktop;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        var config = AppConfig.Load();
        _viewModel = new MainViewModel(config);
        DataContext = _viewModel;

        Loaded += async (_, _) => await _viewModel.InitializeAsync();
    }

    private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
