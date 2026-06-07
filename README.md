# MatrixSaver

A Windows 10/11 screensaver built on the [Rezmason/matrix](https://github.com/Rezmason/matrix) WebGL digital rain engine, wrapped in a native C# / WebView2 shell.

Unlike Electron-based screensaver wrappers, this uses the Win32 screensaver API directly — `SPI_SETSCREENSAVERRUNNING`, `WS_EX_TOPMOST | WS_EX_NOACTIVATE` at window creation time, and global low-level input hooks — so the taskbar is properly suppressed and multi-monitor coverage is reliable.

## Features

- All 12 Rezmason film versions: Classic, Operator, Resurrections, Megacity, Nightmare, Paradise, Palimpsest, Morpheus, Bugs, 3D, Trinity, Neomatrixology
- Multi-monitor: one fullscreen window per display, launched simultaneously
- Full settings UI (30+ parameters) with live preview
- Preview pane embedded correctly in Windows Screen Saver Settings
- Zero runtime dependencies beyond WebView2 (pre-installed on Windows 10 1803+ and Windows 11)

## Requirements

- **Windows 10 (1803 or later)** or **Windows 11**
- **WebView2 Runtime** — already installed on all modern Windows machines as part of the OS

No .NET runtime install required — the exe is fully self-contained.

## Installation

1. Download the latest release zip and extract it anywhere
2. Run `install.bat` as Administrator
   - Copies the full folder to `C:\Program Files\MatrixSaver\`
   - Registers the screensaver in the Windows registry
3. Open Screen Saver Settings (right-click desktop → Personalize → Lock screen → Screen saver) to configure and preview

**Or:** Right-click `MatrixSaver.scr` → **Install** (works when the whole extracted folder stays together).

To uninstall, run `uninstall.bat` as Administrator.

## Building from Source

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download).

```bash
git clone --recurse-submodules https://github.com/youruser/MatrixSaver
cd MatrixSaver
bash build.sh
```

Output lands in `dist/`. The build produces a single self-contained `MatrixSaver.exe` (also copied as `MatrixSaver.scr`) alongside the `matrix/` engine folder.

On Linux/macOS, cross-compilation to Windows x64 works out of the box via the .NET SDK.

## How it works

| Layer | Technology |
|---|---|
| Digital rain engine | [Rezmason/matrix](https://github.com/Rezmason/matrix) — WebGL/WebGPU |
| Renderer | Chromium via Microsoft WebView2 |
| Shell | C# WinForms, .NET 8, self-contained |
| Screensaver API | `SPI_SETSCREENSAVERRUNNING`, `WS_EX_TOPMOST \| WS_EX_NOACTIVATE` |
| Input detection | `SetWindowsHookEx(WH_KEYBOARD_LL \| WH_MOUSE_LL)` — global hooks |
| Preview embedding | Win32 `SetParent` P/Invoke |
| Settings UI | HTML/CSS/JS hosted in a second WebView2 instance |
| Config | `%APPDATA%\MatrixSaver\config.json` |

### Screensaver flags

| Flag | Behaviour |
|---|---|
| `/s` | Fullscreen on all monitors simultaneously; exit on any input |
| `/p <hwnd>` | Embedded preview in the Windows Screen Saver Settings monitor graphic |
| `/c` | Settings window |

## Credits

Digital rain engine by **[Rezmason](https://github.com/Rezmason)** — MIT License.  
WebView2 native shell by Eric Warthan — MIT License.

## License

MIT — see [LICENSE](LICENSE).  
The `matrix/` submodule is separately MIT licensed by Rezmason.
