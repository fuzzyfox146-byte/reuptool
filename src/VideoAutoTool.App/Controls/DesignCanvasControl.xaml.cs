using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VideoAutoTool.App.ViewModels;
using VideoAutoTool.Core.Design;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.App.Controls;

/// <summary>
/// 1280x720 design canvas with zoom, click-to-select, drag-move and corner resize.
/// Interaction is driven by a single state machine to avoid the drag/resize
/// event conflicts that previously caused lag and unpredictable behavior.
/// </summary>
public partial class DesignCanvasControl : UserControl
{
    private const double HandleHit = 12;   // hit tolerance for a corner (canvas px)
    private const double HandleVisual = 10; // drawn handle size (canvas px)
    private const double MinSize = 10;      // minimum layer width/height (canvas px)

    public static readonly DependencyProperty LayersProperty =
        DependencyProperty.Register(nameof(Layers), typeof(ObservableCollection<LayerItemViewModel>),
            typeof(DesignCanvasControl), new PropertyMetadata(null, OnLayersChanged));

    public ObservableCollection<LayerItemViewModel>? Layers
    {
        get => (ObservableCollection<LayerItemViewModel>?)GetValue(LayersProperty);
        set => SetValue(LayersProperty, value);
    }

    public static readonly DependencyProperty SelectedLayerProperty =
        DependencyProperty.Register(nameof(SelectedLayer), typeof(LayerItemViewModel),
            typeof(DesignCanvasControl),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedLayerChanged));

    public LayerItemViewModel? SelectedLayer
    {
        get => (LayerItemViewModel?)GetValue(SelectedLayerProperty);
        set => SetValue(SelectedLayerProperty, value);
    }

    public static readonly DependencyProperty HistoryProperty =
        DependencyProperty.Register(nameof(History), typeof(DesignHistory),
            typeof(DesignCanvasControl), new PropertyMetadata(null, OnHistoryChanged));

    /// <summary>Undo/redo history. When set, canvas refreshes on every history change.</summary>
    public DesignHistory? History
    {
        get => (DesignHistory?)GetValue(HistoryProperty);
        set => SetValue(HistoryProperty, value);
    }

    private enum ResizeHandle { None, TopLeft, TopRight, BottomLeft, BottomRight, Left, Right, Top, Bottom }
    private enum Mode { None, Dragging, Resizing }

    private sealed class LayerVisual
    {
        public required LayerItemViewModel Item { get; init; }
        public required Border Container { get; init; }
        public required Rectangle[] Handles { get; init; } // TL, TR, BL, BR
        public required Brush Stroke { get; init; }
        public double BaseWidth { get; init; }
        public double BaseHeight { get; init; }
    }

    private readonly Dictionary<LayerItemViewModel, LayerVisual> _visuals = new();

    private Mode _mode = Mode.None;
    private ResizeHandle _resizeHandle = ResizeHandle.None;
    private LayerItemViewModel? _activeLayer;
    private Point _startMouse;
    // Layer geometry captured at the start of a resize (canvas coords / px).
    private double _startX, _startY, _startW, _startH;

    public DesignCanvasControl()
    {
        InitializeComponent();
        ZoomComboBox.SelectionChanged += OnZoomChanged;
        Loaded += (_, _) => RenderLayers();
    }

    // ---- Layers collection wiring -------------------------------------------------

    private static void OnLayersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DesignCanvasControl control) return;

        if (e.OldValue is ObservableCollection<LayerItemViewModel> oldCol)
        {
            oldCol.CollectionChanged -= control.OnLayersCollectionChanged;
            foreach (var item in oldCol) item.PropertyChanged -= control.OnLayerItemPropertyChanged;
        }

        if (e.NewValue is ObservableCollection<LayerItemViewModel> newCol)
        {
            newCol.CollectionChanged += control.OnLayersCollectionChanged;
            foreach (var item in newCol) item.PropertyChanged += control.OnLayerItemPropertyChanged;
        }

        control.RenderLayers();
    }

    private void OnLayersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (LayerItemViewModel item in e.OldItems) item.PropertyChanged -= OnLayerItemPropertyChanged;
        if (e.NewItems != null)
            foreach (LayerItemViewModel item in e.NewItems) item.PropertyChanged += OnLayerItemPropertyChanged;

        RenderLayers();
    }

    private void OnLayerItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not LayerItemViewModel item) return;

        switch (e.PropertyName)
        {
            case nameof(LayerItemViewModel.X):
            case nameof(LayerItemViewModel.Y):
            case nameof(LayerItemViewModel.LayerScale):
            case nameof(LayerItemViewModel.LayerWidth):
            case nameof(LayerItemViewModel.LayerHeight):
                if (_visuals.TryGetValue(item, out var v)) UpdateContainerGeometry(v);
                break;
            case nameof(LayerItemViewModel.IsVisible):
            case nameof(LayerItemViewModel.PreviewText):
            case nameof(LayerItemViewModel.PreviewColor):
            case nameof(LayerItemViewModel.PreviewFontSize):
                RenderLayers();
                break;
            case nameof(LayerItemViewModel.IsSelected):
            case nameof(LayerItemViewModel.IsLocked):
                UpdateSelectionVisuals();
                break;
        }
    }

    // ---- Selection ----------------------------------------------------------------

    private static void OnSelectedLayerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DesignCanvasControl control || control.Layers == null) return;
        var selected = e.NewValue as LayerItemViewModel;
        foreach (var item in control.Layers)
            item.IsSelected = ReferenceEquals(item, selected);
    }

    // ---- Undo/redo history --------------------------------------------------------

    private static void OnHistoryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DesignCanvasControl control) return;
        if (e.OldValue is DesignHistory oldHistory) oldHistory.Changed -= control.OnHistoryApplied;
        if (e.NewValue is DesignHistory newHistory) newHistory.Changed += control.OnHistoryApplied;
    }

    private void OnHistoryApplied(object? sender, EventArgs e)
    {
        // Model was mutated by execute/undo/redo: redraw and refresh the panel.
        RenderLayers();
        SelectedLayer?.NotifyTransformChanged();
    }

    private void UpdateSelectionVisuals()
    {
        foreach (var v in _visuals.Values)
        {
            bool sel = v.Item.IsSelected;
            v.Container.BorderBrush = sel ? Brushes.Gold : v.Stroke;
            v.Container.BorderThickness = new Thickness(sel ? 3 : 2);
            var handleVisibility = (sel && !v.Item.IsLocked) ? Visibility.Visible : Visibility.Collapsed;
            foreach (var h in v.Handles) h.Visibility = handleVisibility;
        }
    }

    // ---- Rendering ----------------------------------------------------------------

    private void RenderLayers()
    {
        if (MainCanvas == null) return;

        MainCanvas.Children.Clear();
        _visuals.Clear();
        if (Layers == null) return;

        foreach (var item in Layers)
        {
            if (!item.IsVisible) continue;
            var t = item.Layer.Transform ??= new TransformSettings();
            var style = GetStyle(item.Layer.Type);
            var scale = t.Scale <= 0 ? 1.0 : t.Scale;

            // First time a layer is shown we derive an explicit px size from the
            // base size * legacy Scale. After that width/height are independent.
            t.Width ??= (int)Math.Round(style.BaseWidth * scale);
            t.Height ??= (int)Math.Round(style.BaseHeight * scale);

            var hasMedia = !string.IsNullOrWhiteSpace(item.PreviewImagePath)
                || !string.IsNullOrWhiteSpace(item.PreviewVideoPath)
                || !string.IsNullOrWhiteSpace(item.PreviewText);

            var container = new Border
            {
                Width = Math.Max(MinSize, t.Width.Value),
                Height = Math.Max(MinSize, t.Height.Value),
                Background = hasMedia ? Brushes.Transparent : style.Fill,
                BorderBrush = style.Stroke,
                BorderThickness = new Thickness(2),
                Cursor = Cursors.SizeAll
            };

            var grid = new Grid();
            var preview = CreatePreviewContent(item);
            if (preview != null)
            {
                grid.Children.Add(preview);
            }

            var label = new TextBlock
            {
                Text = item.DisplayName,
                Foreground = style.LabelOnLight ? Brushes.Black : Brushes.White,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(style.LabelOnLight
                    ? Color.FromArgb(180, 255, 255, 150)
                    : Color.FromArgb(180, 0, 0, 0)),
                Padding = new Thickness(4),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                IsHitTestVisible = false
            };
            grid.Children.Add(label);

            var handles = new[]
            {
                // Corners
                CreateHandle(HorizontalAlignment.Left, VerticalAlignment.Top, Cursors.SizeNWSE),
                CreateHandle(HorizontalAlignment.Right, VerticalAlignment.Top, Cursors.SizeNESW),
                CreateHandle(HorizontalAlignment.Left, VerticalAlignment.Bottom, Cursors.SizeNESW),
                CreateHandle(HorizontalAlignment.Right, VerticalAlignment.Bottom, Cursors.SizeNWSE),
                // Edges (mid-side)
                CreateHandle(HorizontalAlignment.Left, VerticalAlignment.Center, Cursors.SizeWE),
                CreateHandle(HorizontalAlignment.Right, VerticalAlignment.Center, Cursors.SizeWE),
                CreateHandle(HorizontalAlignment.Center, VerticalAlignment.Top, Cursors.SizeNS),
                CreateHandle(HorizontalAlignment.Center, VerticalAlignment.Bottom, Cursors.SizeNS)
            };
            foreach (var h in handles) grid.Children.Add(h);

            container.Child = grid;
            Canvas.SetLeft(container, t.X);
            Canvas.SetTop(container, t.Y);
            MainCanvas.Children.Add(container);

            var visual = new LayerVisual
            {
                Item = item,
                Container = container,
                Handles = handles,
                Stroke = style.Stroke,
                BaseWidth = style.BaseWidth,
                BaseHeight = style.BaseHeight
            };
            _visuals[item] = visual;

            AttachInteraction(container, item);
        }

        UpdateSelectionVisuals();
    }

    private static UIElement? CreatePreviewContent(LayerItemViewModel item)
    {
        if (!string.IsNullOrWhiteSpace(item.PreviewImagePath) && File.Exists(item.PreviewImagePath))
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(item.PreviewImagePath, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                return new Image
                {
                    Source = bmp,
                    Stretch = Stretch.UniformToFill,
                    IsHitTestVisible = false
                };
            }
            catch
            {
                // Fall through to other preview types.
            }
        }

        if (!string.IsNullOrWhiteSpace(item.PreviewVideoPath) && File.Exists(item.PreviewVideoPath))
        {
            var media = new MediaElement
            {
                Source = new Uri(item.PreviewVideoPath, UriKind.Absolute),
                LoadedBehavior = MediaState.Play,
                UnloadedBehavior = MediaState.Manual,
                IsMuted = true,
                Stretch = Stretch.Fill,
                ScrubbingEnabled = true,
                IsHitTestVisible = false
            };
            media.MediaEnded += (_, _) =>
            {
                media.Position = TimeSpan.Zero;
                media.Play();
            };
            return media;
        }

        if (!string.IsNullOrWhiteSpace(item.PreviewText))
        {
            return new TextBlock
            {
                Text = item.PreviewText,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = item.PreviewFontSize,
                FontWeight = FontWeights.Bold,
                Foreground = ParseBrush(item.PreviewColor),
                Margin = new Thickness(12),
                IsHitTestVisible = false
            };
        }

        return null;
    }

    private static Brush ParseBrush(string color)
    {
        try
        {
            var converted = ColorConverter.ConvertFromString(color);
            if (converted is Color c) return new SolidColorBrush(c);
        }
        catch
        {
            // ignore invalid hex
        }

        return Brushes.Gold;
    }

    private static Rectangle CreateHandle(HorizontalAlignment h, VerticalAlignment v, Cursor cursor)
    {
        return new Rectangle
        {
            Width = HandleVisual,
            Height = HandleVisual,
            Fill = Brushes.White,
            Stroke = Brushes.Black,
            StrokeThickness = 1,
            HorizontalAlignment = h,
            VerticalAlignment = v,
            // pull half the handle outside so it sits centred on the corner/edge
            Margin = new Thickness(
                h == HorizontalAlignment.Left ? -HandleVisual / 2 : 0,
                v == VerticalAlignment.Top ? -HandleVisual / 2 : 0,
                h == HorizontalAlignment.Right ? -HandleVisual / 2 : 0,
                v == VerticalAlignment.Bottom ? -HandleVisual / 2 : 0),
            Cursor = cursor,
            IsHitTestVisible = false, // corner hit-testing is done on the container
            Visibility = Visibility.Collapsed
        };
    }

    private void UpdateContainerGeometry(LayerVisual v)
    {
        var t = v.Item.Layer.Transform;
        if (t == null) return;
        var scale = t.Scale <= 0 ? 1.0 : t.Scale;
        v.Container.Width = Math.Max(MinSize, t.Width ?? v.BaseWidth * scale);
        v.Container.Height = Math.Max(MinSize, t.Height ?? v.BaseHeight * scale);
        Canvas.SetLeft(v.Container, t.X);
        Canvas.SetTop(v.Container, t.Y);
    }

    // ---- Interaction --------------------------------------------------------------

    private void AttachInteraction(Border container, LayerItemViewModel item)
    {
        container.MouseLeftButtonDown += (_, e) => OnMouseDown(container, item, e);
        container.MouseMove += (_, e) => OnMouseMove(container, item, e);
        container.MouseLeftButtonUp += (_, e) => OnMouseUp(container, item, e);
    }

    private void OnMouseDown(Border container, LayerItemViewModel item, MouseButtonEventArgs e)
    {
        // Clicking always selects the layer (so the property panel updates).
        SelectedLayer = item;

        if (item.IsLocked)
        {
            e.Handled = true;
            return;
        }

        var t = item.Layer.Transform;
        if (t == null) return;

        _activeLayer = item;
        _startMouse = e.GetPosition(MainCanvas);
        // Capture the box at gesture start (used for resize math and for the undo record).
        _startX = t.X;
        _startY = t.Y;
        _startW = container.Width;
        _startH = container.Height;

        var local = e.GetPosition(container);
        var handle = HitHandle(local, container.Width, container.Height);
        _mode = handle != ResizeHandle.None ? Mode.Resizing : Mode.Dragging;
        _resizeHandle = handle;

        container.CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(Border container, LayerItemViewModel item, MouseEventArgs e)
    {
        // Idle: just provide cursor feedback for the selected, unlocked layer.
        if (_mode == Mode.None)
        {
            if (item.IsSelected && !item.IsLocked)
            {
                var local = e.GetPosition(container);
                container.Cursor = HitHandle(local, container.Width, container.Height) switch
                {
                    ResizeHandle.TopLeft or ResizeHandle.BottomRight => Cursors.SizeNWSE,
                    ResizeHandle.TopRight or ResizeHandle.BottomLeft => Cursors.SizeNESW,
                    ResizeHandle.Left or ResizeHandle.Right => Cursors.SizeWE,
                    ResizeHandle.Top or ResizeHandle.Bottom => Cursors.SizeNS,
                    _ => Cursors.SizeAll
                };
            }
            else
            {
                container.Cursor = Cursors.SizeAll;
            }
            return;
        }

        if (_activeLayer != item) return;

        var cur = e.GetPosition(MainCanvas);

        if (_mode == Mode.Dragging)
        {
            var dx = cur.X - _startMouse.X;
            var dy = cur.Y - _startMouse.Y;
            // Setting X/Y raises PropertyChanged -> UpdateContainerGeometry (in place),
            // which also keeps the property panel in sync live.
            item.X += dx;
            item.Y += dy;
            _startMouse = cur;
        }
        else if (_mode == Mode.Resizing)
        {
            // Independent width/height resize. The edge/corner opposite the dragged
            // one stays anchored; width and height change on their own axes only.
            bool leftMoving = _resizeHandle is ResizeHandle.TopLeft or ResizeHandle.BottomLeft or ResizeHandle.Left;
            bool rightMoving = _resizeHandle is ResizeHandle.TopRight or ResizeHandle.BottomRight or ResizeHandle.Right;
            bool topMoving = _resizeHandle is ResizeHandle.TopLeft or ResizeHandle.TopRight or ResizeHandle.Top;
            bool bottomMoving = _resizeHandle is ResizeHandle.BottomLeft or ResizeHandle.BottomRight or ResizeHandle.Bottom;

            double rightEdge = _startX + _startW;
            double bottomEdge = _startY + _startH;

            double newW, newX;
            if (leftMoving) { newW = rightEdge - cur.X; newX = cur.X; }
            else if (rightMoving) { newW = cur.X - _startX; newX = _startX; }
            else { newW = _startW; newX = _startX; }

            double newH, newY;
            if (topMoving) { newH = bottomEdge - cur.Y; newY = cur.Y; }
            else if (bottomMoving) { newH = cur.Y - _startY; newY = _startY; }
            else { newH = _startH; newY = _startY; }

            // Clamp to a minimum size while keeping the anchored edge fixed.
            if (newW < MinSize) { newW = MinSize; if (leftMoving) newX = rightEdge - MinSize; }
            if (newH < MinSize) { newH = MinSize; if (topMoving) newY = bottomEdge - MinSize; }

            item.LayerWidth = Math.Round(newW);
            item.LayerHeight = Math.Round(newH);
            item.X = newX;
            item.Y = newY;
        }

        e.Handled = true;
    }

    private void OnMouseUp(Border container, LayerItemViewModel item, MouseButtonEventArgs e)
    {
        if (_activeLayer == item)
        {
            container.ReleaseMouseCapture();
            var wasResizing = _mode == Mode.Resizing;
            _mode = Mode.None;
            _resizeHandle = ResizeHandle.None;
            _activeLayer = null;

            RecordGesture(item, wasResizing);
            item.NotifyTransformChanged();
        }
        e.Handled = true;
    }

    private void RecordGesture(LayerItemViewModel item, bool wasResizing)
    {
        if (History == null) return;
        var t = item.Layer.Transform;
        if (t == null) return;

        int oldW = (int)Math.Round(_startW);
        int oldH = (int)Math.Round(_startH);
        double newX = t.X, newY = t.Y;
        int newW = t.Width ?? oldW;
        int newH = t.Height ?? oldH;

        bool moved = Math.Abs(newX - _startX) > 0.5 || Math.Abs(newY - _startY) > 0.5;
        bool resized = wasResizing && (newW != oldW || newH != oldH);
        if (!moved && !resized) return;

        // Execute re-applies the (already-set) new values and pushes an undo entry.
        History.Execute(new TransformLayerCommand(
            item.Layer, _startX, _startY, oldW, oldH, newX, newY, newW, newH));
    }

    private static ResizeHandle HitHandle(Point local, double width, double height)
    {
        bool left = local.X <= HandleHit;
        bool right = local.X >= width - HandleHit;
        bool top = local.Y <= HandleHit;
        bool bottom = local.Y >= height - HandleHit;

        // Corners take priority over edges.
        if (left && top) return ResizeHandle.TopLeft;
        if (right && top) return ResizeHandle.TopRight;
        if (left && bottom) return ResizeHandle.BottomLeft;
        if (right && bottom) return ResizeHandle.BottomRight;

        // Edges (dragging any point along a side, away from the corners).
        if (left) return ResizeHandle.Left;
        if (right) return ResizeHandle.Right;
        if (top) return ResizeHandle.Top;
        if (bottom) return ResizeHandle.Bottom;

        return ResizeHandle.None;
    }

    // ---- Styling ------------------------------------------------------------------

    private readonly record struct LayerStyle(
        double BaseWidth, double BaseHeight, Brush Fill, Brush Stroke, bool LabelOnLight);

    private static LayerStyle GetStyle(LayerType type) => type switch
    {
        LayerType.BackgroundChain => new(1280, 720,
            new SolidColorBrush(Color.FromArgb(100, 100, 150, 200)), Brushes.CornflowerBlue, false),
        LayerType.Image => new(200, 200,
            new SolidColorBrush(Color.FromArgb(150, 255, 200, 100)), Brushes.Orange, false),
        LayerType.Subtitle => new(600, 100,
            new SolidColorBrush(Color.FromArgb(100, 255, 255, 100)), Brushes.Yellow, true),
        LayerType.LoopVideo => new(400, 80,
            new SolidColorBrush(Color.FromArgb(150, 50, 255, 150)), Brushes.LimeGreen, false),
        LayerType.FixedText => new(300, 60,
            new SolidColorBrush(Color.FromArgb(150, 255, 100, 255)), Brushes.Magenta, false),
        LayerType.StaticImage => new(250, 250,
            new SolidColorBrush(Color.FromArgb(150, 150, 150, 255)), Brushes.CornflowerBlue, false),
        _ => new(100, 100,
            new SolidColorBrush(Color.FromArgb(100, 128, 128, 128)), Brushes.Gray, false)
    };

    // ---- Zoom ---------------------------------------------------------------------

    private void OnZoomChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MainCanvas == null) return;

        var scaleTransform = new ScaleTransform();
        switch (ZoomComboBox.SelectedIndex)
        {
            case 0: // 50%
                scaleTransform.ScaleX = scaleTransform.ScaleY = 0.5;
                break;
            case 1: // Fit
                var availableWidth = ActualWidth - 100;
                var availableHeight = ActualHeight - 100;
                var fitScale = Math.Min(availableWidth / 1280, availableHeight / 720);
                if (fitScale <= 0 || double.IsNaN(fitScale)) fitScale = 0.5;
                scaleTransform.ScaleX = scaleTransform.ScaleY = fitScale;
                break;
            case 2: // 100%
                scaleTransform.ScaleX = scaleTransform.ScaleY = 1.0;
                break;
        }

        MainCanvas.LayoutTransform = scaleTransform;
    }
}
