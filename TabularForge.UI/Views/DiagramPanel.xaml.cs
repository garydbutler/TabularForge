using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using TabularForge.Core.Models;
using TabularForge.UI.ViewModels;

namespace TabularForge.UI.Views;

public partial class DiagramPanel : UserControl
{
    private DiagramViewModel? _vm;
    private DiagramTableCard? _draggingTable;
    private double _dragOffsetX, _dragOffsetY;
    private bool _isPanning;
    private Point _panStart;
    private readonly Dictionary<string, Border> _tableCardElements = new();

    public DiagramPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is DiagramViewModel vm)
        {
            _vm = vm;
            _vm.Tables.CollectionChanged += (_, _) => Dispatcher.BeginInvoke(RenderDiagram);
            _vm.Relationships.CollectionChanged += (_, _) => Dispatcher.BeginInvoke(RenderDiagram);
            _vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(DiagramViewModel.SelectedRelationship))
                {
                    RelationshipPopup.Visibility = _vm.SelectedRelationship != null
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
            };
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RenderDiagram();
    }

    // ===========================
    //  RENDERING
    // ===========================

    private void RenderDiagram()
    {
        if (_vm == null) return;

        DiagramCanvas.Children.Clear();
        _tableCardElements.Clear();

        // Draw grid dots
        DrawGrid();

        // Draw relationship lines first (behind cards)
        foreach (var rel in _vm.Relationships)
        {
            DrawRelationshipLine(rel);
        }

        // Draw table cards
        foreach (var table in _vm.Tables)
        {
            DrawTableCard(table);
        }

        // Update minimap
        UpdateMinimap();
    }

    private void DrawGrid()
    {
        if (!_vm!.SnapToGrid) return;

        var gridSize = _vm.GridSize * 5; // Larger spacing for dots
        for (double x = 0; x < _vm.CanvasWidth; x += gridSize)
        {
            for (double y = 0; y < _vm.CanvasHeight; y += gridSize)
            {
                var dot = new Ellipse
                {
                    Width = 2,
                    Height = 2,
                    Fill = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255))
                };
                Canvas.SetLeft(dot, x - 1);
                Canvas.SetTop(dot, y - 1);
                DiagramCanvas.Children.Add(dot);
            }
        }
    }

    private void DrawRelationshipLine(DiagramRelationship rel)
    {
        var fromTable = _vm!.Tables.FirstOrDefault(t => t.TableName == rel.FromTable);
        var toTable = _vm.Tables.FirstOrDefault(t => t.TableName == rel.ToTable);
        if (fromTable == null || toTable == null) return;

        // Calculate connection points (center of table cards)
        double fromX = fromTable.X + fromTable.Width / 2;
        double fromY = fromTable.Y + fromTable.Height / 2;
        double toX = toTable.X + toTable.Width / 2;
        double toY = toTable.Y + toTable.Height / 2;

        // Clamp to edge of cards
        ClampToEdge(fromTable, toX, toY, ref fromX, ref fromY);
        ClampToEdge(toTable, fromX, fromY, ref toX, ref toY);

        var lineColor = rel.IsSelected
            ? Color.FromRgb(74, 144, 217)
            : rel.IsActive
                ? Color.FromRgb(160, 160, 160)
                : Color.FromRgb(100, 100, 100);

        var line = new Line
        {
            X1 = fromX, Y1 = fromY,
            X2 = toX, Y2 = toY,
            Stroke = new SolidColorBrush(lineColor),
            StrokeThickness = rel.IsSelected ? 3 : 2,
            StrokeDashArray = rel.IsActive ? null : new DoubleCollection { 4, 2 },
            Tag = rel,
            Cursor = Cursors.Hand
        };

        line.MouseDown += (s, e) =>
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                _vm.SelectRelationshipCommand.Execute(rel);
                e.Handled = true;
            }
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
            {
                // Double-click shows relationship popup
                RelationshipPopup.Visibility = Visibility.Visible;
                e.Handled = true;
            }
        };

        DiagramCanvas.Children.Add(line);

        // Draw cardinality labels
        double midX = (fromX + toX) / 2;
        double midY = (fromY + toY) / 2;

        var cardLabel = new TextBlock
        {
            Text = rel.DisplayCardinality,
            FontSize = 10,
            Foreground = new SolidColorBrush(lineColor),
            Background = new SolidColorBrush(Color.FromArgb(200, 30, 30, 30)),
            Padding = new Thickness(3, 1, 3, 1),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(cardLabel, midX - 12);
        Canvas.SetTop(cardLabel, midY - 8);
        DiagramCanvas.Children.Add(cardLabel);

        // Cross-filter direction arrow
        if (rel.CrossFilterDirection == CrossFilterDirection.Both)
        {
            var arrow = new TextBlock
            {
                Text = "<->",
                FontSize = 9,
                Foreground = new SolidColorBrush(lineColor),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(arrow, midX - 12);
            Canvas.SetTop(arrow, midY + 6);
            DiagramCanvas.Children.Add(arrow);
        }
    }

    private void ClampToEdge(DiagramTableCard table, double targetX, double targetY,
        ref double edgeX, ref double edgeY)
    {
        double cx = table.X + table.Width / 2;
        double cy = table.Y + table.Height / 2;
        double dx = targetX - cx;
        double dy = targetY - cy;

        if (Math.Abs(dx) < 1 && Math.Abs(dy) < 1)
        {
            edgeX = cx;
            edgeY = cy;
            return;
        }

        double hw = table.Width / 2;
        double hh = table.Height / 2;
        double scaleX = Math.Abs(dx) > 0 ? hw / Math.Abs(dx) : double.MaxValue;
        double scaleY = Math.Abs(dy) > 0 ? hh / Math.Abs(dy) : double.MaxValue;
        double scale = Math.Min(scaleX, scaleY);

        edgeX = cx + dx * scale;
        edgeY = cy + dy * scale;
    }

    private void DrawTableCard(DiagramTableCard table)
    {
        var card = new Border
        {
            Width = table.Width,
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
            BorderThickness = new Thickness(table.IsSelected ? 2 : 1),
            CornerRadius = new CornerRadius(4),
            Tag = table,
            Cursor = Cursors.SizeAll
        };

        try
        {
            var borderColor = table.IsSelected
                ? Color.FromRgb(74, 144, 217)
                : (Color)ColorConverter.ConvertFromString(table.GroupColor);
            card.BorderBrush = new SolidColorBrush(borderColor);
        }
        catch
        {
            card.BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60));
        }

        var stack = new StackPanel();

        // Header
        var header = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(80, 74, 144, 217)),
            Padding = new Thickness(8, 6, 8, 6),
            CornerRadius = new CornerRadius(3, 3, 0, 0)
        };
        var headerText = new TextBlock
        {
            Text = table.TableName,
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            Foreground = Brushes.White,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        header.Child = headerText;
        stack.Children.Add(header);

        // Columns section
        if (table.IsExpanded && table.Columns.Count > 0)
        {
            var colHeader = new TextBlock
            {
                Text = "Columns",
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                Margin = new Thickness(8, 4, 8, 2),
                FontWeight = FontWeights.SemiBold
            };
            stack.Children.Add(colHeader);

            foreach (var col in table.Columns.Take(15))
            {
                var colPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 1, 8, 1) };

                // Key indicator
                if (col.IsPrimaryKey)
                {
                    colPanel.Children.Add(new TextBlock
                    {
                        Text = "K",
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromRgb(240, 173, 78)),
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 0, 4, 0),
                        ToolTip = "Primary Key"
                    });
                }
                else if (col.IsForeignKey)
                {
                    colPanel.Children.Add(new TextBlock
                    {
                        Text = "FK",
                        FontSize = 8,
                        Foreground = new SolidColorBrush(Color.FromRgb(91, 192, 222)),
                        Margin = new Thickness(0, 0, 4, 0),
                        ToolTip = "Foreign Key"
                    });
                }

                colPanel.Children.Add(new TextBlock
                {
                    Text = col.Name,
                    FontSize = 11,
                    Foreground = Brushes.White,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 140,
                    ToolTip = $"{col.Name} ({col.DataType})"
                });

                if (!string.IsNullOrEmpty(col.DataType))
                {
                    colPanel.Children.Add(new TextBlock
                    {
                        Text = $" ({col.DataType})",
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120))
                    });
                }

                stack.Children.Add(colPanel);
            }

            if (table.Columns.Count > 15)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = $"... +{table.Columns.Count - 15} more",
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                    Margin = new Thickness(8, 2, 8, 2)
                });
            }
        }

        // Measures section
        if (table.IsExpanded && table.Measures.Count > 0)
        {
            var sepLine = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Margin = new Thickness(4, 4, 4, 2)
            };
            stack.Children.Add(sepLine);

            var measHeader = new TextBlock
            {
                Text = "Measures",
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                Margin = new Thickness(8, 2, 8, 2),
                FontWeight = FontWeights.SemiBold
            };
            stack.Children.Add(measHeader);

            foreach (var m in table.Measures.Take(10))
            {
                var measPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 1, 8, 1) };
                measPanel.Children.Add(new TextBlock
                {
                    Text = "M",
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(92, 184, 92)),
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 4, 0)
                });
                measPanel.Children.Add(new TextBlock
                {
                    Text = m.Name,
                    FontSize = 11,
                    Foreground = Brushes.White,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 160,
                    ToolTip = m.Name
                });
                stack.Children.Add(measPanel);
            }

            if (table.Measures.Count > 10)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = $"... +{table.Measures.Count - 10} more",
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                    Margin = new Thickness(8, 2, 8, 2)
                });
            }
        }

        // Bottom padding
        stack.Children.Add(new Border { Height = 4 });

        card.Child = stack;

        // Measure actual height
        card.Measure(new Size(table.Width, double.PositiveInfinity));
        table.Height = Math.Max(60, card.DesiredSize.Height);

        Canvas.SetLeft(card, table.X);
        Canvas.SetTop(card, table.Y);
        DiagramCanvas.Children.Add(card);

        _tableCardElements[table.TableName] = card;

        // Wire up events
        card.MouseDown += TableCard_MouseDown;
    }

    // ===========================
    //  MOUSE INTERACTION
    // ===========================

    private void TableCard_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.Tag is not DiagramTableCard table) return;

        if (e.ChangedButton == MouseButton.Left)
        {
            // Select the table
            _vm?.SelectTableCommand.Execute(table);

            // Start dragging
            _draggingTable = table;
            var pos = e.GetPosition(DiagramCanvas);
            _dragOffsetX = pos.X - table.X;
            _dragOffsetY = pos.Y - table.Y;
            table.IsDragging = true;
            border.CaptureMouse();
            e.Handled = true;
        }
    }

    private void DiagramCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_draggingTable != null) return;

        if (e.ChangedButton == MouseButton.Middle ||
            (e.ChangedButton == MouseButton.Left && Keyboard.IsKeyDown(Key.Space)))
        {
            // Start panning
            _isPanning = true;
            _panStart = e.GetPosition(this);
            DiagramScroller.CaptureMouse();
            e.Handled = true;
        }
        else if (e.ChangedButton == MouseButton.Left)
        {
            // Click on empty canvas - clear selection
            _vm?.ClearSelectionCommand.Execute(null);
        }
    }

    private void DiagramCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingTable != null && e.LeftButton == MouseButtonState.Pressed)
        {
            var pos = e.GetPosition(DiagramCanvas);
            double newX = pos.X - _dragOffsetX;
            double newY = pos.Y - _dragOffsetY;
            _vm?.MoveTable(_draggingTable, newX, newY);

            // Update card position
            if (_tableCardElements.TryGetValue(_draggingTable.TableName, out var border))
            {
                Canvas.SetLeft(border, _draggingTable.X);
                Canvas.SetTop(border, _draggingTable.Y);
            }

            // Redraw relationship lines
            RenderRelationshipLines();
            UpdateMinimap();
        }
        else if (_isPanning)
        {
            var currentPos = e.GetPosition(this);
            var delta = currentPos - _panStart;
            DiagramScroller.ScrollToHorizontalOffset(DiagramScroller.HorizontalOffset - delta.X);
            DiagramScroller.ScrollToVerticalOffset(DiagramScroller.VerticalOffset - delta.Y);
            _panStart = currentPos;
        }
    }

    private void DiagramCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingTable != null)
        {
            _draggingTable.IsDragging = false;
            _draggingTable = null;

            // Re-render to finalize positions
            RenderDiagram();
        }

        if (_isPanning)
        {
            _isPanning = false;
            DiagramScroller.ReleaseMouseCapture();
        }
    }

    private void DiagramScroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            // Ctrl+Wheel = Zoom
            if (e.Delta > 0)
                _vm?.ZoomInCommand.Execute(null);
            else
                _vm?.ZoomOutCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void RenderRelationshipLines()
    {
        if (_vm == null) return;

        // Remove existing lines and labels
        var toRemove = DiagramCanvas.Children.OfType<UIElement>()
            .Where(c => c is Line || (c is TextBlock tb && tb.Tag is null && !_tableCardElements.Values.Any(b =>
            {
                var stack = b.Child as StackPanel;
                return stack?.Children.Contains(c) == true;
            })))
            .ToList();
        // Simpler: just re-render fully
    }

    // ===========================
    //  MINIMAP
    // ===========================

    private void UpdateMinimap()
    {
        if (_vm == null || !_vm.ShowMinimap) return;

        MinimapCanvas.Children.Clear();

        if (_vm.Tables.Count == 0) return;

        double maxX = Math.Max(1, _vm.Tables.Max(t => t.X + t.Width));
        double maxY = Math.Max(1, _vm.Tables.Max(t => t.Y + t.Height));
        double scaleX = MinimapBorder.Width / maxX;
        double scaleY = MinimapBorder.Height / maxY;
        double scale = Math.Min(scaleX, scaleY) * 0.9;

        // Draw relationship lines
        foreach (var rel in _vm.Relationships)
        {
            var from = _vm.Tables.FirstOrDefault(t => t.TableName == rel.FromTable);
            var to = _vm.Tables.FirstOrDefault(t => t.TableName == rel.ToTable);
            if (from == null || to == null) continue;

            var line = new Line
            {
                X1 = (from.X + from.Width / 2) * scale,
                Y1 = (from.Y + from.Height / 2) * scale,
                X2 = (to.X + to.Width / 2) * scale,
                Y2 = (to.Y + to.Height / 2) * scale,
                Stroke = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                StrokeThickness = 0.5,
                IsHitTestVisible = false
            };
            MinimapCanvas.Children.Add(line);
        }

        // Draw table rectangles
        foreach (var table in _vm.Tables)
        {
            var rect = new Rectangle
            {
                Width = Math.Max(2, table.Width * scale),
                Height = Math.Max(2, table.Height * scale),
                Fill = new SolidColorBrush(Color.FromRgb(74, 144, 217)),
                Opacity = table.IsSelected ? 1.0 : 0.6,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(rect, table.X * scale);
            Canvas.SetTop(rect, table.Y * scale);
            MinimapCanvas.Children.Add(rect);
        }

        // Draw viewport rectangle
        double vpLeft = DiagramScroller.HorizontalOffset / _vm.ZoomLevel * scale;
        double vpTop = DiagramScroller.VerticalOffset / _vm.ZoomLevel * scale;
        double vpWidth = DiagramScroller.ViewportWidth / _vm.ZoomLevel * scale;
        double vpHeight = DiagramScroller.ViewportHeight / _vm.ZoomLevel * scale;

        var viewport = new Rectangle
        {
            Width = Math.Max(2, vpWidth),
            Height = Math.Max(2, vpHeight),
            Stroke = new SolidColorBrush(Colors.White),
            StrokeThickness = 1,
            Fill = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(viewport, vpLeft);
        Canvas.SetTop(viewport, vpTop);
        MinimapCanvas.Children.Add(viewport);
    }

    // ===========================
    //  TOOLBAR HANDLERS
    // ===========================

    private void LayoutCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm == null) return;
        _vm.LayoutMode = LayoutCombo.SelectedIndex switch
        {
            0 => DiagramLayoutMode.Tree,
            1 => DiagramLayoutMode.ForceDirected,
            2 => DiagramLayoutMode.Hierarchical,
            _ => DiagramLayoutMode.Tree
        };
    }

    private void ExportPng_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || _vm.Tables.Count == 0) return;

        var dlg = new SaveFileDialog
        {
            Title = "Export Diagram as Image",
            Filter = "PNG Image (*.png)|*.png",
            DefaultExt = ".png"
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            var dpi = 96;
            var rtb = new RenderTargetBitmap(
                (int)(DiagramCanvas.ActualWidth * _vm.ZoomLevel),
                (int)(DiagramCanvas.ActualHeight * _vm.ZoomLevel),
                dpi, dpi, PixelFormats.Pbgra32);
            rtb.Render(DiagramCanvas);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var stream = File.Create(dlg.FileName);
            encoder.Save(stream);

            _vm.LogMessage($"Diagram exported to {dlg.FileName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
