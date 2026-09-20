using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using VideoAutoTool.App.ViewModels;
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

    private enum Corner { None, TopLeft, TopRight, BottomLeft, BottomRight }
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
    private Corner _resizeCorner = Corner.None;
    private LayerItemViewModel? _activeLayer;
    private Point _startMouse;
    private double _startScale;
    private double _fixedX, _fixedY; // opposite (anchored) corner in canvas coords during resize

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
                if (_visuals.TryGetValue(item, out var v)) UpdateContainerGeometry(v);
                break;
            case nameof(LayerItemViewModel.IsVisible):
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
            var t = item.Layer.Transform;
            if (t == null) continue;

            var style = GetStyle(item.Layer.Type);
            var scale = t.Scale <= 0 ? 1.0 : t.Scale;

            var container = new Border
            {
                Width = style.BaseWidth * scale,
                Height = style.BaseHeight * scale,
                Background = style.Fill,
                BorderBrush = style.Stroke,
                BorderThickness = new Thickness(2),
                Cursor = Cursors.SizeAll
            };

            var grid = new Grid();

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
                CreateHandle(HorizontalAlignment.Left, VerticalAlignment.Top, Cursors.SizeNWSE),
                CreateHandle(HorizontalAlignment.Right, VerticalAlignment.Top, Cursors.SizeNESW),
                CreateHandle(HorizontalAlignment.Left, VerticalAlignment.Bottom, Cursors.SizeNESW),
                CreateHandle(HorizontalAlignment.Right, VerticalAlignment.Bottom, Cursors.SizeNWSE)
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
            // pull half the handle outside so it sits centred on the corner
            Margin = new Thickness(
                h == HorizontalAlignment.Left ? -HandleVisual / 2 : 0, 0,
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
        v.Container.Width = v.BaseWidth * scale;
        v.Container.Height = v.BaseHeight * scale;
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
        _startScale = t.Scale <= 0 ? 1.0 : t.Scale;

        var local = e.GetPosition(container);
        var corner = HitCorner(local, container.Width, container.Height);
        if (corner != Corner.None && _visuals.TryGetValue(item, out var v))
        {
            _mode = Mode.Resizing;
            _resizeCorner = corner;
            double w = v.BaseWidth * _startScale;
            double h = v.BaseHeight * _startScale;
            (_fixedX, _fixedY) = corner switch
            {
                Corner.BottomRight => (t.X, t.Y),
                Corner.BottomLeft => (t.X + w, t.Y),
                Corner.TopRight => (t.X, t.Y + h),
                Corner.TopLeft => (t.X + w, t.Y + h),
                _ => (t.X, t.Y)
            };
        }
        else
        {
            _mode = Mode.Dragging;
        }

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
                container.Cursor = HitCorner(local, container.Width, container.Height) switch
                {
                    Corner.TopLeft or Corner.BottomRight => Cursors.SizeNWSE,
                    Corner.TopRight or Corner.BottomLeft => Cursors.SizeNESW,
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
        else if (_mode == Mode.Resizing && _visuals.TryGetValue(item, out var v))
        {
            double scaleX = Math.Abs(cur.X - _fixedX) / v.BaseWidth;
            double scaleY = Math.Abs(cur.Y - _fixedY) / v.BaseHeight;
            double scale = Math.Max(0.1, (scaleX + scaleY) / 2.0);

            double newW = v.BaseWidth * scale;
            double newH = v.BaseHeight * scale;

            bool leftMoving = _resizeCorner is Corner.TopLeft or Corner.BottomLeft;
            bool topMoving = _resizeCorner is Corner.TopLeft or Corner.TopRight;

            item.LayerScale = scale;
            item.X = leftMoving ? _fixedX - newW : _fixedX;
            item.Y = topMoving ? _fixedY - newH : _fixedY;
        }

        e.Handled = true;
    }

    private void OnMouseUp(Border container, LayerItemViewModel item, MouseButtonEventArgs e)
    {
        if (_activeLayer == item)
        {
            container.ReleaseMouseCapture();
            _mode = Mode.None;
            _resizeCorner = Corner.None;
            _activeLayer = null;
            item.NotifyTransformChanged();
        }
        e.Handled = true;
    }

    private static Corner HitCorner(Point local, double width, double height)
    {
        bool left = local.X <= HandleHit;
        bool right = local.X >= width - HandleHit;
        bool top = local.Y <= HandleHit;
        bool bottom = local.Y >= height - HandleHit;

        if (left && top) return Corner.TopLeft;
        if (right && top) return Corner.TopRight;
        if (left && bottom) return Corner.BottomLeft;
        if (right && bottom) return Corner.BottomRight;
        return Corner.None;
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
