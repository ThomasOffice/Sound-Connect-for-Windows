using Microsoft.UI.Xaml;

namespace Xm6Control.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, args) => Log(args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log(args.ExceptionObject as Exception);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _window = new MainWindow();
            _window.Activate();
        }
        catch (Exception ex)
        {
            Log(ex);
            throw;
        }
    }

    private static void Log(Exception? ex)
    {
        try
        {
            File.WriteAllText(
                Path.Combine(Path.GetTempPath(), "xm6app-error.txt"),
                ex?.ToString() ?? "unknown");
        }
        catch
        {
            // Best effort.
        }
    }
}


