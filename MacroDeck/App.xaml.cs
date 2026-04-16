using System.Windows;

namespace MacroDeck;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Ensure hooks are cleaned up on exit.
        if (MainWindow?.DataContext is ViewModels.MainViewModel vm)
        {
            vm.Dispose();
        }
        base.OnExit(e);
    }
}
