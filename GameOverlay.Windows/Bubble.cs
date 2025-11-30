using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace GameOverlay.Windows;

public abstract class Bubble : UserControl
{
    protected Ellipse? _bubbleEllipse;
    protected double _bubbleSize;
    protected double _bubbleOpacity;
    protected double _bubbleX;
    protected double _bubbleY;
    protected string _bubbleBackgroundColor;

    public double BubbleSize
    {
        get => _bubbleSize;
        set
        {
            _bubbleSize = value;
            UpdateBubbleSize();
        }
    }

    public double BubbleOpacity
    {
        get => _bubbleOpacity;
        set
        {
            _bubbleOpacity = value;
            UpdateBubbleOpacity();
        }
    }

    public double BubbleX
    {
        get => _bubbleX;
        set
        {
            _bubbleX = value;
            UpdatePosition();
        }
    }

    public double BubbleY
    {
        get => _bubbleY;
        set
        {
            _bubbleY = value;
            UpdatePosition();
        }
    }

    public string BubbleBackgroundColor
    {
        get => _bubbleBackgroundColor;
        set
        {
            _bubbleBackgroundColor = value;
            UpdateBackgroundColor();
        }
    }

    protected Bubble()
    {
        _bubbleSize = 50.0;
        _bubbleOpacity = 1.0;
        _bubbleBackgroundColor = "#FF1A1A1A";
        
        CreateBubble();
        MouseEnter += Bubble_MouseEnter;
        MouseLeave += Bubble_MouseLeave;
        MouseLeftButtonDown += Bubble_MouseLeftButtonDown;
    }

    protected virtual void CreateBubble()
    {
        _bubbleEllipse = new Ellipse
        {
            Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_bubbleBackgroundColor)),
            Stroke = new SolidColorBrush(Colors.Gray),
            StrokeThickness = 1,
            Width = _bubbleSize,
            Height = _bubbleSize,
            Opacity = _bubbleOpacity
        };

        Content = _bubbleEllipse;
        Width = _bubbleSize;
        Height = _bubbleSize;
    }

    protected virtual void UpdateBubbleSize()
    {
        if (_bubbleEllipse != null)
        {
            _bubbleEllipse.Width = _bubbleSize;
            _bubbleEllipse.Height = _bubbleSize;
            Width = _bubbleSize;
            Height = _bubbleSize;
        }
    }

    protected virtual void UpdateBubbleOpacity()
    {
        if (_bubbleEllipse != null)
        {
            _bubbleEllipse.Opacity = _bubbleOpacity;
        }
    }

    protected virtual void UpdatePosition()
    {
        // Position sera gérée par le parent Canvas
    }

    protected virtual void UpdateBackgroundColor()
    {
        if (_bubbleEllipse != null)
        {
            try
            {
                _bubbleEllipse.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_bubbleBackgroundColor));
            }
            catch { }
        }
    }

    protected virtual void Bubble_MouseEnter(object sender, MouseEventArgs e)
    {
        if (_bubbleEllipse != null)
        {
            _bubbleEllipse.Opacity = Math.Min(1.0, _bubbleOpacity + 0.2);
        }
    }

    protected virtual void Bubble_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_bubbleEllipse != null)
        {
            _bubbleEllipse.Opacity = _bubbleOpacity;
        }
    }

    protected abstract void Bubble_MouseLeftButtonDown(object sender, MouseButtonEventArgs e);
}

