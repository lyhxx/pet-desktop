using System.Windows;
using System.Windows.Input;
using DesktopPet.Platform;

namespace DesktopPet.UI;

public partial class SettingsWindow : Window
{
    private sealed record SizeOption(string Label, double Scale);

    private static readonly SizeOption[] SizeOptions =
    {
        new("小", 0.35),
        new("中", 0.5),
        new("大", 0.7)
    };

    public SettingsWindow()
    {
        InitializeComponent();

        AppSettings settings = AppServices.Settings;

        StartupCheck.IsChecked = settings.RunAtStartup;
        TopmostCheck.IsChecked = settings.AlwaysOnTop;
        SoundCheck.IsChecked = settings.SoundEnabled;
        VisibleCheck.IsChecked = settings.PetVisible;
        VolumeSlider.Value = settings.Volume;

        SizeCombo.ItemsSource = SizeOptions;
        SizeCombo.DisplayMemberPath = nameof(SizeOption.Label);
        SizeCombo.SelectedValuePath = nameof(SizeOption.Scale);
        SizeCombo.SelectedValue = SizeOptions
            .OrderBy(o => Math.Abs(o.Scale - settings.PetScale))
            .First().Scale;

        VersionText.Text = $"当前版本 v{AppInfo.Version}";
    }

    private void OnCheckUpdate(object sender, RoutedEventArgs e)
        => _ = AppServices.App.CheckForUpdates(manual: true);

    private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    private void OnSave(object sender, RoutedEventArgs e)
    {
        AppSettings settings = AppServices.Settings;
        SoundManager sound = AppServices.Sound;

        settings.RunAtStartup = StartupCheck.IsChecked == true;
        StartupManager.SetEnabled(settings.RunAtStartup);

        settings.AlwaysOnTop = TopmostCheck.IsChecked == true;
        settings.SoundEnabled = SoundCheck.IsChecked == true;
        settings.Volume = VolumeSlider.Value;

        if (SizeCombo.SelectedValue is double size)
            settings.PetScale = size;

        sound.Enabled = settings.SoundEnabled;
        sound.Volume = settings.Volume;

        AppServices.Pet.ApplySettings();

        bool wantVisible = VisibleCheck.IsChecked == true;
        if (wantVisible && !AppServices.Pet.IsVisible)
            AppServices.Pet.ShowPet();
        else if (!wantVisible && AppServices.Pet.IsVisible)
            AppServices.Pet.HidePet();

        AppServices.RequestSave?.Invoke();

        Close();
    }
}
