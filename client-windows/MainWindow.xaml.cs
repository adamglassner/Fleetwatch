using System.Reflection;
using System.Windows;

namespace Fleetwatch.Client;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var v = version is null ? "0.2.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        VersionText.Text = $"Fleetwatch {v} — early preview";
    }
}
