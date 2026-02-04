using System.Windows;
using System.Windows.Controls;
using TabularForge.UI.ViewModels;

namespace TabularForge.UI.Views;

public partial class MessagesPanel : UserControl
{
    public MessagesPanel()
    {
        InitializeComponent();
    }

    private void ClearMessages_Click(object sender, RoutedEventArgs e)
    {
        var vm = DataContext as MainViewModel
            ?? (Application.Current.MainWindow?.DataContext as MainViewModel);
        if (vm != null)
        {
            vm.MessagesText = string.Empty;
        }
    }
}
