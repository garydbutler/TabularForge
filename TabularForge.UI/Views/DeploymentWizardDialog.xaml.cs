using System.Windows;

namespace TabularForge.UI.Views;

public partial class DeploymentWizardDialog : Window
{
    public DeploymentWizardDialog()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
