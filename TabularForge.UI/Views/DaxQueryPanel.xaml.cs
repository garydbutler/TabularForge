using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.Highlighting;
using TabularForge.UI.ViewModels;

namespace TabularForge.UI.Views;

public partial class DaxQueryPanel : UserControl
{
    public DaxQueryPanel()
    {
        InitializeComponent();

        // Apply DAX syntax highlighting from global registration
        var daxHighlighting = HighlightingManager.Instance.GetDefinition("DAX");
        if (daxHighlighting != null)
            QueryEditor.SyntaxHighlighting = daxHighlighting;

        DataContextChanged += (s, e) =>
        {
            if (DataContext is DaxQueryViewModel vm)
            {
                QueryEditor.Text = vm.QueryText;

                QueryEditor.TextChanged += (sender, args) =>
                {
                    vm.QueryText = QueryEditor.Text;
                    if (vm.ActiveTab != null)
                        vm.ActiveTab.QueryText = QueryEditor.Text;
                };

                vm.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(vm.QueryText) && QueryEditor.Text != vm.QueryText)
                        QueryEditor.Text = vm.QueryText;
                };
            }
        };

        // F5 to execute
        QueryEditor.InputBindings.Add(new KeyBinding(
            new RelayExecuteCommand(() =>
            {
                if (DataContext is DaxQueryViewModel vm && vm.ExecuteQueryCommand.CanExecute(null))
                    vm.ExecuteQueryCommand.Execute(null);
            }),
            Key.F5, ModifierKeys.None));
    }

    private class RelayExecuteCommand : ICommand
    {
        private readonly Action _execute;
        public RelayExecuteCommand(Action execute) => _execute = execute;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }
}
