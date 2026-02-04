using System.Windows;
using System.Windows.Controls;
using TabularForge.Core.Models;
using TabularForge.UI.ViewModels;

namespace TabularForge.UI.Views;

public partial class TomExplorerPanel : UserControl
{
    public TomExplorerPanel()
    {
        InitializeComponent();
    }

    private void TomTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TomNode node)
        {
            var vm = DataContext as MainViewModel
                ?? (Application.Current.MainWindow?.DataContext as MainViewModel);
            if (vm != null)
            {
                vm.SelectedNode = node;
            }
        }
    }
}
