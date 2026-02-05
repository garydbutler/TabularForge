using System.Windows;
using System.Windows.Controls;
using TabularForge.UI.ViewModels;

namespace TabularForge.UI.Views;

public partial class WelcomePanel : UserControl
{
    public WelcomePanel()
    {
        InitializeComponent();
        Loaded += WelcomePanel_Loaded;
    }

    private void WelcomePanel_Loaded(object sender, RoutedEventArgs e)
    {
        // AvalonDock doesn't propagate DataContext to LayoutDocument content.
        // Explicitly bind to MainViewModel so commands like ConnectToServer work.
        if (DataContext is not MainViewModel)
        {
            DataContext = Application.Current.MainWindow?.DataContext as MainViewModel;
        }
    }
}
