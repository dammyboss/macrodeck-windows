using System.Windows;
using System.Windows.Interop;
using MacroDeck.ViewModels;

namespace MacroDeck;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (DataContext is MainViewModel vm)
        {
            vm.AttachHotkeys(hwnd);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.Dispose();
        }
        base.OnClosed(e);
    }
}
