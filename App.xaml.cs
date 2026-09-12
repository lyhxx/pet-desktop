using System.Windows;
using System.Windows.Threading;
using DesktopPet.Core;
using DesktopPet.Platform;
using DesktopPet.Save;
using DesktopPet.UI;

namespace DesktopPet;

public partial class App : Application
{
    private SaveManager _save = null!;
    private TrayManager _tray = null!;
    private SoundManager _sound = null!;
    private PetWindow _pet = null!;
    private PetSession _session = null!;
    private AppSettings _settings = null!;
    private DispatcherTimer _autoSave = null!;

    public bool IsExiting { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _save = new SaveManager();
        PetSaveData data = _save.Load();
        _settings = data.Settings;
        _session = data.ToSession();
        _sound = new SoundManager
        {
            Enabled = _settings.SoundEnabled,
            Volume = _settings.Volume
        };

        AppServices.App = this;
        AppServices.Session = _session;
        AppServices.Settings = _settings;
        AppServices.Sound = _sound;

        _pet = new PetWindow(_session, _sound);
        AppServices.Pet = _pet;
        AppServices.RequestSave = SaveNow;

        if (!_settings.PetVisible)
            _pet.Opacity = 0;

        _pet.Show();

        _tray = new TrayManager();
        _tray.Initialize();

        _autoSave = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _autoSave.Tick += (_, _) => SaveNow();
        _autoSave.Start();

        SessionEnding += (_, _) =>
        {
            IsExiting = true;
            SaveNow();
        };

        // 启动后延迟检查更新（未配置仓库地址时自动跳过）。
        _ = Dispatcher.InvokeAsync(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(6));
            await CheckForUpdates(manual: false);
        });
    }

    public async Task CheckForUpdates(bool manual)
    {
        UpdateInfo? info = await UpdateService.CheckAsync();
        if (info is not null)
            _tray.NotifyUpdate(info.Version, info.Url);
        else if (manual)
            _tray.NotifyInfo("检查更新", "当前已是最新版本。");
    }

    public void SaveNow()
    {
        (double x, double y) = _pet.GetAnchorPosition();
        bool hasPosition = double.IsFinite(x) && double.IsFinite(y);
        _save.Save(PetSaveData.From(_session, _settings, x, y, hasPosition));
    }

    public void ExitApp()
    {
        IsExiting = true;
        _autoSave.Stop();
        SaveNow();
        _tray.Dispose();
        Shutdown();
    }
}
