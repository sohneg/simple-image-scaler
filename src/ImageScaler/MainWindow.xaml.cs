using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ImageScaler.Core;

namespace ImageScaler;

public partial class MainWindow : Window
{
    private static readonly string[] ImageExtensions =
        { ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".gif" };

    public ObservableCollection<ImageItem> Inputs { get; } = new();
    public ObservableCollection<ImageItem> Outputs { get; } = new();

    private Point _dragStart;

    public MainWindow()
    {
        InitializeComponent();
        InputList.ItemsSource = Inputs;
        OutputList.ItemsSource = Outputs;
        Inputs.CollectionChanged += (_, _) => InputHint.Visibility = Inputs.Any() ? Visibility.Collapsed : Visibility.Visible;
        Outputs.CollectionChanged += (_, _) => OutputHint.Visibility = Outputs.Any() ? Visibility.Collapsed : Visibility.Visible;
    }

    // ---- Drag-in (links) ----
    private void InputList_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void InputList_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
        int ignored = 0;
        foreach (var path in paths)
        {
            if (!File.Exists(path)) continue;
            if (!ImageExtensions.Contains(Path.GetExtension(path).ToLowerInvariant())) { ignored++; continue; }
            Inputs.Add(new ImageItem
            {
                FilePath = path,
                FileName = Path.GetFileName(path),
                SizeBytes = new FileInfo(path).Length
            });
        }
        StatusLabel.Text = ignored > 0 ? $"{ignored} Nicht-Bild-Datei(en) ignoriert." : "";
    }

    // ---- Einstellungen ----
    private void TargetPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CustomMb == null) return;
        CustomMb.IsEnabled = (TargetPreset.SelectedItem as ComboBoxItem)?.Content as string == "Eigene…";
    }

    private bool TryGetTargetBytes(out long bytes)
    {
        bytes = 0;
        var selected = (TargetPreset.SelectedItem as ComboBoxItem)?.Content as string ?? "";
        double mb;
        if (selected == "Eigene…")
        {
            if (!double.TryParse(CustomMb.Text.Trim().Replace(',', '.'),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out mb) || mb <= 0)
            {
                StatusLabel.Text = "Bitte eine gültige Grösse (MB) eingeben.";
                return false;
            }
        }
        else
        {
            mb = double.Parse(new string(selected.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray()),
                System.Globalization.CultureInfo.InvariantCulture);
        }
        bytes = (long)(mb * 1024 * 1024);
        return true;
    }

    // ---- Komprimieren ----
    private async void CompressButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Inputs.Any()) { StatusLabel.Text = "Keine Bilder zum Komprimieren."; return; }
        if (!TryGetTargetBytes(out var target)) return;

        var session = ((App)Application.Current).Session;
        if (session == null) { StatusLabel.Text = "Keine Session – Neustart nötig."; return; }

        CompressButton.IsEnabled = false;
        ClearButton.IsEnabled = false;
        Outputs.Clear();
        Progress.Maximum = Inputs.Count;
        Progress.Value = 0;
        var options = new CompressionOptions { TargetSizeBytes = target };
        int done = 0, failed = 0;

        foreach (var input in Inputs.ToList())
        {
            try
            {
                var result = await Task.Run(() => JpegCompressor.CompressToTarget(input.FilePath, options));
                var outPath = UniquePath(session.FolderPath, Path.GetFileNameWithoutExtension(input.FileName) + ".jpg");
                await File.WriteAllBytesAsync(outPath, result.Data);

                Outputs.Add(new ImageItem
                {
                    FilePath = outPath,
                    FileName = Path.GetFileName(outPath),
                    SizeBytes = result.Size,
                    Quality = result.Quality,
                    TargetMet = result.TargetMet,
                    StatusText = result.TargetMet
                        ? $"Qualität {result.Quality}"
                        : "Zielgrösse nicht erreicht"
                });
            }
            catch (Exception ex)
            {
                failed++;
                Outputs.Add(new ImageItem
                {
                    FilePath = input.FilePath,
                    FileName = input.FileName,
                    SizeBytes = input.SizeBytes,
                    HasError = true,
                    StatusText = "Fehler: " + ex.Message
                });
            }
            Progress.Value = ++done;
        }

        CompressButton.IsEnabled = true;
        ClearButton.IsEnabled = true;
        StatusLabel.Text = failed > 0 ? $"Fertig – {failed} fehlgeschlagen." : "Fertig.";
    }

    private static string UniquePath(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        if (!File.Exists(path)) return path;
        var name = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        int i = 1;
        do { path = Path.Combine(folder, $"{name} ({i++}){ext}"); } while (File.Exists(path));
        return path;
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        Inputs.Clear();
        Outputs.Clear();
        Progress.Value = 0;
        StatusLabel.Text = "";
    }

    // ---- Drag-out (rechts) ----
    private void OutputList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => _dragStart = e.GetPosition(null);

    private void OutputList_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var paths = OutputList.SelectedItems
            .OfType<ImageItem>()
            .Where(i => !i.HasError && File.Exists(i.FilePath))
            .Select(i => i.FilePath)
            .ToArray();
        if (paths.Length == 0) return;

        var data = new DataObject(DataFormats.FileDrop, paths);
        DragDrop.DoDragDrop(OutputList, data, DragDropEffects.Copy);
    }
}
