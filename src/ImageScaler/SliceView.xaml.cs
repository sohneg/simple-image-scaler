using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageScaler.Core;
// Nur Rectangle wird gebraucht; ein volles "using System.Windows.Shapes" würde
// System.Windows.Shapes.Path mit System.IO.Path kollidieren lassen.
using Rectangle = System.Windows.Shapes.Rectangle;

namespace ImageScaler;

public partial class SliceView : UserControl
{
    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".gif" };

    public ObservableCollection<ImageItem> Outputs { get; } = new();

    private string? _sheetPath;
    private int _imgW, _imgH;
    private SliceGrid _grid = new();

    private double _scale = 1, _offX, _offY;
    private EdgeHandle? _dragHandle;
    private bool _dragIsColumn;
    private Point _listDragStart;

    public SliceView()
    {
        InitializeComponent();
        OutputList.ItemsSource = Outputs;
        Outputs.CollectionChanged += (_, _) =>
            OutputHint.Visibility = Outputs.Any() ? Visibility.Collapsed : Visibility.Visible;
        Overlay.SizeChanged += (_, _) => Redraw();
    }

    // ---- Sheet laden ----
    private void Sheet_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Sheet_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var path = ((string[])e.Data.GetData(DataFormats.FileDrop))
            .FirstOrDefault(p => File.Exists(p) &&
                ImageExtensions.Contains(Path.GetExtension(p).ToLowerInvariant()));
        if (path == null) return;
        LoadSheet(path);
    }

    private void LoadSheet(string path)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;   // Datei nicht sperren
        bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        bmp.UriSource = new Uri(path);
        bmp.EndInit();
        bmp.Freeze();

        _sheetPath = path;
        _imgW = bmp.PixelWidth;
        _imgH = bmp.PixelHeight;
        Preview.Source = bmp;
        DropHint.Visibility = Visibility.Collapsed;
        RebuildGrid();
    }

    // ---- Koordinaten ----
    private void UpdateTransform()
    {
        if (_imgW <= 0 || _imgH <= 0) return;
        double cw = Overlay.ActualWidth, ch = Overlay.ActualHeight;
        if (cw <= 0 || ch <= 0) return;
        _scale = Math.Min(cw / _imgW, ch / _imgH);
        _offX = (cw - _imgW * _scale) / 2;
        _offY = (ch - _imgH * _scale) / 2;
    }

    private double ToCanvasX(int ix) => _offX + ix * _scale;
    private double ToCanvasY(int iy) => _offY + iy * _scale;
    private int ToImageX(double cx) => (int)Math.Round((cx - _offX) / _scale);
    private int ToImageY(double cy) => (int)Math.Round((cy - _offY) / _scale);
    private int Tolerance() => Math.Max(1, (int)Math.Ceiling(6 / _scale));

    // ---- Regler ----
    private void ModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CountPanel == null || SizePanel == null) return;   // während InitializeComponent
        bool bySize = ModeBox.SelectedIndex == 1;
        CountPanel.Visibility = bySize ? Visibility.Collapsed : Visibility.Visible;
        SizePanel.Visibility = bySize ? Visibility.Visible : Visibility.Collapsed;
        RebuildGrid();
    }

    private void Param_Changed(object sender, TextChangedEventArgs e) => RebuildGrid();

    private static int ParseInt(string? s, int fallback)
        => int.TryParse(s?.Trim(), out var v) && v >= 0 ? v : fallback;

    private GridParams ReadParams() => new()
    {
        Mode = ModeBox.SelectedIndex == 1 ? GridMode.BySize : GridMode.ByCount,
        Columns = Math.Max(1, ParseInt(ColumnsBox.Text, 4)),
        Rows = Math.Max(1, ParseInt(RowsBox.Text, 4)),
        CellWidth = Math.Max(1, ParseInt(CellWBox.Text, 32)),
        CellHeight = Math.Max(1, ParseInt(CellHBox.Text, 32)),
        MarginLeft = ParseInt(MarginLeftBox.Text, 0),
        MarginTop = ParseInt(MarginTopBox.Text, 0),
        MarginRight = ParseInt(MarginRightBox.Text, 0),
        MarginBottom = ParseInt(MarginBottomBox.Text, 0),
        SpacingX = ParseInt(SpacingXBox.Text, 0),
        SpacingY = ParseInt(SpacingYBox.Text, 0),
    };

    /// <summary>Regler-Änderung: Raster neu bauen; manuelle Linien gehen bewusst verloren.</summary>
    private void RebuildGrid()
    {
        if (_sheetPath == null || ModeBox == null || ColumnsBox == null) return;
        _grid = GridGenerator.Generate(_imgW, _imgH, ReadParams());
        Redraw();
    }

    private void Redraw()
    {
        if (Overlay == null) return;
        Overlay.Children.Clear();
        if (_sheetPath == null) return;
        UpdateTransform();

        var stroke = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
        stroke.Freeze();

        foreach (var cell in _grid.Cells)
        {
            var r = new Rectangle
            {
                Width = Math.Max(1, cell.Width * _scale),
                Height = Math.Max(1, cell.Height * _scale),
                Stroke = stroke,
                StrokeThickness = 1,
                Fill = Brushes.Transparent,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(r, ToCanvasX(cell.X));
            Canvas.SetTop(r, ToCanvasY(cell.Y));
            Overlay.Children.Add(r);
        }

        if (StatusLabel != null)
            StatusLabel.Text = string.Format(Loc.Instance["SlicedCells"], _grid.Cells.Count());
    }

    // ---- Linien bearbeiten ----
    private void Overlay_MouseMove(object sender, MouseEventArgs e)
    {
        if (_sheetPath == null) return;
        var p = e.GetPosition(Overlay);
        int ix = ToImageX(p.X), iy = ToImageY(p.Y);

        if (e.LeftButton == MouseButtonState.Pressed && _dragHandle != null)
        {
            if (_dragIsColumn) _grid.MoveColumnEdge(_dragHandle.Value, ix);
            else _grid.MoveRowEdge(_dragHandle.Value, iy);
            Redraw();
            return;
        }

        int tol = Tolerance();
        if (_grid.FindNearestColumnEdge(ix, tol) != null) Overlay.Cursor = Cursors.SizeWE;
        else if (_grid.FindNearestRowEdge(iy, tol) != null) Overlay.Cursor = Cursors.SizeNS;
        else Overlay.Cursor = Cursors.Arrow;
    }

    private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_sheetPath == null) return;
        var p = e.GetPosition(Overlay);
        int ix = ToImageX(p.X), iy = ToImageY(p.Y);

        // Canvas hat kein MouseDoubleClick -> über ClickCount erkennen.
        if (e.ClickCount == 2)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) _grid.SplitRowAt(iy);
            else _grid.SplitColumnAt(ix);
            Redraw();
            return;
        }

        int tol = Tolerance();
        var col = _grid.FindNearestColumnEdge(ix, tol);
        if (col != null) { _dragHandle = col; _dragIsColumn = true; Overlay.CaptureMouse(); return; }
        var row = _grid.FindNearestRowEdge(iy, tol);
        if (row != null) { _dragHandle = row; _dragIsColumn = false; Overlay.CaptureMouse(); }
    }

    private void Overlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragHandle = null;
        Overlay.ReleaseMouseCapture();
    }

    private void Overlay_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_sheetPath == null) return;
        var p = e.GetPosition(Overlay);
        int ix = ToImageX(p.X), iy = ToImageY(p.Y);
        int tol = Tolerance();

        var col = _grid.FindNearestColumnEdge(ix, tol);
        if (col != null) { _grid.RemoveColumnEdge(col.Value); Redraw(); return; }
        var row = _grid.FindNearestRowEdge(iy, tol);
        if (row != null) { _grid.RemoveRowEdge(row.Value); Redraw(); }
    }

    // ---- Schneiden ----
    private async void SliceButton_Click(object sender, RoutedEventArgs e)
    {
        if (_sheetPath == null) { StatusLabel.Text = Loc.Instance["NoSheet"]; return; }
        var session = ((App)Application.Current).Session;
        if (session == null) { StatusLabel.Text = Loc.Instance["NoSession"]; return; }

        SliceButton.IsEnabled = false;
        Outputs.Clear();
        var path = _sheetPath;
        var grid = _grid;

        try
        {
            var results = await Task.Run(() => ImageSlicer.Slice(path, grid));
            var baseName = Path.GetFileNameWithoutExtension(path);
            foreach (var r in results)
            {
                var outPath = Path.Combine(session.FolderPath, $"{baseName}_r{r.Row}_c{r.Col}.png");
                await File.WriteAllBytesAsync(outPath, r.PngData);
                Outputs.Add(new ImageItem
                {
                    FilePath = outPath,
                    FileName = Path.GetFileName(outPath),
                    SizeBytes = r.Size,
                    StatusText = ""
                });
            }
            StatusLabel.Text = string.Format(Loc.Instance["SlicedCells"], results.Count);
        }
        catch (Exception ex)
        {
            StatusLabel.Text = Loc.Instance["ErrorPrefix"] + ex.Message;
        }
        finally
        {
            SliceButton.IsEnabled = true;
        }
    }

    // ---- Drag-out (wie im Compress-Tab) ----
    private void OutputList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => _listDragStart = e.GetPosition(null);

    private void OutputList_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _listDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _listDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var paths = OutputList.SelectedItems
            .OfType<ImageItem>()
            .Where(i => !i.HasError && File.Exists(i.FilePath))
            .Select(i => i.FilePath)
            .ToArray();
        if (paths.Length == 0) return;

        DragDrop.DoDragDrop(OutputList, new DataObject(DataFormats.FileDrop, paths), DragDropEffects.Copy);
    }
}
