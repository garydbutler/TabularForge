using System.Windows;

namespace TabularForge.UI.Views;

public partial class ImportWizardDialog : Window
{
    public ImportWizardDialog()
    {
        InitializeComponent();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
