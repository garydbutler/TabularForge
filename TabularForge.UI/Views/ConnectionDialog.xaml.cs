using System.Windows;
using TabularForge.UI.ViewModels;

namespace TabularForge.UI.Views;

public partial class ConnectionDialog : Window
{
    public ConnectionDialog(ConnectionDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ConnectionDialogViewModel.DialogResult) && viewModel.DialogResult)
            {
                DialogResult = true;
                Close();
            }
        };
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
