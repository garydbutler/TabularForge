using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TabularForge.UI.ViewModels;

namespace TabularForge.UI.Views;

public partial class ErrorListPanel : UserControl
{
    private ErrorListViewModel? _viewModel;

    public ErrorListPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is MainViewModel mainVm)
        {
            _viewModel = mainVm.ErrorList;
            UpdateGrid();
            _viewModel.Items.CollectionChanged += (_, _) => UpdateGrid();
        }
    }

    private void UpdateGrid()
    {
        if (_viewModel == null) return;

        Dispatcher.BeginInvoke(() =>
        {
            var showErrors = ShowErrorsCheck.IsChecked == true;
            var showWarnings = ShowWarningsCheck.IsChecked == true;
            var showInfo = ShowInfoCheck.IsChecked == true;

            var items = _viewModel.Items.Where(i =>
                (showErrors && i.Severity == DAXParser.Semantics.DaxDiagnosticSeverity.Error) ||
                (showWarnings && i.Severity == DAXParser.Semantics.DaxDiagnosticSeverity.Warning) ||
                (showInfo && i.Severity == DAXParser.Semantics.DaxDiagnosticSeverity.Info))
                .ToList();

            ErrorGrid.ItemsSource = items;
            SummaryText.Text = _viewModel.SummaryText;
        });
    }

    private void FilterChanged(object sender, RoutedEventArgs e)
    {
        UpdateGrid();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.Clear();
    }

    private void ErrorGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ErrorGrid.SelectedItem is ErrorListItem item)
        {
            _viewModel?.RaiseNavigateToError(item);

            var mainVm = DataContext as MainViewModel
                ?? (Application.Current.MainWindow?.DataContext as MainViewModel);
            mainVm?.AddMessage($"Navigate to: {item.Source} Line {item.Line}, Col {item.Column}");
        }
    }
}
