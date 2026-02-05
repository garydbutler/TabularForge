using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TabularForge.UI.ViewModels;

namespace TabularForge.UI.Views;

public partial class CSharpScriptPanel : UserControl
{
    private ScriptEditorViewModel? _vm;

    public CSharpScriptPanel()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Set up keyboard shortcut for F5 = Run
        var f5Binding = new KeyBinding(
            new RelayInputCommand(() => _vm?.ExecuteScriptCommand.Execute(null)),
            Key.F5, ModifierKeys.None);
        InputBindings.Add(f5Binding);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        _vm = DataContext as ScriptEditorViewModel;
        if (_vm == null) return;

        // Sync code from ViewModel to editor
        ScriptEditor.Text = _vm.ScriptCode;

        // Sync editor changes back to ViewModel
        ScriptEditor.TextChanged += (_, _) =>
        {
            if (_vm != null)
                _vm.ScriptCode = ScriptEditor.Text;
        };

        // Watch for ViewModel code changes (e.g., snippet insertion, macro load)
        _vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ScriptEditorViewModel.ScriptCode))
            {
                if (ScriptEditor.Text != _vm.ScriptCode)
                {
                    Dispatcher.Invoke(() => ScriptEditor.Text = _vm.ScriptCode);
                }
            }
        };
    }

    // Simple ICommand wrapper for key bindings
    private class RelayInputCommand : ICommand
    {
        private readonly Action _execute;
        public RelayInputCommand(Action execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
