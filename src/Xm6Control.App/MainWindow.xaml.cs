using System.Text.Json;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Xm6Control.Bluetooth.Windows;
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

    private static readonly NoiseSensitivityOption[] NoiseSensitivityOptions =
    [
        new(TandemNoiseAdaptiveSensitivity.Standard, "标准"),
        new(TandemNoiseAdaptiveSensitivity.High, "高灵敏度"),
        new(TandemNoiseAdaptiveSensitivity.Low, "低灵敏度"),
    ];

    private static readonly BgmRoomOption[] BgmRoomOptions =
    [
        new(TandemRoomSize.Small, "我的房间"),
        new(TandemRoomSize.Middle, "客厅"),
        new(TandemRoomSize.Large, "咖啡馆"),
    ];

    private static readonly AutoPowerOption[] AutoPowerOptions =
    [
        new(TandemAutoPowerOffElement.WhenRemovedFromEars, "取下耳机后"),
        new(TandemAutoPowerOffElement.Disable, "关闭"),
    ];

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Xm6Control",
        "settings.json");

    private readonly List<Slider> _eqSliders = [];
    private readonly Dictionary<TandemEqPresetId, byte[]> _eqPresetBands = [];
    private TrayIcon? _trayIcon;
    private bool _closeToTray;
    private bool _allowClose;
    private bool _busy;

    public MainWindow()
    {
        InitializeComponent();
        SystemBackdrop = new MicaBackdrop();
        Title = "XM6 Control";
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (File.Exists(iconPath))
        {
            try
            {
                AppWindow.SetIcon(iconPath);
            }
            catch
            {
                // The exe icon still applies if the title-bar icon cannot be set.
            }
        }
        AmbientLevelSlider.Minimum = 1;
        AmbientLevelSlider.Maximum = 20;
        AmbientLevelSlider.StepFrequency = 1;
        AmbientLevelSlider.Value = 20;
        BuildEqSliders();
        foreach (var preset in PresetDefaultBands)
        {
            _eqPresetBands[preset.Key] = preset.Value;
        }

        EqPresetCombo.ItemsSource = SupportedPresets;
        NoiseAdaptiveSensitivityCombo.ItemsSource = NoiseSensitivityOptions;
        NoiseAdaptiveSensitivityCombo.SelectedIndex = 0;
        AutoPowerCombo.ItemsSource = AutoPowerOptions;
        AutoPowerCombo.SelectedIndex = AutoPowerOptions.Length - 1;
        BgmRoomCombo.ItemsSource = BgmRoomOptions;
        BgmRoomCombo.SelectedIndex = 1;
        BgmRoomCombo.IsEnabled = false;
        ListeningModeRadio.SelectedIndex = 0;
        NoiseModeRadio.SelectedIndex = 0;
        UpdateNoiseControls();
        LoadCloseBehavior();
        CloseBehaviorRadio.SelectedIndex = _closeToTray ? 1 : 0;
        AppWindow.Closing += AppWindow_Closing;
        Closed += MainWindow_Closed;
        InitializeTrayIcon();
        Nav.SelectedItem = OverviewNav;
        ShowSection("overview");
        _ = RefreshAsync();
    }

    private sealed record NoiseSensitivityOption(
        TandemNoiseAdaptiveSensitivity Value,
        string Label);

    private sealed record BgmRoomOption(TandemRoomSize Value, string Label);

    private sealed record AutoPowerOption(TandemAutoPowerOffElement Value, string Label);

    private sealed class AppSettings
    {
        public bool CloseToTray { get; set; }
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
                Text = $"频段 {index + 1}",
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
        OverviewSection.Visibility = tag == "overview" ? Visibility.Visible : Visibility.Collapsed;
        NoiseSection.Visibility = tag == "noise" ? Visibility.Visible : Visibility.Collapsed;
        EqSection.Visibility = tag == "eq" ? Visibility.Visible : Visibility.Collapsed;
        ListeningSection.Visibility = tag == "listening" ? Visibility.Visible : Visibility.Collapsed;
        MultipointSection.Visibility = tag == "multipoint" ? Visibility.Visible : Visibility.Collapsed;
        AutoPowerSection.Visibility = tag == "autopower" ? Visibility.Visible : Visibility.Collapsed;
        CloseBehaviorSection.Visibility = tag == "closebehavior" ? Visibility.Visible : Visibility.Collapsed;
        AboutSection.Visibility = tag == "about" ? Visibility.Visible : Visibility.Collapsed;
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

    private void CloseBehaviorRadio_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CloseBehaviorRadio.SelectedIndex < 0)
        {
            return;
        }

        var closeToTray = CloseBehaviorRadio.SelectedIndex == 1;
        if (closeToTray == _closeToTray)
        {
            return;
        }

        _closeToTray = closeToTray;
        SaveCloseBehavior();
    }

    private void InitializeTrayIcon()
    {
        try
        {
            _trayIcon = new TrayIcon();
            if (!_trayIcon.IsAvailable)
            {
                _trayIcon.Dispose();
                _trayIcon = null;
                return;
            }

            _trayIcon.OverviewRequested += () => ShowFromTray("overview");
            _trayIcon.ListeningRequested += () => ShowFromTray("listening");
            _trayIcon.ExitRequested += ExitFromTray;
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
        });
    }

    private void ExitFromTray()
    {
        _allowClose = true;
        _trayIcon?.Dispose();
        _trayIcon = null;
        Close();
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose)
        {
            return;
        }

        if (_closeToTray && _trayIcon?.IsAvailable == true)
        {
            args.Cancel = true;
            AppWindow.Hide();
            return;
        }

        _trayIcon?.Dispose();
        _trayIcon = null;
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
    }

    private void LoadCloseBehavior()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return;
            }

            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
            _closeToTray = settings?.CloseToTray ?? false;
        }
        catch
        {
            // A missing or unreadable settings file is non-fatal.
        }
    }

    private void SaveCloseBehavior()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                SettingsPath,
                JsonSerializer.Serialize(
                    new AppSettings { CloseToTray = _closeToTray },
                    new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Persisting the close preference is best effort.
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        await RunBusyAsync("正在连接耳机并读取状态...", async () =>
        {
            var report = await new TandemGattSession().RunAsync(new ProbeRequest
            {
                QuerySnapshot = true,
            });

            ApplyReport(report);
            if (report.Status == "ok")
            {
                ShowStatus(InfoBarSeverity.Success, "状态已更新", "已读取耳机当前状态。");
            }
            else
            {
                ShowStatus(InfoBarSeverity.Error, "读取失败", report.Message ?? report.Status);
            }
        });
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

        await RunBusyAsync("正在写入降噪/环境声...", async () =>
        {
            var report = await new TandemGattSession().RunAsync(new ProbeRequest
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
                    report.NcAsm.Verified ? "已应用" : "写入未确认",
                    FormatNoiseState(after));
            }
            else
            {
                ShowStatus(InfoBarSeverity.Error, "写入未确认", "写入后没有回读到降噪/环境声状态。");
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

        await RunBusyAsync("正在写入均衡器...", async () =>
        {
            var report = await new TandemGattSession().RunAsync(new ProbeRequest
            {
                ApplyEq = true,
                EqPreset = preset,
                EqBands = preset == TandemEqPresetId.Custom ? bands : null,
            });

            if (report.Eq?.After is { } after)
            {
                UpdateEq(after);
                var suffix = report.Eq.UsedPresetDefault ? "（已读取预设默认值）" : string.Empty;
                ShowStatus(
                    report.Eq.Verified ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
                    report.Eq.Verified ? "已应用" : "写入未确认",
                    $"当前 EQ：{after.Preset}{suffix}。");
            }
            else
            {
                ShowStatus(InfoBarSeverity.Error, "写入未确认", "写入后没有回读到 EQ 状态。");
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

        await RunBusyAsync("正在写入聆听模式...", async () =>
        {
            var report = await new TandemGattSession().RunAsync(new ProbeRequest
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
                ShowStatus(
                    InfoBarSeverity.Success,
                    "已应用",
                    $"聆听模式：{ListeningModeName(change.After?.Mode ?? mode)}"
                    + (mode == TandemListeningMode.BackgroundMusic
                        ? $"，距离：{RoomSizeName(room)}。"
                        : "。"));
            }
            else
            {
                ShowStatus(
                    InfoBarSeverity.Warning,
                    "写入未确认",
                    $"耳机当前模式：{ListeningModeName(change?.After?.Mode)}。");
            }
        });
    }

    private async void ApplyMultipoint_Click(object sender, RoutedEventArgs e)
    {
        var enabled = MultipointToggle.IsOn;

        await RunBusyAsync("正在写入多点连接设置...", async () =>
        {
            var report = await new TandemGattSession().RunAsync(new ProbeRequest
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
                    "已应用",
                    $"多点连接已{(enabled ? "开启" : "关闭")}。");
            }
            else
            {
                ShowStatus(
                    InfoBarSeverity.Warning,
                    "写入未确认",
                    $"耳机当前仍为{FormatOnOff(change?.After)}。若耳机正在使用 LDAC 或两台设备同时连接，关闭多点连接可能被拒绝。");
            }
        });
    }

    private async void ApplyAutoPower_Click(object sender, RoutedEventArgs e)
    {
        var option = AutoPowerCombo.SelectedItem as AutoPowerOption
            ?? AutoPowerOptions[^1];

        await RunBusyAsync("正在写入自动关机设置...", async () =>
        {
            var report = await new TandemGattSession().RunAsync(new ProbeRequest
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
                    "已应用",
                    $"自动关机：{AutoPowerName(change.After?.First ?? option.Value)}。");
            }
            else
            {
                ShowStatus(
                    InfoBarSeverity.Warning,
                    "写入未确认",
                    $"耳机当前状态：{AutoPowerName(change?.After?.First)}。");
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
        DeviceNameText.Text = report.Handshake.Model ?? "WH-1000XM6";
        FirmwareText.Text = report.Handshake.Firmware ?? "未读取";
        ConnectionText.Text = report.Status == "ok" ? "已连接" : "未连接";
        LastUpdatedText.Text = $"更新于 {DateTime.Now:HH:mm:ss}";

        if (report.Snapshot is { } snapshot)
        {
            BatteryText.Text = snapshot.BatteryPercent is { } battery ? $"{battery}%" : "未读取";
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
            BatteryText.Text = "未读取";
        }
    }

    private void UpdateNoise(TandemNcAsmState state)
    {
        var off = state.OnOff == TandemNcAsmOnOffValue.Off;
        NoiseModeRadio.SelectedIndex = off
            ? 2
            : state.Mode == TandemNcNcssAsmMode.Asm ? 1 : 0;
        AmbientLevelSlider.Value = state.Level;
        AmbientLevelText.Text = state.Level.ToString();
        NoiseAdaptiveToggle.IsOn = state.NoiseAdaptiveOnOff == TandemNcAsmOnOffValue.On;
        VoiceFocusToggle.IsOn = state.AmbientMode == TandemAmbientSoundMode.Voice;
        var sensitivity = NoiseSensitivityOptions.FirstOrDefault(
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
            return "当前模式：关闭";
        }

        if (state.Mode == TandemNcNcssAsmMode.Nc)
        {
            return "当前模式：降噪";
        }

        var adaptive = state.NoiseAdaptiveOnOff == TandemNcAsmOnOffValue.On
            ? $"开启（{SensitivityName(state.Sensitivity)}）"
            : "关闭";
        var voice = state.AmbientMode == TandemAmbientSoundMode.Voice ? "开启" : "关闭";
        return $"当前模式：环境声，等级 {state.Level}，自动环境声音：{adaptive}，关注语音：{voice}";
    }

    private static string SensitivityName(TandemNoiseAdaptiveSensitivity sensitivity)
    {
        return sensitivity switch
        {
            TandemNoiseAdaptiveSensitivity.Standard => "标准",
            TandemNoiseAdaptiveSensitivity.High => "高灵敏度",
            TandemNoiseAdaptiveSensitivity.Low => "低灵敏度",
            _ => "未读取",
        };
    }

    private void UpdateEq(TandemEqState state)
    {
        EqPresetCombo.SelectedItem = state.Preset;
        if (state.Preset != TandemEqPresetId.Custom)
        {
            _eqPresetBands[state.Preset] = state.Bands;
        }

        EqCurrentText.Text = $"当前 EQ：{state.Preset}，频段 {string.Join(',', state.Bands)}";
        ListeningEqText.Text = $"当前 EQ：{state.Preset}";
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
        ListeningModeRadio.SelectedIndex = state.Mode switch
        {
            TandemListeningMode.BackgroundMusic => 1,
            TandemListeningMode.Cinema => 2,
            _ => 0,
        };

        var room = BgmRoomOptions.FirstOrDefault(item => item.Value == state.RoomSize)
            ?? BgmRoomOptions[1];
        BgmRoomCombo.SelectedItem = room;
        var roomText = state.Mode == TandemListeningMode.BackgroundMusic
            ? $"，距离 {room.Label}"
            : string.Empty;
        ListeningCurrentText.Text = $"当前模式：{ListeningModeName(state.Mode)}{roomText}";
    }

    private static string ListeningModeName(TandemListeningMode? mode)
    {
        return mode switch
        {
            TandemListeningMode.BackgroundMusic => "背景音乐",
            TandemListeningMode.Cinema => "电影",
            TandemListeningMode.Standard => "标准",
            _ => "未读取",
        };
    }

    private static string RoomSizeName(TandemRoomSize roomSize)
    {
        return roomSize switch
        {
            TandemRoomSize.Small => "我的房间",
            TandemRoomSize.Middle => "客厅",
            TandemRoomSize.Large => "咖啡馆",
            _ => "未读取",
        };
    }

    private void UpdateMultipoint(bool enabled)
    {
        MultipointToggle.IsOn = enabled;
        MultipointStateText.Text = $"当前状态：{(enabled ? "已开启" : "已关闭")}";
    }

    private void UpdateAutoPower(TandemAutoPowerOffState state)
    {
        var option = AutoPowerOptions.FirstOrDefault(item => item.Value == state.First);
        if (option is not null)
        {
            AutoPowerCombo.SelectedItem = option;
        }

        AutoPowerStateText.Text = $"当前状态：{AutoPowerName(state.First)}";
    }

    private static string AutoPowerName(TandemAutoPowerOffElement? element)
    {
        return element switch
        {
            TandemAutoPowerOffElement.WhenRemovedFromEars => "取下耳机后",
            TandemAutoPowerOffElement.Disable => "关闭",
            _ => "未读取",
        };
    }

    private static string FormatOnOff(bool? value)
    {
        return value switch
        {
            true => "开启",
            false => "关闭",
            null => "未读取",
        };
    }

    private async Task RunBusyAsync(string message, Func<Task> action)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        SetButtonsEnabled(false);
        ShowStatus(InfoBarSeverity.Informational, "请稍候", message);
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, "操作失败", ex.Message);
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
    }

    private void ShowStatus(InfoBarSeverity severity, string title, string message)
    {
        StatusBar.Severity = severity;
        StatusBar.Title = title;
        StatusBar.Message = message;
        StatusBar.IsOpen = true;
    }

}






