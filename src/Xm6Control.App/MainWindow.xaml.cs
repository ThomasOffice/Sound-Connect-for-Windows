using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.UI;
using Xm6Control.Bluetooth.Windows;
using Xm6Control.Core;
using Xm6Control.Protocol;

namespace Xm6Control.App;

public sealed partial class MainWindow : Window
{
    private static readonly TandemEqPresetId[] SupportedPresets =
    [
        TandemEqPresetId.Off,
        TandemEqPresetId.Heavy,
        TandemEqPresetId.Clear,
        TandemEqPresetId.Hard,
        TandemEqPresetId.Soft,
        TandemEqPresetId.GamingEq,
        TandemEqPresetId.Custom,
        TandemEqPresetId.UserSetting1,
        TandemEqPresetId.UserSetting2,
    ];

    private static readonly Dictionary<TandemEqPresetId, byte[]> PresetDefaultBands = new()
    {
        [TandemEqPresetId.Off] = new byte[] { 6, 6, 6, 6, 6, 6, 6, 6, 6, 6 },
        [TandemEqPresetId.Heavy] = new byte[] { 10, 10, 5, 5, 6, 6, 6, 6, 6, 6 },
        [TandemEqPresetId.Clear] = new byte[] { 6, 6, 6, 6, 6, 6, 6, 8, 8, 9 },
        [TandemEqPresetId.Hard] = new byte[] { 10, 10, 5, 5, 6, 6, 6, 8, 8, 9 },
        [TandemEqPresetId.Soft] = new byte[] { 3, 4, 5, 7, 7, 7, 6, 6, 4, 3 },
        [TandemEqPresetId.GamingEq] = new byte[] { 0, 4, 8, 9, 7, 5, 7, 9, 6, 4 },
        [TandemEqPresetId.UserSetting1] = new byte[] { 7, 2, 6, 6, 10, 8, 8, 9, 7, 11 },
        [TandemEqPresetId.UserSetting2] = new byte[] { 5, 2, 6, 6, 10, 8, 8, 9, 7, 11 },
    };

    private static NoiseSensitivityOption[] BuildNoiseSensitivityOptions() =>
    [
        new(TandemNoiseAdaptiveSensitivity.Standard, Localization.Text("sensitivity.standard")),
        new(TandemNoiseAdaptiveSensitivity.High, Localization.Text("sensitivity.high")),
        new(TandemNoiseAdaptiveSensitivity.Low, Localization.Text("sensitivity.low")),
    ];

    private static BgmRoomOption[] BuildBgmRoomOptions() =>
    [
        new(TandemRoomSize.Small, Localization.Text("listening.room.small")),
        new(TandemRoomSize.Middle, Localization.Text("listening.room.middle")),
        new(TandemRoomSize.Large, Localization.Text("listening.room.large")),
    ];

    private static AutoPowerOption[] BuildAutoPowerOptions() =>
    [
        new(TandemAutoPowerOffElement.WhenRemovedFromEars, Localization.Text("autopower.removed")),
        new(TandemAutoPowerOffElement.Disable, Localization.Text("autopower.disable")),
    ];

    private static ThemeOption[] BuildThemeOptions() =>
    [
        new(AppTheme.System, Localization.Text("theme.system")),
        new(AppTheme.Light, Localization.Text("theme.light")),
        new(AppTheme.Dark, Localization.Text("theme.dark")),
    ];

    private static BackdropOption[] BuildBackdropOptions() =>
    [
        new(AppBackdrop.Mica, Localization.Text("backdrop.mica")),
        new(AppBackdrop.Acrylic, Localization.Text("backdrop.acrylic")),
        new(AppBackdrop.Solid, Localization.Text("backdrop.solid")),
    ];

    private static RefreshOption[] BuildRefreshOptions() =>
    [
        new(0, Localization.Text("refresh.off")),
        new(1, Localization.Text("refresh.1")),
        new(5, Localization.Text("refresh.5")),
        new(15, Localization.Text("refresh.15")),
    ];

    private static TrayClickOption[] BuildTrayClickOptions() =>
    [
        new(TrayClickAction.OpenWindow, Localization.Text("trayclick.openwindow")),
        new(TrayClickAction.ShowMenu, Localization.Text("trayclick.showmenu")),
    ];

    private static LanguageOption[] BuildLanguageOptions() =>
    [
        new(UserSettings.ChineseLanguage, "中文"),
        new(UserSettings.EnglishLanguage, "English"),
    ];

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Xm6Control",
        "settings.json");

    private readonly List<Slider> _eqSliders = [];
    private readonly Dictionary<TandemEqPresetId, byte[]> _eqPresetBands = [];
    private readonly SettingsStore _settingsStore;
    private readonly TandemSessionHost _tandemHost = new();
    private UserSettings _settings;
    private DispatcherTimer? _refreshTimer;
    private TrayIcon? _trayIcon;
    private TandemSessionReport? _lastReport;
    private TandemNcAsmState? _lastNoise;
    private TandemEqState? _lastEq;
    private TandemListeningModeState? _lastListening;
    private bool? _lastMultipoint;
    private TandemAutoPowerOffState? _lastAutoPower;
    private DateTime? _lastUpdatedAt;
    private bool _allowClose;
    private bool _busy;
    private bool _updatingDsee;
    private bool _suppressSettingsSave;
    private string _currentSection = "overview";

    public MainWindow()
    {
        _settingsStore = new SettingsStore(SettingsPath);
        _settings = _settingsStore.Load();
        Localization.SetLanguage(_settings.Language);

        InitializeComponent();
        Title = "XM6 Control";
        ApplyAppIcon();
        Localization.Changed += OnLocalizationChanged;

        AmbientLevelSlider.Minimum = 1;
        AmbientLevelSlider.Maximum = 20;
        AmbientLevelSlider.StepFrequency = 1;
        AmbientLevelSlider.Value = 20;
        BuildEqSliders();
        foreach (var preset in PresetDefaultBands)
        {
            _eqPresetBands[preset.Key] = preset.Value;
        }

        ConfigureOptionLists();
        LoadSettingsIntoUi();
        RefreshSceneList();
        ApplyTheme();
        ApplyBackdrop();
        RootGrid.ActualThemeChanged += (_, _) =>
        {
            UpdateTitleBarColors();
            if (_settings.Backdrop == AppBackdrop.Solid)
            {
                ApplyBackdrop();
            }
        };

        AppWindow.Closing += AppWindow_Closing;
        Closed += MainWindow_Closed;
        InitializeTrayIcon();
        NavigateTo(_settings.LastSection);

        StartRefreshTimer();
        ShouldStartMinimized = _settings.StartMinimized && _trayIcon?.IsAvailable == true;
        if (!ShouldStartMinimized)
        {
            _tandemHost.KeepAlive = true;
            _ = PrewarmAsync();
        }

        if (_settings.RefreshOnStart)
        {
            _ = RefreshAsync(silent: true);
        }
    }

    /// <summary>True when the app should start hidden in the notification area.</summary>
    public bool ShouldStartMinimized { get; }

    private sealed record NoiseSensitivityOption(
        TandemNoiseAdaptiveSensitivity Value,
        string Label);

    private sealed record BgmRoomOption(TandemRoomSize Value, string Label);

    private sealed record AutoPowerOption(TandemAutoPowerOffElement Value, string Label);

    private sealed record ThemeOption(AppTheme Value, string Label);

    private sealed record BackdropOption(AppBackdrop Value, string Label);

    private sealed record RefreshOption(int Minutes, string Label);

    private sealed record TrayClickOption(TrayClickAction Value, string Label);

    private sealed record LanguageOption(string Value, string Label);

    private void ApplyAppIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (!File.Exists(iconPath))
        {
            return;
        }

        try
        {
            AppWindow.SetIcon(iconPath);
        }
        catch
        {
            // The exe icon still applies if the title-bar icon cannot be set.
        }
    }

    private void ConfigureOptionLists()
    {
        EqPresetCombo.ItemsSource = SupportedPresets;
        RebuildOptionLists();
        ListeningModeRadio.SelectedIndex = 0;
        NoiseModeRadio.SelectedIndex = 0;
        BgmRoomCombo.IsEnabled = false;
    }

    /// <summary>
    /// Rebuilds the option lists with labels in the current language while
    /// keeping the user's selection.
    /// </summary>
    private void RebuildOptionLists()
    {
        _suppressSettingsSave = true;
        try
        {
            var sensitivity = (NoiseAdaptiveSensitivityCombo.SelectedItem as NoiseSensitivityOption)?.Value;
            var room = (BgmRoomCombo.SelectedItem as BgmRoomOption)?.Value;
            var autoPower = (AutoPowerCombo.SelectedItem as AutoPowerOption)?.Value;
            var theme = (ThemeCombo.SelectedItem as ThemeOption)?.Value;
            var backdrop = (BackdropCombo.SelectedItem as BackdropOption)?.Value;
            var refresh = (AutoRefreshCombo.SelectedItem as RefreshOption)?.Minutes;
            var trayClick = (TrayClickCombo.SelectedItem as TrayClickOption)?.Value;
            var language = (LanguageCombo.SelectedItem as LanguageOption)?.Value;

            var sensitivities = BuildNoiseSensitivityOptions();
            NoiseAdaptiveSensitivityCombo.ItemsSource = sensitivities;
            NoiseAdaptiveSensitivityCombo.SelectedItem =
                sensitivities.FirstOrDefault(option => option.Value == sensitivity) ?? sensitivities[0];

            var rooms = BuildBgmRoomOptions();
            BgmRoomCombo.ItemsSource = rooms;
            BgmRoomCombo.SelectedItem =
                rooms.FirstOrDefault(option => option.Value == room) ?? rooms[1];

            var autoPowerOptions = BuildAutoPowerOptions();
            AutoPowerCombo.ItemsSource = autoPowerOptions;
            AutoPowerCombo.SelectedItem =
                autoPowerOptions.FirstOrDefault(option => option.Value == autoPower) ?? autoPowerOptions[^1];

            var themes = BuildThemeOptions();
            ThemeCombo.ItemsSource = themes;
            ThemeCombo.SelectedItem =
                themes.FirstOrDefault(option => option.Value == theme) ?? themes[0];

            var backdrops = BuildBackdropOptions();
            BackdropCombo.ItemsSource = backdrops;
            BackdropCombo.SelectedItem =
                backdrops.FirstOrDefault(option => option.Value == backdrop) ?? backdrops[0];

            var refreshes = BuildRefreshOptions();
            AutoRefreshCombo.ItemsSource = refreshes;
            AutoRefreshCombo.SelectedItem =
                refreshes.FirstOrDefault(option => option.Minutes == refresh) ?? refreshes[0];

            var trayClicks = BuildTrayClickOptions();
            TrayClickCombo.ItemsSource = trayClicks;
            TrayClickCombo.SelectedItem =
                trayClicks.FirstOrDefault(option => option.Value == trayClick) ?? trayClicks[0];

            var languages = BuildLanguageOptions();
            LanguageCombo.ItemsSource = languages;
            var currentLanguage = language ?? _settings.Language;
            LanguageCombo.SelectedItem =
                languages.FirstOrDefault(option => option.Value == currentLanguage) ?? languages[0];
        }
        finally
        {
            _suppressSettingsSave = false;
        }
    }

    private void LoadSettingsIntoUi()
    {
        _suppressSettingsSave = true;
        try
        {
            LaunchOnStartupToggle.IsOn = StartupRegistration.IsEnabled();
            _settings.LaunchOnStartup = LaunchOnStartupToggle.IsOn;
            StartMinimizedToggle.IsOn = _settings.StartMinimized;
            RefreshOnStartToggle.IsOn = _settings.RefreshOnStart;
            RememberWindowToggle.IsOn = _settings.RememberWindow;

            SelectOption(ThemeCombo, BuildThemeOptions(), _settings.Theme);
            SelectOption(BackdropCombo, BuildBackdropOptions(), _settings.Backdrop);
            TrayShowStatusToggle.IsOn = _settings.TrayShowStatus;
            SelectOption(TrayClickCombo, BuildTrayClickOptions(), _settings.TrayClickAction);
            SelectOption(AutoRefreshCombo, BuildRefreshOptions(), _settings.AutoRefreshMinutes);
            AutoReconnectToggle.IsOn = _settings.AutoReconnect;

            DeviceNameBox.Text = _settings.DeviceName;
            DeviceAddressBox.Text = _settings.DeviceAddress ?? string.Empty;
            CloseBehaviorRadio.SelectedIndex = _settings.CloseToTray ? 1 : 0;
            UpdateLastSectionText();

            UpdateNoiseControls();
            ListeningModeRadio.SelectedIndex = 0;
        }
        finally
        {
            _suppressSettingsSave = false;
        }

        // Keep the Run value pointing at the current executable after a move.
        if (_settings.LaunchOnStartup
            && !StartupRegistration.IsEnabled()
            && !StartupRegistration.TrySet(true, out var error))
        {
            ShowStatus(
                InfoBarSeverity.Warning,
                Localization.Text("status.startup.updatefailed"),
                error ?? Localization.Text("status.startup.registry"));
        }
    }

    private static void SelectOption<T>(ComboBox combo, T[] options, object value)
        where T : class
    {
        var selected = options.FirstOrDefault(option =>
            option switch
            {
                ThemeOption theme => theme.Value.Equals(value),
                BackdropOption backdrop => backdrop.Value.Equals(value),
                TrayClickOption trayClick => trayClick.Value.Equals(value),
                RefreshOption refresh => refresh.Minutes.Equals(value),
                _ => false,
            });
        combo.SelectedItem = selected ?? options[0];
    }

    private void UpdateLastSectionText()
    {
        LastSectionText.Text = Localization.Format(
            "settings.lastsection",
            SectionTitle(_settings.LastSection));
    }

    private static string SectionTitle(string tag)
    {
        return Localization.Text(tag switch
        {
            "noise" => "nav.noise",
            "eq" => "nav.eq",
            "listening" => "nav.listening",
            "multipoint" => "nav.multipoint",
            "autopower" => "nav.autopower",
            "scenes" => "nav.scenes",
            "settings" => "nav.settings",
            _ => "nav.overview",
        });
    }

    private void ApplyLanguage()
    {
        Loc.RefreshAll();
        RebuildOptionLists();
        RefreshSceneList();
        UpdateLastSectionText();
        RenderOverview();
        RenderStates();
        RefreshTrayTooltip();
        // Status messages are built when an action runs, so drop any stale one
        // instead of leaving text in the previous language.
        StatusBar.IsOpen = false;
    }

    /// <summary>
    /// Re-renders the read-only state labels in the current language. Editable
    /// controls are left alone so a language change does not discard unsaved
    /// selections.
    /// </summary>
    private void RenderStates()
    {
        NoiseCurrentText.Text = _lastNoise is { } noise
            ? FormatNoiseState(noise)
            : Localization.Text("common.current.mode");

        if (_lastEq is { } eq)
        {
            EqCurrentText.Text = Localization.Format(
                "eq.state",
                eq.Preset,
                string.Join(',', eq.Bands));
            ListeningEqText.Text = Localization.Format("eq.current.value", eq.Preset);
        }
        else
        {
            EqCurrentText.Text = Localization.Text("common.current.eq");
            ListeningEqText.Text = Localization.Text("common.current.eq");
        }

        if (_lastListening is { } listening)
        {
            var roomText = listening.Mode == TandemListeningMode.BackgroundMusic
                ? Localization.Format("listening.room.suffix", RoomSizeName(listening.RoomSize))
                : string.Empty;
            ListeningCurrentText.Text = Localization.Format(
                "listening.current.value",
                ListeningModeName(listening.Mode) + roomText);
        }
        else
        {
            ListeningCurrentText.Text = Localization.Text("common.current.mode");
        }

        MultipointStateText.Text = _lastMultipoint is { } multipoint
            ? Localization.Text(multipoint ? "multipoint.on" : "multipoint.off")
            : Localization.Text("common.current.state");

        AutoPowerStateText.Text = _lastAutoPower is { } autoPower
            ? Localization.Format("autopower.current.value", AutoPowerName(autoPower.First))
            : Localization.Text("common.current.state");
    }

    private void RefreshSceneList()
    {
        SceneListPanel.Children.Clear();
        NoScenesText.Visibility = _settings.Scenes.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        foreach (var scene in _settings.Scenes.ToList())
        {
            SceneListPanel.Children.Add(BuildSceneRow(scene));
        }
    }

    private FrameworkElement BuildSceneRow(TandemSceneDefinition scene)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(new TextBlock { Text = scene.Name, FontWeight = FontWeights.SemiBold });
        text.Children.Add(new TextBlock
        {
            Text = DescribeScene(scene),
            TextWrapping = TextWrapping.Wrap,
            Foreground = LookupBrush("TextFillColorSecondaryBrush"),
        });
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var apply = new Button { Content = Localization.Text("scenes.apply"), Tag = scene.Id };
        apply.Click += ApplyScene_Click;
        Grid.SetColumn(apply, 1);
        grid.Children.Add(apply);

        var delete = new Button { Content = Localization.Text("scenes.delete"), Tag = scene.Id };
        delete.Click += DeleteScene_Click;
        Grid.SetColumn(delete, 2);
        grid.Children.Add(delete);

        return grid;
    }

    private static Brush? LookupBrush(string key)
    {
        return Application.Current.Resources.TryGetValue(key, out var value)
            ? value as Brush
            : null;
    }

    private static string DescribeScene(TandemSceneDefinition scene)
    {
        var parts = new List<string>();
        if (scene.NcAsm is { } nc)
        {
            parts.Add(!nc.Enabled
                ? Localization.Text("scene.noise.off")
                : nc.Mode == TandemNcAsmMode.Asm
                    ? Localization.Format("scene.asm", nc.Level)
                    : Localization.Text("scene.nc"));
        }

        if (scene.Eq is { } eq)
        {
            parts.Add(Localization.Format("scene.eq", eq.Preset));
        }

        if (scene.Listening is { } listening)
        {
            parts.Add(listening.Mode switch
            {
                TandemListeningMode.BackgroundMusic =>
                    Localization.Format("scene.listening.bgm", RoomSizeName(listening.RoomSize)),
                TandemListeningMode.Cinema => Localization.Text("scene.listening.cinema"),
                _ => Localization.Text("scene.listening.standard"),
            });
        }

        return string.Join(" · ", parts);
    }

    private void CaptureScene_Click(object sender, RoutedEventArgs e)
    {
        var name = SceneNameBox.Text.Trim();
        if (name.Length == 0)
        {
            ShowStatus(
                InfoBarSeverity.Warning,
                Localization.Text("status.scene.savefailed"),
                Localization.Text("status.scene.noname"));
            return;
        }

        if (_settings.Scenes.Count >= 50)
        {
            ShowStatus(
                InfoBarSeverity.Warning,
                Localization.Text("status.scene.savefailed"),
                Localization.Text("status.scene.limit"));
            return;
        }

        if (_settings.Scenes.Any(scene => string.Equals(scene.Name, name, StringComparison.Ordinal)))
        {
            ShowStatus(
                InfoBarSeverity.Warning,
                Localization.Text("status.scene.savefailed"),
                Localization.Text("status.scene.duplicate"));
            return;
        }

        var enabled = NoiseModeRadio.SelectedIndex != 2;
        var mode = NoiseModeRadio.SelectedIndex == 1 ? TandemNcAsmMode.Asm : TandemNcAsmMode.Nc;
        var level = (byte)Math.Clamp(Math.Round(AmbientLevelSlider.Value), 1, 20);
        var sensitivity = (NoiseAdaptiveSensitivityCombo.SelectedItem as NoiseSensitivityOption)
            ?.Value ?? TandemNoiseAdaptiveSensitivity.Standard;
        var preset = EqPresetCombo.SelectedItem is TandemEqPresetId selected
            ? selected
            : TandemEqPresetId.UserSetting1;
        var bands = _eqSliders
            .Select(slider => (int)Math.Clamp(Math.Round(slider.Value), 0, 255))
            .ToArray();
        var listeningMode = ListeningModeRadio.SelectedIndex switch
        {
            1 => TandemListeningMode.BackgroundMusic,
            2 => TandemListeningMode.Cinema,
            _ => TandemListeningMode.Standard,
        };
        var room = BgmRoomCombo.SelectedItem is BgmRoomOption option
            ? option.Value
            : TandemRoomSize.Middle;

        var scene = new TandemSceneDefinition(
            Guid.NewGuid().ToString("N")[..8],
            name,
            new TandemSceneNcAsm(
                mode,
                enabled,
                level,
                VoiceFocusToggle.IsOn,
                NoiseAdaptiveToggle.IsOn,
                sensitivity),
            new TandemSceneEq(
                preset,
                preset == TandemEqPresetId.Custom ? bands : null),
            new TandemSceneListening(listeningMode, room));

        _settings.Scenes.Add(scene);
        PersistSettings();
        RefreshSceneList();
        SceneNameBox.Text = string.Empty;
        ShowStatus(
            InfoBarSeverity.Success,
            Localization.Text("status.scene.saved"),
            Localization.Format("status.scene.saved.body", name));
    }

    private async void ApplyScene_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id })
        {
            return;
        }

        var scene = _settings.Scenes.FirstOrDefault(candidate => candidate.Id == id);
        if (scene is null)
        {
            return;
        }

        await RunBusyAsync(
            Localization.Format("status.scene.busy", scene.Name),
            async () =>
        {
            var report = await RunSessionAsync(new ProbeRequest
            {
                ApplyScene = true,
                Scene = scene,
            });

            if (report.Scene is not { } change)
            {
                ShowStatus(
                    InfoBarSeverity.Error,
                    Localization.Text("status.scene.failed"),
                    report.Message ?? report.Status);
                return;
            }

            if (report.NcAsm?.After is { } ncAsm)
            {
                UpdateNoise(ncAsm);
            }

            if (report.Eq?.After is { } eq)
            {
                UpdateEq(eq);
            }

            if (report.ListeningMode?.After is { } listeningMode)
            {
                UpdateListening(listeningMode);
            }

            var summary = string.Join(
                Localization.Text("common.list.separator"),
                change.Steps.Select(step =>
                    $"{SceneStepName(step.Name)}：{(step.Verified
                        ? Localization.Text("status.scene.verified")
                        : Localization.Text("status.scene.unverified"))}"));
            ShowStatus(
                change.Verified ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
                Localization.Text(change.Verified ? "status.scene.applied" : "status.scene.partial"),
                summary);
        });
    }

    private static string SceneStepName(string id)
    {
        return Localization.Text(id switch
        {
            "ncasm" => "tray.noise",
            "eq" => "nav.eq",
            _ => "nav.listening",
        });
    }

    private void DeleteScene_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id })
        {
            return;
        }

        var scene = _settings.Scenes.FirstOrDefault(candidate => candidate.Id == id);
        if (scene is null)
        {
            return;
        }

        _settings.Scenes.Remove(scene);
        PersistSettings();
        RefreshSceneList();
        ShowStatus(
            InfoBarSeverity.Informational,
            Localization.Text("status.scene.deleted"),
            Localization.Format("status.scene.deleted.body", scene.Name));
    }

    private void SettingsToggle_Changed(object sender, RoutedEventArgs e)
    {
        SaveSettingsFromUi();
    }

    private void SettingsSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        SaveSettingsFromUi();
    }

    private void SettingsText_LostFocus(object sender, RoutedEventArgs e)
    {
        SaveSettingsFromUi();
    }

    private void SaveSettingsFromUi()
    {
        if (_suppressSettingsSave)
        {
            return;
        }

        SaveStartupPreference();

        _settings.StartMinimized = StartMinimizedToggle.IsOn;
        _settings.RefreshOnStart = RefreshOnStartToggle.IsOn;
        _settings.RememberWindow = RememberWindowToggle.IsOn;
        if (ThemeCombo.SelectedItem is ThemeOption theme)
        {
            _settings.Theme = theme.Value;
        }

        if (BackdropCombo.SelectedItem is BackdropOption backdrop)
        {
            _settings.Backdrop = backdrop.Value;
        }

        _settings.TrayShowStatus = TrayShowStatusToggle.IsOn;
        if (TrayClickCombo.SelectedItem is TrayClickOption clickAction)
        {
            _settings.TrayClickAction = clickAction.Value;
        }

        if (AutoRefreshCombo.SelectedItem is RefreshOption refresh)
        {
            _settings.AutoRefreshMinutes = refresh.Minutes;
        }

        _settings.AutoReconnect = AutoReconnectToggle.IsOn;
        _settings.CloseToTray = CloseBehaviorRadio.SelectedIndex == 1;

        var languageChanged = false;
        if (LanguageCombo.SelectedItem is LanguageOption language
            && !string.Equals(language.Value, _settings.Language, StringComparison.Ordinal))
        {
            _settings.Language = language.Value;
            languageChanged = true;
        }

        var name = DeviceNameBox.Text.Trim();
        _settings.DeviceName = name.Length == 0 ? UserSettings.DefaultDeviceName : name;
        var address = DeviceAddressBox.Text.Trim();
        _settings.DeviceAddress = address.Length == 0 ? null : address;

        ApplyTheme();
        ApplyBackdrop();
        _trayIcon?.SetClickAction(_settings.TrayClickAction);
        RefreshTrayTooltip();
        StartRefreshTimer();
        PersistSettings();

        if (languageChanged)
        {
            Localization.SetLanguage(_settings.Language);
        }
    }

    private void SaveStartupPreference()
    {
        var desired = LaunchOnStartupToggle.IsOn;
        if (desired == _settings.LaunchOnStartup && desired == StartupRegistration.IsEnabled())
        {
            return;
        }

        if (StartupRegistration.TrySet(desired, out var error))
        {
            _settings.LaunchOnStartup = desired;
            return;
        }

        _suppressSettingsSave = true;
        LaunchOnStartupToggle.IsOn = !desired;
        _suppressSettingsSave = false;
        _settings.LaunchOnStartup = LaunchOnStartupToggle.IsOn;
        ShowStatus(
            InfoBarSeverity.Warning,
            Localization.Text("status.startup.failed"),
            error ?? Localization.Text("status.startup.registry"));
    }

    private void PersistSettings()
    {
        if (!_settingsStore.TrySave(_settings, out var error) && !string.IsNullOrEmpty(error))
        {
            ShowStatus(
                InfoBarSeverity.Warning,
                Localization.Text("status.settings.unsaved"),
                error);
        }
    }

    private void ApplyTheme()
    {
        if (Content is FrameworkElement root)
        {
            root.RequestedTheme = _settings.Theme switch
            {
                AppTheme.Light => ElementTheme.Light,
                AppTheme.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };
        }

        UpdateTitleBarColors();
    }

    /// <summary>
    /// Paints the title bar and its caption buttons with an opaque colour that
    /// matches the app theme. Transparent caption backgrounds are rendered as
    /// white by the system when the non-client area is not extended, which made
    /// the close/minimise/maximise glyphs invisible.
    /// </summary>
    private void UpdateTitleBarColors()
    {
        try
        {
            var titleBar = AppWindow.TitleBar;
            if (titleBar is null)
            {
                return;
            }

            var dark = IsDarkTheme();
            var background = dark
                ? Color.FromArgb(0xFF, 0x20, 0x20, 0x20)
                : Color.FromArgb(0xFF, 0xF3, 0xF3, 0xF3);
            var foreground = dark ? Colors.White : Colors.Black;
            var inactiveForeground = dark
                ? Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)
                : Color.FromArgb(0x99, 0, 0, 0);

            titleBar.BackgroundColor = background;
            titleBar.InactiveBackgroundColor = background;
            titleBar.ForegroundColor = foreground;
            titleBar.InactiveForegroundColor = inactiveForeground;

            titleBar.ButtonBackgroundColor = background;
            titleBar.ButtonInactiveBackgroundColor = background;
            titleBar.ButtonForegroundColor = foreground;
            titleBar.ButtonInactiveForegroundColor = inactiveForeground;
            titleBar.ButtonHoverBackgroundColor = dark
                ? Color.FromArgb(0xFF, 0x2F, 0x2F, 0x2F)
                : Color.FromArgb(0xFF, 0xE3, 0xE3, 0xE3);
            titleBar.ButtonHoverForegroundColor = foreground;
            titleBar.ButtonPressedBackgroundColor = dark
                ? Color.FromArgb(0xFF, 0x3A, 0x3A, 0x3A)
                : Color.FromArgb(0xFF, 0xD6, 0xD6, 0xD6);
            titleBar.ButtonPressedForegroundColor = foreground;
        }
        catch
        {
            // Title-bar colors are cosmetic; the app still works without them.
        }
    }

    private bool IsDarkTheme()
    {
        return _settings.Theme == AppTheme.Dark
            || (_settings.Theme == AppTheme.System
                && Application.Current.RequestedTheme == ApplicationTheme.Dark);
    }

    private void ApplyBackdrop()
    {
        try
        {
            switch (_settings.Backdrop)
            {
                case AppBackdrop.Acrylic:
                    SystemBackdrop = new DesktopAcrylicBackdrop();
                    RootGrid.Background = null;
                    break;

                case AppBackdrop.Solid:
                    SystemBackdrop = null;
                    RootGrid.Background = new SolidColorBrush(
                        IsDarkTheme()
                            ? Color.FromArgb(0xFF, 0x20, 0x20, 0x20)
                            : Color.FromArgb(0xFF, 0xF3, 0xF3, 0xF3));
                    break;

                default:
                    SystemBackdrop = new MicaBackdrop();
                    RootGrid.Background = null;
                    break;
            }
        }
        catch
        {
            SystemBackdrop = null;
            RootGrid.Background = new SolidColorBrush(
                IsDarkTheme()
                    ? Color.FromArgb(0xFF, 0x20, 0x20, 0x20)
                    : Color.FromArgb(0xFF, 0xF3, 0xF3, 0xF3));
        }
    }

    private void ApplyWindowPlacement()
    {
        if (!_settings.RememberWindow)
        {
            return;
        }

        try
        {
            var scale = GetUiScale();
            var areas = new List<ScreenRect>();
            foreach (var area in GetWorkAreas())
            {
                areas.Add(Scale(area, 1 / scale));
            }

            var bounds = WindowPlacement.Resolve(
                _settings.WindowLeft,
                _settings.WindowTop,
                _settings.WindowWidth,
                _settings.WindowHeight,
                areas);
            var physical = Scale(bounds, scale);
            AppWindow.MoveAndResize(new RectInt32(
                physical.Left,
                physical.Top,
                physical.Width,
                physical.Height));
        }
        catch
        {
            // Keep the system default placement if anything is unavailable.
        }
    }

    /// <summary>
    /// Applies the saved window placement. Call after the window has been
    /// activated; applying it earlier is overridden by the first show.
    /// </summary>
    public void ApplyStartupPlacement()
    {
        DispatcherQueue.TryEnqueue(ApplyWindowPlacement);
    }

    private double GetUiScale()
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var dpi = GetDpiForWindow(hwnd);
            if (dpi > 0)
            {
                return dpi / 96.0;
            }
        }
        catch
        {
            // Fall back to the XAML scale below.
        }

        var scale = RootGrid.XamlRoot?.RasterizationScale ?? 1.0;
        return scale > 0.1 ? scale : 1.0;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    private static ScreenRect Scale(ScreenRect rect, double scale)
    {
        return new ScreenRect(
            (int)Math.Round(rect.Left * scale),
            (int)Math.Round(rect.Top * scale),
            (int)Math.Round(rect.Width * scale),
            (int)Math.Round(rect.Height * scale));
    }

    private static WindowBounds Scale(WindowBounds bounds, double scale)
    {
        return new WindowBounds(
            (int)Math.Round(bounds.Left * scale),
            (int)Math.Round(bounds.Top * scale),
            (int)Math.Round(bounds.Width * scale),
            (int)Math.Round(bounds.Height * scale));
    }

    private IReadOnlyList<ScreenRect> GetWorkAreas()
    {
        var areas = new List<ScreenRect>();
        var primary = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        if (primary is not null)
        {
            areas.Add(ToScreenRect(primary.WorkArea));
        }

        // DisplayArea.FindAll() comes from WinRT; enumerate by index because its
        // projected IReadOnlyList can fail to cast to IEnumerable.
        var all = DisplayArea.FindAll();
        for (var index = 0; index < all.Count; index++)
        {
            var rect = ToScreenRect(all[index].WorkArea);
            if (!areas.Contains(rect))
            {
                areas.Add(rect);
            }
        }

        return areas;
    }

    private static ScreenRect ToScreenRect(RectInt32 rect)
    {
        return new ScreenRect(rect.X, rect.Y, rect.Width, rect.Height);
    }

    private void SaveWindowPlacement()
    {
        if (!_settings.RememberWindow)
        {
            return;
        }

        try
        {
            var position = AppWindow.Position;
            var size = AppWindow.Size;
            if (size.Width <= 0 || size.Height <= 0)
            {
                return;
            }

            var scale = GetUiScale();
            _settings.WindowLeft = (int)Math.Round(position.X / scale);
            _settings.WindowTop = (int)Math.Round(position.Y / scale);
            _settings.WindowWidth = (int)Math.Round(size.Width / scale);
            _settings.WindowHeight = (int)Math.Round(size.Height / scale);
        }
        catch
        {
            // Placement persistence is best effort.
        }
    }

    private void StartRefreshTimer()
    {
        if (_refreshTimer is not null)
        {
            _refreshTimer.Stop();
            _refreshTimer.Tick -= RefreshTimer_Tick;
            _refreshTimer = null;
        }

        if (_settings.AutoRefreshMinutes <= 0)
        {
            return;
        }

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(_settings.AutoRefreshMinutes),
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _refreshTimer.Start();
    }

    private async void RefreshTimer_Tick(object? sender, object e)
    {
        if (_busy)
        {
            return;
        }

        await RefreshAsync(silent: true);
    }

    private void BuildEqSliders()
    {
        for (var index = 0; index < 10; index++)
        {
            var row = new Grid
            {
                ColumnSpacing = 12,
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock
            {
                Text = Localization.Format("eq.band", index + 1),
                Width = 72,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var slider = new Slider
            {
                Minimum = 0,
                Maximum = 20,
                StepFrequency = 1,
                Value = 0,
            };
            var value = new TextBlock
            {
                Text = "0",
                Width = 36,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };

            Grid.SetColumn(label, 0);
            Grid.SetColumn(slider, 1);
            Grid.SetColumn(value, 2);
            row.Children.Add(label);
            row.Children.Add(slider);
            row.Children.Add(value);

            slider.ValueChanged += (_, args) =>
            {
                value.Text = ((int)Math.Round(args.NewValue)).ToString();
            };

            _eqSliders.Add(slider);
            EqBandsPanel.Children.Add(row);
        }
    }

    private void Navigation_SelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is string tag)
        {
            ShowSection(tag);
        }
    }

    private void ShowSection(string tag)
    {
        _currentSection = tag;
        _settings.LastSection = tag;
        OverviewSection.Visibility = tag == "overview" ? Visibility.Visible : Visibility.Collapsed;
        NoiseSection.Visibility = tag == "noise" ? Visibility.Visible : Visibility.Collapsed;
        EqSection.Visibility = tag == "eq" ? Visibility.Visible : Visibility.Collapsed;
        ListeningSection.Visibility = tag == "listening" ? Visibility.Visible : Visibility.Collapsed;
        MultipointSection.Visibility = tag == "multipoint" ? Visibility.Visible : Visibility.Collapsed;
        AutoPowerSection.Visibility = tag == "autopower" ? Visibility.Visible : Visibility.Collapsed;
        ScenesSection.Visibility = tag == "scenes" ? Visibility.Visible : Visibility.Collapsed;
        SettingsSection.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;
        AboutSection.Visibility = tag == "about" ? Visibility.Visible : Visibility.Collapsed;

        if (tag == "settings")
        {
            UpdateLastSectionText();
        }

        if (tag is "overview" or "listening")
        {
            _ = EnsureIndicatorsAsync();
        }
    }

    /// <summary>
    /// Loads the codec and DSEE indicators once when the user first reaches a
    /// page that shows them, without waiting for a full snapshot refresh.
    /// </summary>
    private async Task EnsureIndicatorsAsync()
    {
        if (_busy || _lastReport is null)
        {
            return;
        }

        var current = _lastReport.Snapshot;
        if (current is not null && current.Codec is not null && current.Dsee is not null)
        {
            return;
        }

        await RunBusyAsync(
            Localization.Text("status.refreshing"),
            async () =>
            {
                var report = await RunSessionAsync(new ProbeRequest
                {
                    QueryIndicators = true,
                });
                if (report.Snapshot is not { } loaded)
                {
                    return;
                }

                if (_lastReport.Handshake.SupportFunctions.Count == 0)
                {
                    _lastReport.Handshake = report.Handshake;
                }

                if (report.Status == "ok")
                {
                    _lastReport.Status = report.Status;
                    _lastReport.Message = report.Message;
                    _lastUpdatedAt = DateTime.Now;
                }

                _lastReport.Snapshot ??= new TandemDeviceSnapshot();
                _lastReport.Snapshot.Codec = loaded.Codec ?? _lastReport.Snapshot.Codec;
                _lastReport.Snapshot.Dsee = loaded.Dsee ?? _lastReport.Snapshot.Dsee;
                RenderOverview();
            },
            showProgress: false);
    }

    private void NavigateTo(string tag)
    {
        var item = Nav.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(candidate => string.Equals(candidate.Tag as string, tag, StringComparison.Ordinal));
        if (item is not null)
        {
            Nav.SelectedItem = item;
        }

        ShowSection(tag);
    }

    private void InitializeTrayIcon()
    {
        try
        {
            _trayIcon = new TrayIcon
            {
                ClickAction = _settings.TrayClickAction,
            };
            if (!_trayIcon.IsAvailable)
            {
                _trayIcon.Dispose();
                _trayIcon = null;
                return;
            }

            _trayIcon.OverviewRequested += () => ShowFromTray("overview");
            _trayIcon.NoiseRequested += () => ShowFromTray("noise");
            _trayIcon.EqRequested += () => ShowFromTray("eq");
            _trayIcon.ListeningRequested += () => ShowFromTray("listening");
            _trayIcon.ExitRequested += ExitFromTray;
            RefreshTrayTooltip();
        }
        catch
        {
            _trayIcon?.Dispose();
            _trayIcon = null;
        }
    }

    private void ShowFromTray(string tag)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            AppWindow.Show();
            Activate();
            NavigateTo(tag);
            _tandemHost.KeepAlive = true;
            _ = PrewarmAsync();
        });
    }

    private async Task PrewarmAsync()
    {
        try
        {
            await _tandemHost.EnsureConnectedAsync(
                ApplyTargetDevice(new ProbeRequest
                {
                    CollectServices = false,
                    CollectDiagnostics = false,
                }));
        }
        catch
        {
            // Prewarming is best effort; the next action reconnects.
        }
    }

    private void ExitFromTray()
    {
        _allowClose = true;
        PersistWindowState();
        _trayIcon?.Dispose();
        _trayIcon = null;
        _ = _tandemHost.DisposeAsync();
        Close();
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        PersistWindowState();

        if (_allowClose)
        {
            return;
        }

        if (_settings.CloseToTray && _trayIcon?.IsAvailable == true)
        {
            args.Cancel = true;
            AppWindow.Hide();
            _tandemHost.KeepAlive = false;
            _ = _tandemHost.ReleaseAsync();
            return;
        }

        _trayIcon?.Dispose();
        _trayIcon = null;
        _ = _tandemHost.DisposeAsync();
    }

    private void PersistWindowState()
    {
        _settings.LastSection = _currentSection;
        SaveWindowPlacement();
        PersistSettings();
        UpdateLastSectionText();
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        Localization.Changed -= OnLocalizationChanged;
        _refreshTimer?.Stop();
        _trayIcon?.Dispose();
        _trayIcon = null;
        _ = _tandemHost.DisposeAsync();
    }

    private void OnLocalizationChanged()
    {
        // Rebuilding combo sources from inside a SelectionChanged handler is not
        // safe, so defer the visual refresh to the next dispatch.
        DispatcherQueue.TryEnqueue(ApplyLanguage);
    }

    private void RefreshTrayTooltip()
    {
        _trayIcon?.UpdateTooltip(BuildTrayTooltip());
    }

    private string BuildTrayTooltip()
    {
        var device = _lastReport?.Handshake.Model ?? _settings.DeviceName;
        if (!_settings.TrayShowStatus)
        {
            return Localization.Format("tray.tooltip.plain", device);
        }

        if (_lastReport?.Status != "ok")
        {
            return Localization.Format("tray.tooltip.disconnected", device);
        }

        var mode = _lastNoise switch
        {
            null => Localization.Text("tray.mode.unknown"),
            { OnOff: TandemNcAsmOnOffValue.Off } => Localization.Text("noise.mode.off"),
            { Mode: TandemNcNcssAsmMode.Nc } => Localization.Text("noise.mode.nc"),
            _ => Localization.Text("noise.mode.asm"),
        };

        return _lastReport.Snapshot?.BatteryPercent is { } battery
            ? Localization.Format("tray.tooltip.status", device, mode, $"{battery}%")
            : Localization.Format("tray.tooltip.status.nobattery", device, mode);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync(bool silent = false)
    {
        await RunBusyAsync(
            Localization.Text(silent ? "status.auto.refreshing" : "status.refreshing"),
            async () =>
            {
                var report = await RunWithRetryAsync(new ProbeRequest
                {
                    QuerySnapshot = true,
                    QueryIndicators = _currentSection is "overview" or "listening",
                });

                ApplyReport(report);
                if (report.Status == "ok")
                {
                    if (!silent)
                    {
                        ShowStatus(
                            InfoBarSeverity.Success,
                            Localization.Text("status.refreshed"),
                            Localization.Text("status.refreshed.body"));
                    }
                }
                else
                {
                    ShowStatus(
                        InfoBarSeverity.Error,
                        Localization.Text("status.readfailed"),
                        DescribeFailure(report));
                }
            },
            showProgress: !silent);
    }

    private string DescribeFailure(TandemSessionReport report)
    {
        if (report.Status == "device_unavailable")
        {
            return Localization.Text("status.device.unavailable");
        }

        if (report.Status == "not_found")
        {
            return Localization.Format("status.notfound", _settings.DeviceName);
        }

        return report.Message ?? report.Status;
    }

    private Task<TandemSessionReport> RunSessionAsync(ProbeRequest request)
    {
        return _tandemHost.ExecuteAsync(ApplyTargetDevice(request));
    }

    private async Task<TandemSessionReport> RunWithRetryAsync(ProbeRequest request)
    {
        var report = await RunSessionAsync(request);
        if (!_settings.AutoReconnect)
        {
            return report;
        }

        int[] delays = [2, 5, 15];
        for (var attempt = 0;
            attempt < delays.Length && SessionRetryPolicy.ShouldRetry(report.Status);
            attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(delays[attempt]));
            report = await RunSessionAsync(request);
        }

        return report;
    }

    private ProbeRequest ApplyTargetDevice(ProbeRequest request)
    {
        return request with
        {
            Name = _settings.DeviceName,
            Address = _settings.DeviceAddress,
            CollectServices = false,
            CollectDiagnostics = false,
        };
    }

    private async void ApplyNoise_Click(object sender, RoutedEventArgs e)
    {
        var enabled = NoiseModeRadio.SelectedIndex != 2;
        var mode = NoiseModeRadio.SelectedIndex == 1
            ? TandemNcAsmMode.Asm
            : TandemNcAsmMode.Nc;
        var level = (byte)Math.Clamp(Math.Round(AmbientLevelSlider.Value), 1, 20);
        var sensitivity = (NoiseAdaptiveSensitivityCombo.SelectedItem as NoiseSensitivityOption)
            ?.Value ?? TandemNoiseAdaptiveSensitivity.Standard;

        await RunBusyAsync(Localization.Text("status.noise.busy"), async () =>
        {
            var report = await RunSessionAsync(new ProbeRequest
            {
                ApplyNcAsm = true,
                NcAsmMode = enabled ? mode : null,
                NcAsmEnabled = enabled,
                NcAsmLevel = level,
                NcAsmVoiceFocus = VoiceFocusToggle.IsOn,
                NcAsmNoiseAdaptive = NoiseAdaptiveToggle.IsOn,
                NcAsmSensitivity = sensitivity,
            });

            if (report.NcAsm?.After is { } after)
            {
                UpdateNoise(after);
                ShowStatus(
                    report.NcAsm.Verified ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
                    Localization.Text(report.NcAsm.Verified ? "status.applied" : "status.unverified"),
                    FormatNoiseState(after));
            }
            else
            {
                ShowStatus(
                    InfoBarSeverity.Error,
                    Localization.Text("status.unverified"),
                    Localization.Text("status.noise.noread"));
            }
        });
    }

    private async void ApplyEq_Click(object sender, RoutedEventArgs e)
    {
        var preset = EqPresetCombo.SelectedItem is TandemEqPresetId selected
            ? selected
            : TandemEqPresetId.UserSetting1;
        var bands = _eqSliders
            .Select(slider => (byte)Math.Clamp(Math.Round(slider.Value), 0, 255))
            .ToArray();

        await RunBusyAsync(Localization.Text("status.eq.busy"), async () =>
        {
            var report = await RunSessionAsync(new ProbeRequest
            {
                ApplyEq = true,
                EqPreset = preset,
                EqBands = preset == TandemEqPresetId.Custom ? bands : null,
            });

            if (report.Eq?.After is { } after)
            {
                UpdateEq(after);
                var suffix = report.Eq.UsedPresetDefault
                    ? Localization.Text("eq.preset.default")
                    : string.Empty;
                ShowStatus(
                    report.Eq.Verified ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
                    Localization.Text(report.Eq.Verified ? "status.applied" : "status.unverified"),
                    Localization.Format("eq.applied", after.Preset, suffix));
            }
            else
            {
                ShowStatus(
                    InfoBarSeverity.Error,
                    Localization.Text("status.unverified"),
                    Localization.Text("status.eq.noread"));
            }
        });
    }

    private void ListeningModeRadio_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BgmRoomCombo is not null)
        {
            BgmRoomCombo.IsEnabled = !_busy
                && ListeningModeRadio.SelectedIndex == 1;
        }
    }

    private async void ApplyListening_Click(object sender, RoutedEventArgs e)
    {
        var mode = ListeningModeRadio.SelectedIndex switch
        {
            1 => TandemListeningMode.BackgroundMusic,
            2 => TandemListeningMode.Cinema,
            _ => TandemListeningMode.Standard,
        };
        var room = BgmRoomCombo.SelectedItem is BgmRoomOption option
            ? option.Value
            : TandemRoomSize.Middle;

        await RunBusyAsync(Localization.Text("status.listening.busy"), async () =>
        {
            var report = await RunSessionAsync(new ProbeRequest
            {
                ApplyListeningMode = true,
                ListeningMode = mode,
                BgmRoomSize = room,
            });

            var change = report.ListeningMode;
            if (change?.After is { } after)
            {
                UpdateListening(after);
            }

            if (change?.Verified == true)
            {
                var roomSuffix = mode == TandemListeningMode.BackgroundMusic
                    ? Localization.Format("status.listening.room", RoomSizeName(room))
                    : string.Empty;
                ShowStatus(
                    InfoBarSeverity.Success,
                    Localization.Text("status.applied"),
                    Localization.Format(
                        "status.listening.applied",
                        ListeningModeName(change.After?.Mode ?? mode),
                        roomSuffix));
            }
            else
            {
                ShowStatus(
                    InfoBarSeverity.Warning,
                    Localization.Text("status.unverified"),
                    Localization.Format(
                        "status.listening.current",
                        ListeningModeName(change?.After?.Mode)));
            }
        });
    }

    private async void DseeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_updatingDsee)
        {
            return;
        }

        var auto = DseeToggle.IsOn;
        var previous = _lastReport?.Snapshot?.Dsee;
        var completed = await RunBusyAsync(
            Localization.Text("status.dsee.applying"),
            async () =>
            {
                var report = await RunSessionAsync(new ProbeRequest
                {
                    ApplyDsee = true,
                    DseeAuto = auto,
                });

                if (report.Dsee is not { } change)
                {
                    RestoreDsee(previous);
                    ShowStatus(
                        InfoBarSeverity.Error,
                        Localization.Text("status.unverified"),
                        DescribeFailure(report));
                    return;
                }

                if (change.Status == "unsupported")
                {
                    RestoreDsee(previous);
                    ShowStatus(
                        InfoBarSeverity.Error,
                        Localization.Text("status.dsee.unsupported"),
                        report.Message ?? report.Status);
                    return;
                }

                if (change.After is { } after)
                {
                    _lastReport ??= report;
                    _lastReport.Snapshot ??= new TandemDeviceSnapshot();
                    _lastReport.Snapshot.Dsee = after;

                    RenderDsee();
                    ShowStatus(
                        change.Verified ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
                        Localization.Text(
                            change.Verified ? "status.dsee.applied" : "status.dsee.unverified"),
                        DseeStatusName(after));
                }
                else
                {
                    RestoreDsee(previous);
                    ShowStatus(
                        InfoBarSeverity.Warning,
                        Localization.Text("status.dsee.unverified"),
                        report.Message ?? report.Status);
                }
            });

        if (!completed)
        {
            RestoreDsee(previous);
        }
    }

    private void RestoreDsee(TandemDseeState? previous)
    {
        if (previous is not null && _lastReport?.Snapshot is { } snapshot)
        {
            snapshot.Dsee = previous;
        }

        RenderDsee();
    }

    private async void ApplyMultipoint_Click(object sender, RoutedEventArgs e)
    {
        var enabled = MultipointToggle.IsOn;

        await RunBusyAsync(Localization.Text("status.multipoint.busy"), async () =>
        {
            var report = await RunSessionAsync(new ProbeRequest
            {
                ApplyMultipoint = true,
                MultipointEnabled = enabled,
            });

            var change = report.Multipoint;
            if (change?.After is { } after)
            {
                UpdateMultipoint(after);
            }

            if (change?.Verified == true)
            {
                ShowStatus(
                    InfoBarSeverity.Success,
                    Localization.Text("status.applied"),
                    Localization.Text(enabled ? "status.multipoint.on" : "status.multipoint.off"));
            }
            else
            {
                ShowStatus(
                    InfoBarSeverity.Warning,
                    Localization.Text("status.unverified"),
                    Localization.Format("status.multipoint.refused", FormatOnOff(change?.After)));
            }
        });
    }

    private async void ApplyAutoPower_Click(object sender, RoutedEventArgs e)
    {
        var option = AutoPowerCombo.SelectedItem as AutoPowerOption
            ?? BuildAutoPowerOptions()[^1];

        await RunBusyAsync(Localization.Text("status.autopower.busy"), async () =>
        {
            var report = await RunSessionAsync(new ProbeRequest
            {
                ApplyAutoPowerOff = true,
                AutoPowerOffElement = option.Value,
            });

            var change = report.AutoPowerOff;
            if (change?.After is { } after)
            {
                UpdateAutoPower(after);
            }

            if (change?.Verified == true)
            {
                ShowStatus(
                    InfoBarSeverity.Success,
                    Localization.Text("status.applied"),
                    Localization.Format(
                        "status.autopower.applied",
                        AutoPowerName(change.After?.First ?? option.Value)));
            }
            else
            {
                ShowStatus(
                    InfoBarSeverity.Warning,
                    Localization.Text("status.unverified"),
                    Localization.Format(
                        "status.autopower.current",
                        AutoPowerName(change?.After?.First)));
            }
        });
    }

    private void NoiseModeRadio_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateNoiseControls();
    }

    private void NoiseAdaptiveToggle_Toggled(object sender, RoutedEventArgs e)
    {
        UpdateNoiseControls();
    }

    private void UpdateNoiseControls()
    {
        var ambient = NoiseModeRadio.SelectedIndex == 1;
        AmbientControls.Visibility = ambient ? Visibility.Visible : Visibility.Collapsed;
        if (!ambient)
        {
            return;
        }

        AmbientLevelSlider.IsEnabled = !_busy;
        NoiseAdaptiveToggle.IsEnabled = !_busy;
        NoiseAdaptiveSensitivityCombo.IsEnabled = !_busy && NoiseAdaptiveToggle.IsOn;
        VoiceFocusToggle.IsEnabled = !_busy;
    }

    private void EqPresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EqPresetCombo.SelectedItem is not TandemEqPresetId preset
            || preset == TandemEqPresetId.Custom
            || !_eqPresetBands.TryGetValue(preset, out var bands))
        {
            return;
        }

        ApplyEqBandsToSliders(bands);
    }

    private void AmbientLevelSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        AmbientLevelText.Text = ((int)Math.Round(e.NewValue)).ToString();
    }

    private void ApplyReport(TandemSessionReport report)
    {
        _lastReport = report;
        if (report.Status == "ok")
        {
            _lastUpdatedAt = DateTime.Now;
        }

        RenderOverview();

        if (report.Snapshot is { } snapshot)
        {
            if (snapshot.NcAsm is { } ncAsm)
            {
                UpdateNoise(ncAsm);
            }

            if (snapshot.Eq is { } eq)
            {
                UpdateEq(eq);
            }

            if (snapshot.MultipointEnabled is { } multipoint)
            {
                UpdateMultipoint(multipoint);
            }

            if (snapshot.AutoPowerOff is { } autoPowerOff)
            {
                UpdateAutoPower(autoPowerOff);
            }

            if (snapshot.ListeningMode is { } listeningMode)
            {
                UpdateListening(listeningMode);
            }
        }
        else
        {
            RenderBattery();
        }

        RefreshTrayTooltip();
    }

    private void RenderOverview()
    {
        DeviceNameText.Text = _lastReport?.Handshake.Model ?? UserSettings.DefaultDeviceName;
        FirmwareText.Text = _lastReport?.Handshake.Firmware ?? Localization.Text("overview.unknown");
        ConnectionText.Text = Localization.Text(
            _lastReport?.Status == "ok" ? "overview.connected" : "overview.disconnected");
        RenderBattery();
        RenderCodec();
        RenderDsee();
        LastUpdatedText.Text = _lastUpdatedAt is { } updatedAt
            ? Localization.Format("overview.updated", updatedAt.ToString("HH:mm:ss"))
            : string.Empty;
    }

    private void RenderCodec()
    {
        CodecText.Text = _lastReport?.Snapshot?.Codec switch
        {
            TandemAudioCodec.Sbc => "SBC",
            TandemAudioCodec.Aac => "AAC",
            TandemAudioCodec.Ldac => "LDAC",
            TandemAudioCodec.AptX => "aptX",
            TandemAudioCodec.AptXHd => "aptX HD",
            TandemAudioCodec.Lc3 => "LC3",
            TandemAudioCodec.Unsettled => Localization.Text("codec.unsettled"),
            TandemAudioCodec.Other => Localization.Text("codec.other"),
            _ => Localization.Text("overview.unknown"),
        };
    }

    private void RenderDsee()
    {
        var support = SupportsDsee();
        if (support != true)
        {
            DseeOverviewText.Text = support == false
                ? Localization.Text("dsee.state.unsupported")
                : Localization.Text("overview.unknown");
            DseeBox.Visibility = Visibility.Collapsed;
            return;
        }

        DseeBox.Visibility = Visibility.Visible;
        var state = _lastReport?.Snapshot?.Dsee;
        if (state is null)
        {
            DseeOverviewText.Text = Localization.Text("overview.unknown");
            DseeStatusText.Text = Localization.Text("dsee.state.unknown");
            SetDseeToggle(false);
            DseeToggle.IsEnabled = false;
            return;
        }

        SetDseeToggle(state.Setting == TandemUpscalingSetting.Auto);
        DseeToggle.IsEnabled = !_busy;

        var status = DseeStatusName(state);
        DseeStatusText.Text = status;
        DseeOverviewText.Text = state.Setting == TandemUpscalingSetting.Auto
            && state.EffectStatus == TandemUpscalingEffectStatus.Valid
            ? $"{status} · {DseeTypeName(state.Type)}"
            : status;
    }

    /// <summary>
    /// Programmatic switch updates must not look like a user toggle. The flag is
    /// cleared after the current dispatcher turn so a synchronously raised
    /// Toggled event cannot start a write.
    /// </summary>
    private void SetDseeToggle(bool auto)
    {
        _updatingDsee = true;
        DseeToggle.IsOn = auto;
        DispatcherQueue.TryEnqueue(() => _updatingDsee = false);
    }

    private bool? SupportsDsee()
    {
        var functions = _lastReport?.Handshake.SupportFunctions;
        if (functions is null || functions.Count == 0)
        {
            return null;
        }

        return functions.Any(
            function => function.FunctionType == TandemV2FunctionTypes.UpscalingAutoOff);
    }

    private static string DseeStatusName(TandemDseeState state)
    {
        if (state.Setting == TandemUpscalingSetting.OutOfRange)
        {
            return Localization.Text("dsee.state.unknown");
        }

        if (state.Setting == TandemUpscalingSetting.Off)
        {
            return Localization.Text("dsee.state.off");
        }

        return Localization.Text(
            state.EffectStatus == TandemUpscalingEffectStatus.Valid
                ? "dsee.state.running"
                : "dsee.state.enabled");
    }

    private static string DseeTypeName(TandemUpscalingType type)
    {
        return Localization.Text(type switch
        {
            TandemUpscalingType.DseeHx => "dsee.type.dseehx",
            TandemUpscalingType.Dsee => "dsee.type.dsee",
            TandemUpscalingType.DseeHxAi => "dsee.type.ai",
            TandemUpscalingType.DseeUltimate => "dsee.type.ultimate",
            _ => "dsee.state.unsupported",
        });
    }

    private void RenderBattery()
    {
        BatteryText.Text = _lastReport?.Snapshot?.BatteryPercent is { } battery
            ? $"{battery}%"
            : Localization.Text("overview.unknown");
    }

    private void UpdateNoise(TandemNcAsmState state)
    {
        _lastNoise = state;
        var off = state.OnOff == TandemNcAsmOnOffValue.Off;
        NoiseModeRadio.SelectedIndex = off
            ? 2
            : state.Mode == TandemNcNcssAsmMode.Asm ? 1 : 0;
        AmbientLevelSlider.Value = state.Level;
        AmbientLevelText.Text = state.Level.ToString();
        NoiseAdaptiveToggle.IsOn = state.NoiseAdaptiveOnOff == TandemNcAsmOnOffValue.On;
        VoiceFocusToggle.IsOn = state.AmbientMode == TandemAmbientSoundMode.Voice;
        var sensitivity = BuildNoiseSensitivityOptions().FirstOrDefault(
            item => item.Value == state.Sensitivity);
        if (sensitivity is not null)
        {
            NoiseAdaptiveSensitivityCombo.SelectedItem = sensitivity;
        }

        NoiseCurrentText.Text = FormatNoiseState(state);
        UpdateNoiseControls();
    }

    private static string FormatNoiseState(TandemNcAsmState state)
    {
        if (state.OnOff == TandemNcAsmOnOffValue.Off)
        {
            return Localization.Text("noise.state.off");
        }

        if (state.Mode == TandemNcNcssAsmMode.Nc)
        {
            return Localization.Text("noise.state.nc");
        }

        var adaptive = state.NoiseAdaptiveOnOff == TandemNcAsmOnOffValue.On
            ? Localization.Format("noise.adaptive.on", SensitivityName(state.Sensitivity))
            : Localization.Text("noise.adaptive.off");
        var voice = Localization.Text(
            state.AmbientMode == TandemAmbientSoundMode.Voice ? "toggle.on" : "toggle.off");
        return Localization.Format("noise.state.asm", state.Level, adaptive, voice);
    }

    private static string SensitivityName(TandemNoiseAdaptiveSensitivity sensitivity)
    {
        return Localization.Text(sensitivity switch
        {
            TandemNoiseAdaptiveSensitivity.Standard => "sensitivity.standard",
            TandemNoiseAdaptiveSensitivity.High => "sensitivity.high",
            TandemNoiseAdaptiveSensitivity.Low => "sensitivity.low",
            _ => "overview.unknown",
        });
    }

    private void UpdateEq(TandemEqState state)
    {
        _lastEq = state;
        EqPresetCombo.SelectedItem = state.Preset;
        if (state.Preset != TandemEqPresetId.Custom)
        {
            _eqPresetBands[state.Preset] = state.Bands;
        }

        EqCurrentText.Text = Localization.Format(
            "eq.state",
            state.Preset,
            string.Join(',', state.Bands));
        ListeningEqText.Text = Localization.Format("eq.current.value", state.Preset);
        ApplyEqBandsToSliders(state.Bands);
    }

    private void ApplyEqBandsToSliders(IReadOnlyList<byte> bands)
    {
        for (var index = 0; index < _eqSliders.Count && index < bands.Count; index++)
        {
            _eqSliders[index].Value = bands[index];
        }
    }

    private void UpdateListening(TandemListeningModeState state)
    {
        _lastListening = state;
        ListeningModeRadio.SelectedIndex = state.Mode switch
        {
            TandemListeningMode.BackgroundMusic => 1,
            TandemListeningMode.Cinema => 2,
            _ => 0,
        };

        var rooms = BuildBgmRoomOptions();
        var room = rooms.FirstOrDefault(item => item.Value == state.RoomSize) ?? rooms[1];
        BgmRoomCombo.SelectedItem = room;
        var roomText = state.Mode == TandemListeningMode.BackgroundMusic
            ? Localization.Format("listening.room.suffix", room.Label)
            : string.Empty;
        ListeningCurrentText.Text = Localization.Format(
            "listening.current.value",
            ListeningModeName(state.Mode) + roomText);
    }

    private static string ListeningModeName(TandemListeningMode? mode)
    {
        return Localization.Text(mode switch
        {
            TandemListeningMode.BackgroundMusic => "listening.name.bgm",
            TandemListeningMode.Cinema => "listening.name.cinema",
            TandemListeningMode.Standard => "listening.name.standard",
            _ => "overview.unknown",
        });
    }

    private static string RoomSizeName(TandemRoomSize roomSize)
    {
        return Localization.Text(roomSize switch
        {
            TandemRoomSize.Small => "listening.room.small",
            TandemRoomSize.Middle => "listening.room.middle",
            TandemRoomSize.Large => "listening.room.large",
            _ => "overview.unknown",
        });
    }

    private void UpdateMultipoint(bool enabled)
    {
        _lastMultipoint = enabled;
        MultipointToggle.IsOn = enabled;
        MultipointStateText.Text = Localization.Text(enabled ? "multipoint.on" : "multipoint.off");
    }

    private void UpdateAutoPower(TandemAutoPowerOffState state)
    {
        _lastAutoPower = state;
        var option = BuildAutoPowerOptions().FirstOrDefault(item => item.Value == state.First);
        if (option is not null)
        {
            AutoPowerCombo.SelectedItem = option;
        }

        AutoPowerStateText.Text = Localization.Format("autopower.current.value", AutoPowerName(state.First));
    }

    private static string AutoPowerName(TandemAutoPowerOffElement? element)
    {
        return Localization.Text(element switch
        {
            TandemAutoPowerOffElement.WhenRemovedFromEars => "autopower.removed",
            TandemAutoPowerOffElement.Disable => "autopower.disable",
            _ => "overview.unknown",
        });
    }

    private static string FormatOnOff(bool? value)
    {
        return Localization.Text(value switch
        {
            true => "toggle.on",
            false => "toggle.off",
            null => "overview.unknown",
        });
    }

    private async Task<bool> RunBusyAsync(
        string message,
        Func<Task> action,
        bool showProgress = true)
    {
        if (_busy)
        {
            return false;
        }

        _busy = true;
        SetButtonsEnabled(false);
        if (showProgress)
        {
            ShowStatus(
                InfoBarSeverity.Informational,
                Localization.Text("status.busy"),
                message);
        }

        try
        {
            await action();
            return true;
        }
        catch (Exception ex)
        {
            ShowStatus(
                InfoBarSeverity.Error,
                Localization.Text("status.operationfailed"),
                ex.Message);
            return false;
        }
        finally
        {
            _busy = false;
            SetButtonsEnabled(true);
        }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        RefreshButton.IsEnabled = enabled;
        ApplyNoiseButton.IsEnabled = enabled;
        NoiseModeRadio.IsEnabled = enabled;
        UpdateNoiseControls();
        ApplyEqButton.IsEnabled = enabled;
        ApplyListeningButton.IsEnabled = enabled;
        ListeningModeRadio.IsEnabled = enabled;
        BgmRoomCombo.IsEnabled = enabled && ListeningModeRadio.SelectedIndex == 1;
        ApplyMultipointButton.IsEnabled = enabled;
        ApplyAutoPowerButton.IsEnabled = enabled;
        MultipointToggle.IsEnabled = enabled;
        AutoPowerCombo.IsEnabled = enabled;
        DseeToggle.IsEnabled = enabled
            && DseeBox.Visibility == Visibility.Visible
            && _lastReport?.Snapshot?.Dsee is not null;
    }

    private void ShowStatus(InfoBarSeverity severity, string title, string message)
    {
        StatusBar.Severity = severity;
        StatusBar.Title = title;
        StatusBar.Message = message;
        StatusBar.IsOpen = true;
    }

}






