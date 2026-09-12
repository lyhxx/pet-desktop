using System.Windows;
using System.Windows.Controls;
using DesktopPet.Core;

namespace DesktopPet.UI;

public partial class PetMenuWindow : Window
{
    private const double Radius = 86;
    private const double SpanDegrees = 70;
    private const double EdgePadding = 6;

    public event EventHandler? PetRequested;
    public event EventHandler? OutfitRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? HideRequested;
    public event EventHandler<Food>? FoodRequested;
    public event EventHandler<bool>? PointerInsideChanged;

    private bool _rightSide = true;
    private bool _foodMode;

    public PetMenuWindow()
    {
        InitializeComponent();

        MouseEnter += (_, _) => PointerInsideChanged?.Invoke(this, true);
        MouseLeave += (_, _) => PointerInsideChanged?.Invoke(this, false);

        SetMode(foodMode: false);
    }

    /// <summary>true = 弧线朝右展开，false = 朝左展开。</summary>
    public void SetSide(bool rightSide)
    {
        _rightSide = rightSide;
        LayoutArc();
    }

    /// <summary>每次显示时回到主菜单。</summary>
    public void ResetMode() => SetMode(foodMode: false);

    private void SetMode(bool foodMode)
    {
        _foodMode = foodMode;

        Button[] main = { FeedButton, PetButton, OutfitButton, SettingsButton, HideButton };
        Button[] food = { FishButton, KibbleButton, MilkButton, BackButton };

        foreach (Button b in main)
            b.Visibility = foodMode ? Visibility.Collapsed : Visibility.Visible;
        foreach (Button b in food)
            b.Visibility = foodMode ? Visibility.Visible : Visibility.Collapsed;

        LayoutArc();
    }

    private void LayoutArc()
    {
        Button[] visible = _foodMode
            ? new[] { FishButton, KibbleButton, MilkButton, BackButton }
            : new[] { FeedButton, PetButton, OutfitButton, SettingsButton, HideButton };

        double cy = Height / 2;
        double anchorX = _rightSide ? EdgePadding : Width - EdgePadding;
        double direction = _rightSide ? 1 : -1;

        for (int i = 0; i < visible.Length; i++)
        {
            double t = visible.Length == 1 ? 0 : (i / (double)(visible.Length - 1)) * 2 - 1;
            double angle = t * SpanDegrees * Math.PI / 180.0;

            double x = anchorX + direction * Radius * Math.Cos(angle);
            double y = cy + Radius * Math.Sin(angle);
            double r = visible[i].Width / 2;

            Canvas.SetLeft(visible[i], x - r);
            Canvas.SetTop(visible[i], y - r);
        }
    }

    private void OnFeed(object sender, RoutedEventArgs e) => SetMode(foodMode: true);

    private void OnBack(object sender, RoutedEventArgs e) => SetMode(foodMode: false);

    private void OnFish(object sender, RoutedEventArgs e) => FoodRequested?.Invoke(this, Foods.Fish);

    private void OnKibble(object sender, RoutedEventArgs e) => FoodRequested?.Invoke(this, Foods.Kibble);

    private void OnMilk(object sender, RoutedEventArgs e) => FoodRequested?.Invoke(this, Foods.Milk);

    private void OnPet(object sender, RoutedEventArgs e) => PetRequested?.Invoke(this, EventArgs.Empty);

    private void OnOutfit(object sender, RoutedEventArgs e) => OutfitRequested?.Invoke(this, EventArgs.Empty);

    private void OnSettings(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void OnHide(object sender, RoutedEventArgs e) => HideRequested?.Invoke(this, EventArgs.Empty);
}
