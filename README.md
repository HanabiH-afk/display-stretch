# DisplayStretch

A tiny Windows tray tool that switches your display to a **4:3 resolution with true full-screen stretching** (no letterboxing / black bars), and back to your native resolution, with a single key press.

Built for games like **VALORANT** that benefit from a stretched 4:3 image (wider player models, classic stretched look), while leaving your normal desktop resolution untouched.

> Windows 10 / 11 · .NET Framework 4.x (preinstalled) · no admin rights required · no drivers, no services, no telemetry.

## Features

- **One-key toggle** — press <kbd>F9</kbd> to switch `native 16:9` ⇄ `4:3 stretched`; press again to go back.
- **True stretch, not letterbox** — sets the display mode with `DMDFO_STRETCH` (`DM_DISPLAYFIXEDOUTPUT`), then verifies via `QueryDisplayConfig` that the path scaling really is `STRETCHED` before reporting success.
- **Remembers your native resolution** — first run captures the current mode; the desktop is restored exactly as it was.
- **Tray menu** — toggle, pick other 4:3 resolutions (1440×1080, 1024×768, 1152×864, …), enable *start with Windows*, read help, or quit (which restores the native mode).
- **INI config** — target resolution / refresh rate / hotkey are stored next to the exe.
- **CLI mode** — `--toggle`, `--4x3`, `--restore`, `--status` for shortcuts, stream decks and scripts.
- **Log file** — every switch is logged with old → new mode and the verified scaling mode.

## Quick start

1. Download `DisplayStretch.exe` (or build it from source, see below).
2. Run it. A notification confirms the hotkey is registered; a small blue icon appears in the notification area (Windows 11 may hide it under `^`).
3. Press <kbd>F9</kbd> — the screen switches to 1280×960 @ highest refresh rate, stretched to fill the panel.
4. In the game: resolution **1280×960**, display mode **Fullscreen** (aspect ratio 4:3).
5. Press <kbd>F9</kbd> again to return to your desktop resolution.

Right-click the tray icon for more options. *Quit (restore native)* always restores the original mode.

## Configuration

`DisplayStretch.ini` is created next to the exe on first run:

```ini
TargetWidth=1280      ; 4:3 target width
TargetHeight=960
TargetRefresh=0       ; 0 = highest supported refresh rate
NativeWidth=1920      ; captured automatically
NativeHeight=1080
NativeRefresh=240
NativeDevice=\\.\DISPLAY6
Hotkey=F9             ; change to F8 / F10 / ScrollLock … then restart
```

If the target mode is unsupported, the tool falls back through a list of common 4:3 modes
(1280×960 → 1440×1080 → 1024×768 → 1152×864 → 1600×1200 → 1920×1440 → 2048×1536) and reports what happened in the log/notification.

## Command line

```bat
DisplayStretch.exe --toggle    :: toggle between native and 4:3
DisplayStretch.exe --4x3       :: switch to 4:3
DisplayStretch.exe --restore   :: restore native mode
DisplayStretch.exe --status    :: write current state to the log only
```

## Build from source

Requires only the C# compiler shipped with .NET Framework (no Visual Studio, no NuGet):

```bat
build.bat
```

or manually:

```bat
csc /nologo /target:winexe /codepage:65001 /win32icon:app.ico ^
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
    /out:DisplayStretch.exe DisplayStretch.cs
```

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| 4:3 mode has black bars instead of stretching | The GPU driver overrides scaling. NVIDIA Control Panel → *Display* → *Adjust desktop size and position* → scaling mode **Full-screen**, tick *Override the scaling mode set by games and programs*. AMD: Radeon Software → Display → Scaling Mode **Full Panel**. Then press F9 again. |
| Hotkey does nothing | Check the tray icon exists and the log says the hotkey registered. Some exclusive-fullscreen games swallow hotkeys — Alt+Tab out and press F9. If another app owns F9, change `Hotkey=` in the ini and restart. |
| Tray icon not visible | Windows 11 hides new icons under `^` in the taskbar; drag it onto the taskbar. |
| After reboot the screen is still 4:3 | Windows remembers the last mode. Run the tool and press F9, or `DisplayStretch.exe --restore`. Enable *Start with Windows* in the tray menu. |
| SmartScreen blocks the exe | It is unsigned. *More info* → *Run anyway*, or build it yourself from source. |

The tool only writes to `HKCU\...\Run` when you enable autostart, and otherwise leaves the registry alone. It changes display settings the same way the Windows *Display settings* page does.

## Notes

- Stretched 4:3 is a display-mode change: every application on that monitor is affected while it is active.
- Tested on a 1920×1080 @ 240 Hz display; other resolutions work as long as the monitor exposes a 4:3 mode.

## License

MIT — see [LICENSE](LICENSE).
