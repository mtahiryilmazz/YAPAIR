using Microsoft.UI.Xaml;
using Yapair.Admin.Services;

namespace Yapair.Admin;

public partial class App : Application
{
    public static MainWindow? MainWin { get; private set; }

    public App()
    {
        InitializeComponent();
        RequestedTheme = ApplicationTheme.Dark;
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await EmbeddedApiServer.StartAsync();
        MainWin = new MainWindow();
        MainWin.Activate();
    }
}
