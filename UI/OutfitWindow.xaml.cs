using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DesktopPet.Core;

namespace DesktopPet.UI;

public partial class OutfitWindow : Window
{
    private sealed record Option(string Label, string Value);

    private static readonly Option[] HatOptions =
    {
        new("无", OutfitState.None),
        new("小帽子", "cap")
    };

    private static readonly Option[] GlassesOptions =
    {
        new("无", OutfitState.None),
        new("眼镜", "glasses")
    };

    private static readonly Option[] ScarfOptions =
    {
        new("无", OutfitState.None),
        new("围巾", "scarf")
    };

    private bool _initialized;

    public OutfitWindow()
    {
        InitializeComponent();

        OutfitState outfit = AppServices.Session.Outfit;

        SetupCombo(HatCombo, HatOptions, outfit.Hat);
        SetupCombo(GlassesCombo, GlassesOptions, outfit.Glasses);
        SetupCombo(ScarfCombo, ScarfOptions, outfit.Scarf);

        _initialized = true;
    }

    private static void SetupCombo(ComboBox combo, Option[] options, string current)
    {
        combo.ItemsSource = options;
        combo.DisplayMemberPath = nameof(Option.Label);
        combo.SelectedValuePath = nameof(Option.Value);
        combo.SelectedValue = current;
    }

    private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;

        OutfitState outfit = AppServices.Session.Outfit;
        outfit.Hat = HatCombo.SelectedValue as string ?? OutfitState.None;
        outfit.Glasses = GlassesCombo.SelectedValue as string ?? OutfitState.None;
        outfit.Scarf = ScarfCombo.SelectedValue as string ?? OutfitState.None;

        AppServices.Pet.ApplyOutfit();
        AppServices.RequestSave?.Invoke();
    }
}
