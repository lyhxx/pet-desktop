using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using DesktopPet.Core;

namespace DesktopPet.UI;

/// <summary>
/// 独立粒子/效果层。与宠物状态机完全解耦：宠物播放动作的同时，
/// 由这里单独播放爱心、星星、汗滴、睡眠气泡。
/// </summary>
public sealed class EffectLayer
{
    private static readonly Geometry HeartGeometry = Geometry.Parse(
        "M 8,14 C 8,14 0,9 0,4 C 0,0.5 3,-1 5.5,1 C 6.6,2 7.4,3 8,4 " +
        "C 8.6,3 9.4,2 10.5,1 C 13,-1 16,0.5 16,4 C 16,9 8,14 8,14 Z");

    private static readonly Geometry DropGeometry = Geometry.Parse(
        "M 6,0 C 6,0 12,8 12,12 C 12,15.3 9.3,18 6,18 C 2.7,18 0,15.3 0,12 C 0,8 6,0 6,0 Z");

    private readonly Canvas _canvas;
    private readonly Random _rng = new();

    public EffectLayer(Canvas canvas) => _canvas = canvas;

    public void Play(EffectKind kind)
    {
        switch (kind)
        {
            case EffectKind.Heart:
                Spawn(new Path { Data = HeartGeometry, Fill = new SolidColorBrush(Color.FromRgb(255, 107, 129)) },
                    new Size(16, 14), 0.0, 1.2);
                break;

            case EffectKind.Star:
                Spawn(new Polygon
                {
                    Points = new PointCollection
                    {
                        new(8, 0), new(10, 6), new(16, 6), new(11, 10),
                        new(13, 16), new(8, 12), new(3, 16), new(5, 10), new(0, 6), new(6, 6)
                    },
                    Fill = new SolidColorBrush(Color.FromRgb(255, 213, 74))
                }, new Size(16, 16), 0.0, 1.0);
                break;

            case EffectKind.Sweat:
                Spawn(new Path { Data = DropGeometry, Fill = new SolidColorBrush(Color.FromRgb(127, 200, 248)) },
                    new Size(12, 18), 0.0, 0.9);
                break;

            case EffectKind.Sleep:
                Spawn(new TextBlock
                {
                    Text = "Z",
                    FontSize = 22,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(150, 170, 220))
                }, new Size(18, 22), 0.0, 1.6, riseDistance: 70);
                break;
        }
    }

    private void Spawn(FrameworkElement element, Size size, double scaleFrom, double duration,
        double riseDistance = 56)
    {
        element.Width = size.Width;
        element.Height = size.Height;
        element.IsHitTestVisible = false;
        element.Opacity = 0;
        element.RenderTransformOrigin = new Point(0.5, 0.5);

        double startX = 132 + _rng.NextDouble() * 60;
        double startY = 70 + _rng.NextDouble() * 20;

        Canvas.SetLeft(element, startX);
        Canvas.SetTop(element, startY);

        var translate = new TranslateTransform();
        element.RenderTransform = translate;
        _canvas.Children.Add(element);

        double drift = (_rng.NextDouble() - 0.5) * 24;
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        var rise = new DoubleAnimation(0, -riseDistance, TimeSpan.FromSeconds(duration))
        {
            EasingFunction = easing
        };
        var fade = new DoubleAnimationUsingKeyFrames();
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(duration * 0.25))));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(duration * 0.6))));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(duration))));

        fade.Completed += (_, _) => _canvas.Children.Remove(element);

        translate.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(0, drift, TimeSpan.FromSeconds(duration)));
        translate.BeginAnimation(TranslateTransform.YProperty, rise);
        element.BeginAnimation(UIElement.OpacityProperty, fade);
    }
}
