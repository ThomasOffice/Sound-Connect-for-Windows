namespace Xm6Control.Core;

/// <summary>
/// Small in-app string table. Keeping it here (instead of a .resw resource
/// pipeline) makes runtime switching deterministic for an unpackaged,
/// self-contained WinUI app.
/// </summary>
public static class Localization
{
    public static readonly IReadOnlyDictionary<string, (string Zh, string En)> Table =
        new Dictionary<string, (string Zh, string En)>(StringComparer.Ordinal)
        {
            // Navigation and pages
            ["nav.overview"] = ("概览", "Overview"),
            ["nav.noise"] = ("环境声音控制", "Noise control"),
            ["nav.eq"] = ("均衡器", "Equalizer"),
            ["nav.listening"] = ("聆听模式", "Listening mode"),
            ["nav.multipoint"] = ("多点连接", "Multipoint"),
            ["nav.autopower"] = ("自动关机", "Auto power off"),
            ["nav.scenes"] = ("情景", "Scenes"),
            ["nav.settings"] = ("设置", "Settings"),
            ["nav.about"] = ("关于", "About"),

            ["page.overview.title"] = ("概览", "Overview"),
            ["page.overview.subtitle"] = ("Sony WH-1000XM6 的本机控制面板", "Local control panel for Sony WH-1000XM6"),
            ["overview.device"] = ("设备", "Device"),
            ["overview.firmware"] = ("固件", "Firmware"),
            ["overview.battery"] = ("电量", "Battery"),
            ["overview.connection"] = ("连接", "Connection"),
            ["overview.connected"] = ("已连接", "Connected"),
            ["overview.disconnected"] = ("未连接", "Not connected"),
            ["overview.unknown"] = ("未读取", "Not read"),
            ["overview.refresh"] = ("刷新状态", "Refresh"),
            ["overview.updated"] = ("更新于 {0}", "Updated {0}"),
            ["overview.codec"] = ("编解码器", "Codec"),
            ["overview.dsee"] = ("DSEE", "DSEE"),
            ["codec.unsettled"] = ("未确定", "Not settled"),
            ["codec.other"] = ("其他", "Other"),

            ["page.noise.title"] = ("环境声音控制", "Noise control"),
            ["page.noise.subtitle"] = ("当前状态会先读取，再写入，并回读确认。", "Current state is read first, then written, then verified."),
            ["noise.mode.nc"] = ("降噪", "Noise cancelling"),
            ["noise.mode.asm"] = ("环境声", "Ambient sound"),
            ["noise.mode.off"] = ("关闭", "Off"),
            ["noise.level"] = ("环境声等级", "Ambient level"),
            ["noise.adaptive"] = ("自动环境声音", "Automatic ambient sound"),
            ["noise.sensitivity"] = ("灵敏度", "Sensitivity"),
            ["noise.voicefocus"] = ("关注语音", "Focus on voice"),
            ["noise.state.off"] = ("当前模式：关闭", "Current mode: off"),
            ["noise.state.nc"] = ("当前模式：降噪", "Current mode: noise cancelling"),
            ["noise.state.asm"] = (
                "当前模式：环境声，等级 {0}，自动环境声音：{1}，关注语音：{2}",
                "Current mode: ambient sound, level {0}, automatic ambient: {1}, focus on voice: {2}"),
            ["noise.adaptive.on"] = ("开启（{0}）", "On ({0})"),
            ["noise.adaptive.off"] = ("关闭", "Off"),
            ["sensitivity.standard"] = ("标准", "Standard"),
            ["sensitivity.high"] = ("高灵敏度", "High"),
            ["sensitivity.low"] = ("低灵敏度", "Low"),

            ["page.eq.title"] = ("均衡器", "Equalizer"),
            ["page.eq.subtitle"] = (
                "当前预设和 10 段频段值来自耳机；写入后会重新读取确认。",
                "The current preset and 10 bands come from the headphone; the app reads them back after writing."),
            ["eq.preset"] = ("预设", "Preset"),
            ["eq.band"] = ("频段 {0}", "Band {0}"),
            ["eq.state"] = ("当前 EQ：{0}，频段 {1}", "Current EQ: {0}, bands {1}"),
            ["eq.preset.default"] = ("（已读取预设默认值）", " (preset defaults read back)"),

            ["page.listening.title"] = ("聆听模式", "Listening mode"),
            ["page.listening.subtitle"] = (
                "标准模式使用当前 EQ；背景音乐和电影会启用耳机音效。",
                "Standard uses the current EQ; Background music and Cinema enable headphone effects."),
            ["listening.mode.standard"] = ("标准（使用 EQ）", "Standard (uses EQ)"),
            ["listening.mode.bgm"] = ("背景音乐", "Background music"),
            ["listening.mode.cinema"] = ("电影", "Cinema"),
            ["listening.room"] = ("背景音乐距离", "Background music distance"),
            ["listening.hint"] = (
                "说明：背景音乐的距离对应我的房间、客厅、咖啡馆。切换到标准会关闭 BGM / 电影音效，并恢复当前 EQ 设置。",
                "Background music distance maps to My room, Living room, and Cafe. Switching to Standard turns BGM and Cinema off and keeps the current EQ."),
            ["listening.name.standard"] = ("标准", "Standard"),
            ["listening.name.bgm"] = ("背景音乐", "Background music"),
            ["listening.name.cinema"] = ("电影", "Cinema"),
            ["listening.room.small"] = ("我的房间", "My room"),
            ["listening.room.middle"] = ("客厅", "Living room"),
            ["listening.room.large"] = ("咖啡馆", "Cafe"),

            ["dsee.title"] = ("DSEE", "DSEE"),
            ["dsee.auto"] = ("DSEE 自动", "DSEE Auto"),
            ["dsee.hint"] = (
                "协议只提供“自动/关”。是否真正生效取决于当前编码和聆听模式；显示“已开启，当前未生效”时代表设置已打开但效果尚未应用。",
                "The protocol exposes Auto/Off only. Whether the effect applies depends on the current codec and listening mode; \"Enabled, not active\" means the setting is on but the effect is not applied yet."),
            ["dsee.state.running"] = ("运行中", "Active"),
            ["dsee.state.enabled"] = ("已开启，当前未生效", "Enabled, not active"),
            ["dsee.state.off"] = ("关闭", "Off"),
            ["dsee.state.unknown"] = ("未读取", "Not read"),
            ["dsee.state.unsupported"] = ("不支持", "Not supported"),
            ["dsee.type.dseehx"] = ("DSEE HX", "DSEE HX"),
            ["dsee.type.dsee"] = ("DSEE", "DSEE"),
            ["dsee.type.ai"] = ("DSEE Extreme", "DSEE Extreme"),
            ["dsee.type.ultimate"] = ("DSEE Ultimate", "DSEE Ultimate"),

            ["page.multipoint.title"] = ("多点连接", "Multipoint"),
            ["page.multipoint.subtitle"] = (
                "允许耳机同时连接两台设备；关闭后第二台设备会断开。",
                "Lets the headphone stay connected to two devices; turning it off disconnects the second one."),
            ["multipoint.label"] = ("多点连接", "Multipoint"),
            ["multipoint.on"] = ("当前状态：已开启", "Current state: on"),
            ["multipoint.off"] = ("当前状态：已关闭", "Current state: off"),
            ["multipoint.hint"] = (
                "说明：耳机端没有公开设备列表/主动切换命令。此处控制多点连接开关；在手机与电脑之间切换音频源由耳机和当前播放设备自动完成。若耳机正在使用 LDAC，关闭多点连接可能被拒绝。",
                "The headphone exposes no device list or active-source command. This switch controls multipoint only; source switching is handled by the headphone and the playing device. Turning it off can be refused while LDAC is in use."),

            ["page.autopower.title"] = ("自动关机", "Auto power off"),
            ["page.autopower.subtitle"] = (
                "设置取下耳机后自动关机，或关闭自动关机。",
                "Power off after taking the headphones off, or disable auto power off."),
            ["autopower.label"] = ("自动关机", "Auto power off"),
            ["autopower.removed"] = ("取下耳机后", "After removing from ears"),
            ["autopower.disable"] = ("关闭", "Off"),
            ["autopower.hint"] = (
                "说明：自动关机只有“取下耳机后”和“关闭”两种状态。",
                "Auto power off has two states: after removing from ears, or off."),

            ["page.scenes.title"] = ("情景", "Scenes"),
            ["page.scenes.subtitle"] = (
                "把环境声音、均衡器和聆听模式组合保存，一键应用。",
                "Save a combination of noise control, EQ, and listening mode, then apply it in one click."),
            ["scenes.new"] = ("新建情景", "New scene"),
            ["scenes.name"] = ("情景名称", "Scene name"),
            ["scenes.name.placeholder"] = ("例如：专注工作", "For example: Focus"),
            ["scenes.capture"] = ("把当前设置保存为情景", "Save current settings as a scene"),
            ["scenes.capture.hint"] = (
                "当前设置指页面上显示的环境声音、均衡器和聆听模式。应用时会依次写入并逐项回读。",
                "Current settings are the noise control, EQ, and listening mode shown in the app. Applying writes them in order and verifies each step."),
            ["scenes.saved"] = ("已保存的情景", "Saved scenes"),
            ["scenes.empty"] = ("还没有保存的情景。", "No saved scenes yet."),
            ["scenes.apply"] = ("应用", "Apply"),
            ["scenes.delete"] = ("删除", "Delete"),
            ["scene.nc"] = ("降噪", "Noise cancelling"),
            ["scene.asm"] = ("环境声：等级 {0}", "Ambient: level {0}"),
            ["scene.noise.off"] = ("环境声音：关闭", "Noise control: off"),
            ["scene.eq"] = ("均衡器：{0}", "EQ: {0}"),
            ["scene.listening.standard"] = ("聆听：标准", "Listening: Standard"),
            ["scene.listening.bgm"] = ("聆听：背景音乐（{0}）", "Listening: Background music ({0})"),
            ["scene.listening.cinema"] = ("聆听：电影", "Listening: Cinema"),

            ["page.settings.title"] = ("设置", "Settings"),
            ["page.settings.subtitle"] = (
                "以下选项只保存在本机，不会上传或同步。",
                "These options stay on this PC and are never uploaded or synced."),
            ["settings.startup"] = ("启动", "Startup"),
            ["settings.launch"] = ("开机自动启动", "Start with Windows"),
            ["settings.startminimized"] = ("启动时最小化到系统托盘", "Start minimized to the tray"),
            ["settings.refreshonstart"] = ("启动时读取耳机状态", "Read headphone state at startup"),
            ["settings.startup.hint"] = (
                "开机自动启动写入当前用户的启动项；如果托盘图标不可用，窗口会正常显示。",
                "Start with Windows writes the per-user startup entry. If the tray icon is unavailable the window opens normally."),
            ["settings.window"] = ("窗口", "Window"),
            ["settings.resetwindow"] = ("恢复默认窗口大小", "Reset window size"),
            ["settings.rememberwindow"] = ("记住窗口位置和大小", "Remember window position and size"),
            ["settings.lastsection"] = ("下次启动打开：{0}", "Opens at startup: {0}"),
            ["settings.appearance"] = ("外观", "Appearance"),
            ["settings.theme"] = ("主题", "Theme"),
            ["settings.backdrop"] = ("背景", "Backdrop"),
            ["theme.system"] = ("跟随系统", "System"),
            ["theme.light"] = ("浅色", "Light"),
            ["theme.dark"] = ("深色", "Dark"),
            ["backdrop.mica"] = ("Mica", "Mica"),
            ["backdrop.acrylic"] = ("亚克力", "Acrylic"),
            ["backdrop.solid"] = ("纯色", "Solid"),
            ["settings.tray"] = ("托盘", "Tray"),
            ["settings.tray.showstatus"] = ("在托盘提示中显示电量和模式", "Show battery and mode in the tray tooltip"),
            ["settings.tray.click"] = ("单击托盘图标", "Single click on the tray icon"),
            ["trayclick.openwindow"] = ("打开窗口", "Open the window"),
            ["trayclick.showmenu"] = ("显示菜单", "Show the menu"),
            ["settings.refresh"] = ("刷新与重连", "Refresh and reconnect"),
            ["settings.autorefresh"] = ("自动刷新间隔", "Automatic refresh interval"),
            ["settings.autoreconnect"] = ("连接失败时自动重试", "Retry automatically after a failed connection"),
            ["settings.refresh.hint"] = (
                "耳机电量最多每分钟读取一次，因此最短间隔为 1 分钟。仅在窗口空闲时刷新。",
                "Battery is read at most once per minute, so the shortest interval is one minute. Refreshes are skipped while busy."),
            ["refresh.off"] = ("关闭", "Off"),
            ["refresh.1"] = ("1 分钟", "1 minute"),
            ["refresh.5"] = ("5 分钟", "5 minutes"),
            ["refresh.15"] = ("15 分钟", "15 minutes"),
            ["settings.device"] = ("目标设备", "Target device"),
            ["settings.device.name"] = ("设备名称", "Device name"),
            ["settings.device.address"] = ("蓝牙地址（可选）", "Bluetooth address (optional)"),
            ["settings.device.hint"] = (
                "默认 WH-1000XM6。修改后点击“刷新状态”生效；地址留空时按名称查找。",
                "Defaults to WH-1000XM6. Click Refresh to apply changes; leave the address empty to look up by name."),
            ["settings.close"] = ("关闭行为", "Close behavior"),
            ["settings.close.direct"] = ("直接关闭应用", "Close the app"),
            ["settings.close.tray"] = ("隐藏到系统托盘", "Hide to the tray"),
            ["settings.close.hint"] = (
                "隐藏后，可从系统托盘菜单打开页面，也可以从托盘退出应用。",
                "When hidden, use the tray menu to open pages or exit the app."),
            ["settings.language"] = ("语言", "Language"),

            ["page.about.title"] = ("关于", "About"),
            ["about.name"] = ("XM6 Control", "XM6 Control"),
            ["about.version"] = ("版本 1.0.3", "Version 1.0.3"),
            ["about.body"] = (
                "本机 Sony WH-1000XM6 控制工具，支持环境声音控制（降噪/环境声/关闭、自动环境声音、关注语音）、均衡器、聆听模式、多点连接、自动关机和系统托盘。",
                "Local Sony WH-1000XM6 control tool: noise control (NC / ambient / off, automatic ambient, focus on voice), EQ, listening mode, multipoint, auto power off, and a system tray."),
            ["about.local"] = (
                "LE Tandem 控制通道；不使用云服务，不上传设备数据。",
                "Works over the LE Tandem control channel. No cloud services and no device data uploads."),
            ["about.links"] = ("功能和源码：", "Features and source:"),

            ["tray.overview"] = ("概览", "Overview"),
            ["tray.noise"] = ("环境声音", "Noise control"),
            ["tray.eq"] = ("均衡器", "Equalizer"),
            ["tray.listening"] = ("聆听模式", "Listening mode"),
            ["tray.exit"] = ("退出", "Exit"),
            ["tray.tooltip.disconnected"] = ("{0} · 未连接", "{0} · Not connected"),
            ["tray.tooltip.plain"] = ("{0} · XM6 Control", "{0} · XM6 Control"),
            ["tray.tooltip.status"] = ("{0} · {1} · {2}", "{0} · {1} · {2}"),
            ["tray.tooltip.status.nobattery"] = ("{0} · {1}", "{0} · {1}"),
            ["tray.mode.unknown"] = ("未知模式", "Unknown mode"),

            ["toggle.on"] = ("开启", "On"),
            ["toggle.off"] = ("关闭", "Off"),
            ["common.mode"] = ("模式", "Mode"),
            ["common.current.mode"] = ("当前模式：未读取", "Current mode: not read"),
            ["common.current.state"] = ("当前状态：未读取", "Current state: not read"),
            ["common.current.eq"] = ("当前 EQ：未读取", "Current EQ: not read"),
            ["button.apply.verify"] = ("应用并回读", "Apply and verify"),
            ["common.list.separator"] = ("；", "; "),
            ["eq.current.value"] = ("当前 EQ：{0}", "Current EQ: {0}"),
            ["eq.applied"] = ("当前 EQ：{0}{1}。", "Current EQ: {0}{1}."),
            ["listening.current.value"] = ("当前模式：{0}", "Current mode: {0}"),
            ["listening.room.suffix"] = ("，距离 {0}", ", distance {0}"),
            ["autopower.current.value"] = ("当前状态：{0}", "Current state: {0}"),
            ["status.listening.applied"] = ("聆听模式：{0}{1}。", "Listening mode: {0}{1}."),
            ["status.listening.room"] = ("，距离：{0}", ", distance: {0}"),

            ["status.busy"] = ("请稍候", "Working"),
            ["status.refreshing"] = ("正在连接耳机并读取状态...", "Connecting and reading the headphone..."),
            ["status.auto.refreshing"] = ("正在自动刷新...", "Refreshing automatically..."),
            ["status.refreshed"] = ("状态已更新", "State updated"),
            ["status.refreshed.body"] = ("已读取耳机当前状态。", "Current headphone state has been read."),
            ["status.refresh.queued"] = (
                "当前操作完成后会自动刷新状态。",
                "The status will refresh when the current operation finishes."),
            ["status.readfailed"] = ("读取失败", "Read failed"),
            ["status.windowreset.title"] = ("窗口大小已恢复", "Window size reset"),
            ["status.windowreset"] = (
                "已恢复为默认的 920 × 600。",
                "The default 920 × 600 size has been restored."),
            ["status.notfound"] = (
                "未找到设备「{0}」。请确认耳机已连接，或在设置中修改设备名称。",
                "Device \"{0}\" was not found. Check the connection or change the device name in Settings."),
            ["status.device.unavailable"] = (
                "耳机当前不可用。请确认耳机已开机并已与电脑连接。",
                "The headphone is unavailable. Make sure it is powered on and connected to this PC."),
            ["status.operationfailed"] = ("操作失败", "Operation failed"),
            ["status.applied"] = ("已应用", "Applied"),
            ["status.unverified"] = ("写入未确认", "Write not confirmed"),
            ["status.settings.unsaved"] = ("设置未保存", "Settings not saved"),
            ["status.startup.failed"] = ("无法修改开机启动项", "Could not change the startup entry"),
            ["status.startup.registry"] = ("注册表写入失败。", "Registry write failed."),
            ["status.startup.nopath"] = ("无法确定程序路径。", "Could not determine the executable path."),

            ["status.noise.busy"] = ("正在写入降噪/环境声...", "Writing noise control..."),
            ["status.noise.noread"] = ("写入后没有回读到降噪/环境声状态。", "No noise control state was read back after writing."),
            ["status.eq.busy"] = ("正在写入均衡器...", "Writing EQ..."),
            ["status.eq.noread"] = ("写入后没有回读到 EQ 状态。", "No EQ state was read back after writing."),
            ["status.listening.busy"] = ("正在写入聆听模式...", "Writing listening mode..."),
            ["status.dsee.applying"] = ("正在设置 DSEE...", "Applying DSEE..."),
            ["status.dsee.applied"] = ("DSEE 设置已回读", "DSEE setting verified"),
            ["status.dsee.unverified"] = ("DSEE 设置未确认", "DSEE setting not verified"),
            ["status.dsee.unsupported"] = ("未能读取 DSEE 设置", "Could not read the DSEE setting"),
            ["status.listening.current"] = ("耳机当前模式：{0}。", "Headphone mode: {0}."),
            ["status.multipoint.busy"] = ("正在写入多点连接设置...", "Writing multipoint settings..."),
            ["status.multipoint.on"] = ("多点连接已开启。", "Multipoint is on."),
            ["status.multipoint.off"] = ("多点连接已关闭。", "Multipoint is off."),
            ["status.multipoint.refused"] = (
                "耳机当前仍为{0}。若耳机正在使用 LDAC 或两台设备同时连接，关闭多点连接可能被拒绝。",
                "The headphone is still {0}. Turning multipoint off can be refused while LDAC or two devices are active."),
            ["status.autopower.busy"] = ("正在写入自动关机设置...", "Writing auto power off settings..."),
            ["status.autopower.applied"] = ("自动关机：{0}。", "Auto power off: {0}."),
            ["status.autopower.current"] = ("耳机当前状态：{0}。", "Headphone state: {0}."),

            ["status.scene.busy"] = ("正在应用情景「{0}」...", "Applying scene \"{0}\"..."),
            ["status.scene.applied"] = ("情景已应用", "Scene applied"),
            ["status.scene.partial"] = ("情景部分未确认", "Scene partially confirmed"),
            ["status.scene.failed"] = ("情景未应用", "Scene not applied"),
            ["status.scene.verified"] = ("已确认", "confirmed"),
            ["status.scene.unverified"] = ("未确认", "not confirmed"),
            ["status.scene.saved"] = ("已保存情景", "Scene saved"),
            ["status.scene.saved.body"] = ("「{0}」已保存。", "\"{0}\" was saved."),
            ["status.scene.savefailed"] = ("无法保存情景", "Could not save the scene"),
            ["status.scene.noname"] = ("请先填写情景名称。", "Enter a scene name first."),
            ["status.scene.limit"] = ("最多保存 50 个情景。", "You can save at most 50 scenes."),
            ["status.scene.duplicate"] = ("已有同名情景，请换一个名称。", "A scene with that name already exists."),
            ["status.scene.deleted"] = ("已删除情景", "Scene deleted"),
            ["status.scene.deleted.body"] = ("「{0}」已删除。", "\"{0}\" was deleted."),
        };

    private static string _language = UserSettings.ChineseLanguage;

    public static event Action? Changed;

    public static string Language => _language;

    public static bool IsEnglish =>
        string.Equals(_language, UserSettings.EnglishLanguage, StringComparison.Ordinal);

    public static void SetLanguage(string? language)
    {
        var normalized = UserSettings.NormalizeLanguage(language);
        if (string.Equals(normalized, _language, StringComparison.Ordinal))
        {
            return;
        }

        _language = normalized;
        Changed?.Invoke();
    }

    public static string Text(string key)
    {
        if (!Table.TryGetValue(key, out var entry))
        {
            return key;
        }

        return IsEnglish ? entry.En : entry.Zh;
    }

    public static string Format(string key, params object?[] arguments)
    {
        return string.Format(Text(key), arguments);
    }
}
