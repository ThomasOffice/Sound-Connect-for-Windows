# XM6 Control UI

WinUI 3 desktop app for the local WH-1000XM6 Tandem control path.

## Build and run

```powershell
dotnet build src/Xm6Control.App/Xm6Control.App.csproj -c Debug
src/Xm6Control.App/bin/Debug/net10.0-windows10.0.26100.0/win-x64/Xm6Control.App.exe
```

The app is unpackaged and self-contained with Windows App SDK 1.8.

## Pages

- **概览**: model, firmware, battery, connection state, refresh.
- **环境声音控制**: NC, ASM, or Off; ambient level, automatic ambient sound
  with sensitivity, voice focus; apply and read-back.
- **均衡器**: preset, 10 band sliders, apply and read-back. Built-in preset
  defaults are filled from live device read-back; Custom keeps the editable sliders.
- **聆听模式**: Standard (uses EQ), Background Music with room distance, Cinema.
- **多点连接**: multipoint on/off, apply and read-back.
- **自动关机**: remove-from-ears or disabled; apply and read-back.
- **关闭行为**: close the app directly or hide it to the system tray.
- **关于**: feature summary, local-only scope, and GitHub link.

## Listening mode

- Standard turns BGM and Cinema off; the current EQ setting is left untouched.
- Background Music enables BGM and lets you choose My Room, Living Room, or
  Cafe. This maps to the headphone's `RoomSize` Small/Middle/Large values.
- Cinema enables the `UPMIX_CINEMA` audio effect and turns BGM off.
- The page shows the current EQ preset so the Standard/EQ relationship is clear.

## Multipoint and auto power off

- Multipoint is read from `GENERAL_SETTING2` (`MULTIPOINT_SETTING`) and written
  with `GENERAL_SETTING_SET_PARAM`. Turning it off can be refused while LDAC is
  available or two devices are connected; the UI reports the read-back result.
- Auto power off exposes only `WhenRemovedFromEars` and `Disable`. The UI reads
  and writes the wearing-detection path and does not show a “last selected”
  time.
- The headphone does not expose a connected-device list or an active-source
  switch over Tandem table1. Source switching is left to the headphone and the
  playing device.

## System tray

- **关闭行为** selects whether the window close button exits or hides the app.
- The tray icon menu provides **概览**, **聆听模式**, and **退出**.
- The preference is stored in `%LOCALAPPDATA%\Xm6Control\settings.json`.
- Tray icon creation is best effort; if the shell rejects it, close-to-tray is
  not used and the app closes normally.

## Current UI limitations

- Each action opens a new Tandem session; there is no persistent background session yet.
- Startup behavior is not implemented yet.
- NC/ASM supports NC, ASM, and Off, plus automatic ambient sound (with
  sensitivity) and voice focus.
