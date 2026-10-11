# NetSet

A small Windows tray utility for switching a network adapter between saved IP presets.

![NetSet main window](docs/screenshot.png)

![New preset dialog with suggestions from the current adapter](docs/screenshot-editor.png)

## Features

- Pick any network adapter and see its live status, IP, mask, gateway, DNS and MAC.
- Save presets: DHCP, or static IP with mask, gateway and up to two DNS servers.
- New presets start with the selected adapter's current settings as gray suggestions: press **Tab** in an empty field to accept one, or just type to replace it.
- Mark presets as **favorites** (click the ★) and they appear when you right-click the tray icon, so switching is two clicks.
- Import / export presets as JSON, with a merge-or-replace prompt on import.
- **Run at startup** option in the tray menu.
- Dark, modern UI and a pre-2000 style "networked computers" icon, drawn in code.

## Tray menu

- Left-click the tray icon to open the window.
- Right-click for the current adapter, a "Change adapter" submenu, your favorite presets, Open, Run at startup and Exit.
- Closing the window hides it back to the tray; use **Exit** in the tray menu to quit.

## Build

Requires the .NET 8 SDK (or newer) on Windows.

```powershell
dotnet publish -c Release -o publish
```

This produces a single self-contained `publish\NetSet.exe` (about 68 MB, no .NET runtime needed to run it).

## Notes

- NetSet changes adapter settings through `netsh`, which needs administrator rights. The exe requests elevation, so you will see a UAC prompt when you launch it.
- **Run at startup** creates a scheduled task named `NetSet` (at logon, highest privileges) rather than a registry Run key, because Windows skips elevated apps in Run keys. If you move the exe, toggle the option off and on again.
- Presets and the last-used adapter are stored in `%AppData%\NetSet\settings.json`.
- `NetSet.exe --show` opens the window at launch. `NetSet.exe --export-icon <file>` regenerates the `.ico` from the drawing code.

## Project layout

| File | Purpose |
| --- | --- |
| `Program.cs` | Entry point, single-instance guard |
| `TrayApp.cs` | Tray icon, context menu, applying presets |
| `MainForm.cs` / `PresetEditor.cs` | Main window and preset editor |
| `Controls.cs` / `Theme.cs` | Custom dark controls and palette |
| `NetworkConfig.cs` | Reads adapter info, applies settings via `netsh` |
| `StartupTask.cs` | Run-at-startup scheduled task |
| `Preset.cs` | Preset model, settings store, import/export |
| `IconFactory.cs` | The pixel-art tray and app icon |
