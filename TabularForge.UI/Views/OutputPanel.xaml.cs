using System.Windows;
using System.Windows.Controls;
using TabularForge.UI.ViewModels;

namespace TabularForge.UI.Views;

public partial class OutputPanel : UserControl
{
    public OutputPanel()
    {
        InitializeComponent();
    }

    private void ClearOutput_Click(object sender, RoutedEventArgs e)
    {
        var vm = DataContext as MainViewModel
            ?? (Application.Current.MainWindow?.DataContext as MainViewModel);
        if (vm != null)
        {
            vm.OutputText = string.Empty;
        }
    }
}
