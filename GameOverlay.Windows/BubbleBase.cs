using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GameOverlay.Windows;

public class BubbleBase : Control
{
    protected Ellipse? _ellipse;
    protected Canvas? _parentCanvas;
    private double _bubbleSize = 50;
    private double _bubbleOpacity = 1.0;
    private Point _position;

    public double BubbleSize
    {
        get => _bubbleSize;
        set
        {
            _bubbleSize = value;
            UpdateSize();
        }
    }

    public double BubbleOpacity
    {
        get => _bubbleOpacity;
        set
        {
            _bubbleOpacity = value;
            if (_ellipse != null)
                _ellipse.Opacity = value;
        }
    }

    public Point Position
    {
        get => _position;
        set
        {
            _position = value;
            UpdatePosition();
        }
    }

    public string BubbleBackgroundColor { get; set; } = "#FF1A1A1A";

    protected virtual void UpdateSize()
    {
        if (_ellipse != null)
        {
            _ellipse.Width = _bubbleSize;
            _ellipse.Height = _bubbleSize;
        }
    }

    protected virtual void UpdatePosition()
    {
        if (_parentCanvas != null)
        {
            Canvas.SetLeft(this, _position.X);
            Canvas.SetTop(this, _position.Y);
        }
    }

    public BubbleBase()
    {
        Width = 50;
        Height = 50;
        MouseEnter += BubbleBase_MouseEnter;
        MouseLeave += BubbleBase_MouseLeave;
        MouseLeftButtonDown += BubbleBase_MouseLeftButtonDown;
    }

    protected virtual void BubbleBase_MouseEnter(object sender, MouseEventArgs e)
    {
        if (_ellipse != null)
            _ellipse.Opacity = Math.Min(1.0, _bubbleOpacity + 0.2);
    }

    protected virtual void BubbleBase_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_ellipse != null)
            _ellipse.Opacity = _bubbleOpacity;
    }

    protected virtual void BubbleBase_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // À implémenter dans les classes dérivées
    }

    public virtual void Initialize(Canvas parentCanvas)
    {
        _parentCanvas = parentCanvas;
        _parentCanvas.Children.Add(this);
        UpdatePosition();
    }
}






