# XM6 Control UI

WinUI 3 desktop app for the local WH-1000XM6 Tandem control path.

## Build and run

```powershell
dotnet build src/Xm6Control.App/Xm6Control.App.csproj -c Debug
src/Xm6Control.App/bin/Debug/net10.0-windows10.0.26100.0/win-x64/Xm6Control.App.exe
```

The app is unpackaged and self-contained with Windows App SDK 1.8.

## Pages

- **概览**: model, firmware, battery, connection state, active codec, DSEE
  effect.
- **环境声音控制**: NC, ASM, or Off; ambient level, automatic ambient sound
  with sensitivity, voice focus; apply and read-back.
- **均衡器**: preset, 10 band sliders, apply and read-back. Built-in preset
  defaults are filled from live device read-back; Custom keeps the editable sliders.
- **聆听模式**: Standard (uses EQ), Background Music with room distance,
  Cinema, plus a separate DSEE box.
- **多点连接**: multipoint on/off, apply and read-back.
- **自动关机**: remove-from-ears or disabled; apply and read-back.
- **情景**: save the current noise, EQ, and listening settings as a named scene,
  then apply it with one click.
- **设置**: startup, window, appearance, language, tray, refresh/reconnect, and
  target device.
- **关于**: feature summary, local-only scope, and GitHub link.

## Layout and window

- The content column fills the window. Every card uses the shared
  `CardBorderStyle`, so all cards have the same width, padding, border, and
  corner radius on every page and follow the window width.
- Page subtitles and hints wrap instead of clipping.
- The default window size is 920 x 600 logical pixels. A saved placement is
  restored when **Remember window** is on.
- The **Window** card in Settings has **恢复默认窗口大小**. It clears the saved
  position and size, then applies the default centered size; other settings are
  left untouched.

## Refresh

- Every page shares one **刷新状态** button in the fixed header row, next to the
  last update time. It stays enabled while reads and writes run and is never
  greyed out.
- On the overview the button reads every state the app shows: model, firmware,
  battery, noise control, EQ, multipoint, auto power off, listening mode, codec,
  and DSEE. A full read replaces the cached state, so a field that failed shows
  "未读取".
- Other pages read only what they display: noise control, EQ, listening mode
  plus the current EQ and DSEE, multipoint, or auto power off. The scene page
  reads noise, EQ, and listening mode because a new scene captures those three;
  settings and about read the battery so the timestamp and tray tooltip stay
  current.
- A partial read is merged into the cached state. Fields that were not requested
  keep their previous values.
- Clicking refresh during a write queues one read and shows
  "当前操作完成后会自动刷新状态". Clicks during a read in progress are coalesced
  into that read.
- The startup refresh and the automatic refresh timer always read the full
  state, regardless of the page that is open.

## Listening mode

- Standard turns BGM and Cinema off; the current EQ setting is left untouched.
- Background Music enables BGM and lets you choose My Room, Living Room, or
  Cafe. This maps to the headphone's `RoomSize` Small/Middle/Large values.
- Cinema enables the `UPMIX_CINEMA` audio effect and turns BGM off.
- The page shows the current EQ preset so the Standard/EQ relationship is clear.
- The DSEE box is separate from the mode radio buttons. Its switch writes
  Auto or Off and reads the setting and effect back. The protocol has no hard
  "on": `运行中` means the headphone reports the effect as active, while
  `已开启，当前未生效` means Auto is set but the current codec or mode does not
  apply the effect. The box is hidden when the device does not report DSEE
  setting support.

## Scenes

- A scene stores up to three groups: noise control (mode, level, automatic
  ambient, sensitivity, voice focus), EQ preset (plus bands for Custom), and
  listening mode with room distance.
- Applying a scene uses a single Tandem session and writes the groups in a fixed
  order: noise control, EQ, then listening mode. Each step is read back and
  reported separately; a failed step does not stop the remaining ones.
- When a scene targets NC, the app first writes the ambient parameters with mode
  ASM and then switches to NC. The headphone ignores ambient fields on an
  ASM -> NC write and keeps the last ASM values.
- Scenes are stored in the same settings file. Up to 50 scenes, unique names,
  no import/export yet.

## Settings

- **启动**: start with Windows (per-user `Run` entry), start minimized to the
  tray, and read the headphone state at startup. If the tray icon is unavailable
  the window opens normally.
- **窗口**: remember position and size, and reopen the last page. Restored
  positions are converted between DIPs and physical pixels and recentred when
  the saved position is mostly off-screen.
- **外观**: system/light/dark theme and Mica, Acrylic, or solid backdrop. An
  unsupported backdrop falls back to a solid theme colour.
- **语言**: Chinese or English, applied without restarting the app.
- **托盘**: show battery and mode in the tooltip; choose whether a single click
  opens the window or shows the menu. A double click always opens the window.
- **刷新与重连**: automatic refresh at 1, 5, or 15 minutes (off by default) and
  a retry policy of 2 s, 5 s, and 15 s after a transient failure. A missing or
  unavailable device is reported immediately instead of being retried. The
  shortest interval is one minute because battery is read at most once per
  minute.
- **目标设备**: device name and optional Bluetooth address used by every Tandem
  session. A wrong name is reported explicitly on the overview page.
- Close behaviour is part of this page: exit directly or hide to the tray.

Preferences are stored in `%LOCALAPPDATA%\Xm6Control\settings.json`. Writes are
atomic, unknown or missing fields fall back to defaults, and a malformed file is
kept aside as `settings.json.corrupt` instead of being overwritten.

## Connection lifetime

- While the main window is shown, the app keeps one Tandem session open and
  reuses the connection, GATT discovery, and handshake across actions. A warm
  write takes roughly 0.3-1 s instead of several seconds.
- Opening the window from the tray reconnects in the background immediately.
  An action issued while this prewarm runs waits for the connection instead of
  opening a second session.
- Hiding the window to the tray releases the connection so the phone app can use
  Tandem again. Automatic refresh while hidden uses a one-shot connect, refresh,
  and release.
- When the link drops (headphone power off, Bluetooth toggle, another host), the
  next operation reconnects automatically.
- Holding the connection while the window is open means the phone's Sound
  Connect cannot use Tandem during that time.

## System tray

- The tray icon menu provides **概览**, **环境声音**, **均衡器**, **聆听模式**,
  and **退出**.
- The tooltip shows the device, mode, and battery when available.
- Tray icon creation is best effort; if the shell rejects it, close-to-tray is
  not used and the app closes normally.

## Localization

- User-visible strings live in `src/Xm6Control.Core/Localization.cs` rather than
  `.resw` resources. This keeps runtime switching deterministic for an
  unpackaged, self-contained app.
- XAML strings use the `Loc` attached properties; dynamic strings call
  `Localization.Text`/`Format`. A language change re-applies both without
  restarting.
- Option labels, scene summaries, tray menu items, and status messages follow the
  selected language. User data such as scene names is never translated.

## Current UI limitations

- A scene apply is best-effort per step: partial success is reported, not rolled back.
- Restoring a window position across monitors with different DPI scales uses the
  window's current scale, so the restored position can be approximate.
- NC/ASM supports NC, ASM, and Off, plus automatic ambient sound (with
  sensitivity) and voice focus.
