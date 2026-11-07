# Moonlit

> **Your screen. Your atmosphere.**  
> A custom cinematic lock-screen experience for Windows built with C# and .NET 10 LTS.

---

## 🌟 Overview

Moonlit brings a customizable, cinematic lock-screen experience to Windows. Inspired by Wallpaper Engine but tailored for lock-screen atmospheres, Moonlit renders high-definition MP4 video backgrounds with a dynamic, typography-rich digital clock and date overlay, while seamlessly coordinating with genuine Windows authentication (PIN, Windows Hello, and passwords).

---

## 🚀 Key Features

### ⚡ Instant Startup Experience (Priority 1 Window Pipeline)
- **Zero-Delay Task Scheduler Launch**: Moonlit registers an interactive "At Log On" task via the Windows Task Scheduler (`Schedule.Service` COM API) with zero artificial startup delay (`TASK_TRIGGER_LOGON` + `TASK_LOGON_INTERACTIVE_TOKEN`).
- **Immediate Frame Display**: The full-screen borderless window and live ticking clock render in milliseconds upon logon before any video decoding or disk probing takes place.
- **Atmospheric Startup Canvas**: Employs an instant dark radial atmospheric canvas (`#181F2C` → `#0F131C` → `#06080B`) visible on frame 1 to eliminate jarring black flashes while DirectShow / Media Foundation initializes.
- **Asynchronous Video Loading & Smooth Crossfade**: The video player initializes in the background and smoothly crossfades in over 250ms as soon as the first frame is ready.
- **Graceful Migration**: Automatically detects and migrates legacy `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` entries into Task Scheduler on launch.

### 🎬 Cinematic Video Background
- **Native MP4 Playback**: Full-screen video rendering with aspect ratio preservation (`UniformToFill`).
- **Mute Audio Option**: Toggleable setting to prevent video sound from playing during lock-screen presentation (silent atmosphere).
- **Seamless Looping**: Automatic restart when playback reaches the end.
- **Resource Efficient**: Low CPU and memory footprint with hardware-accelerated WPF media decoding.
- **In-App Preview**: Integrated media player in settings with Play, Pause, and status feedback.

### ⏰ Dynamic Custom Clock & Date
- **Live System Synchronization**: Smooth, accurate time updates with sub-second accuracy.
- **12-Hour / 24-Hour Formats**: Configurable AM/PM or 24-hour time display.
- **Optional Seconds Display**: Toggleable `:ss` display for precision.
- **Multiple Alignments**: Position clock at `Center`, `Bottom-Left`, `Top-Left`, or `Bottom-Right`.
- **Contrast & Readability**: Atmospheric vignette, drop shadows, and an adjustable readability dimming slider (0% to 70%) ensuring text clarity against both light and dark video backgrounds.

### 🔒 Windows Authentication Transition & Kiosk Overlay
- **Genuine Windows Security**: Moonlit is a post-sign-in cinematic experience, not a credential stealer. It never prompts for, stores, or intercepts Windows system credentials.
- **Interactive PIN Prompt**: Optional cinematic Moonlit PIN screen ("1080" default test PIN) that smoothly slides the clock upwards and blurs the background video on keypress.
- **Automatic Inactivity Revert**: Reverts back to clock and video playback if left idle on the PIN screen for 60 seconds.
- **Windows Workstation Locking**: Directly triggers native `LockWorkStation` so Windows presents its official authentication surface (Windows Hello, PIN, password).
- **Session Lifecycle Coordination**: Subscribes to `SystemEvents.SessionSwitch` to automatically handle session locking and unlocking.

### 🔔 System Tray & Seamless Activation
- **Taskbar Notification Area Icon**: Moonlit maintains a resident icon in the Windows System Tray with instant access.
- **Left-Click / Double-Click**: Instantly brings the Settings Dashboard to the front.
- **Right-Click Context Menu**:
  - `Open Settings Dashboard`
  - `Lock PC with Moonlit (Win + L)`
  - `Exit Moonlit` (cleanly and immediately terminates all background processes, hooks, and services).
- **Single-Instance IPC Activation**: Attempting to launch Moonlit when it is already running automatically signals the running instance over IPC to restore and focus the dashboard—no duplicate errors or frozen instances.

### ⚙️ Preferences & Persistence
- **Instant Test Actions**:
  - `Test Lock Experience`: Full-screen preview dismissed back to the desktop on keypress/click.
  - `Lock PC with Moonlit`: Full-screen experience transitioning immediately into the genuine Windows lock screen upon dismissal.
- **Startup Registration**: Verified registration in Windows Task Scheduler (At Log On, zero delay).
- **Human-Readable Settings**: JSON configuration stored under `%LOCALAPPDATA%\Moonlit\settings.json`.
- **Diagnostic Logging**: Non-sensitive logging to `%LOCALAPPDATA%\Moonlit\logs\moonlit.log`.

---

## 🛡️ Windows 11 Home Compliance

Moonlit is specifically designed to run reliably on **Windows 11 Home** without destabilizing the operating system:
- **No Windows Shell Launcher**: Moonlit does not replace or modify `explorer.exe`.
- **No Winlogon Hijacking**: Winlogon registry keys and Windows authentication Gina/Credential Providers remain completely untouched.
- **No Process Termination**: Explorer is never killed or hidden to fake a lock screen.
- **Standard User Elevation**: Everyday startup management and Task Scheduler registration run entirely under standard user permissions (`TASK_LOGON_INTERACTIVE_TOKEN`).
- **One-Click Restoration**: Moonlit provides a 1-click restore button that completely cleans all optional registry policy overrides (`DisableLockWorkstation`, `NoLockScreen`, `DoNotDisplayLockScreen`) and restores Windows 11 defaults.

---

## ⏱️ Startup Performance Diagnostics

Moonlit includes built-in startup diagnostics to continuously evaluate instant launch responsiveness:
```text
Startup Performance Metrics:
  • Process Started:              16:20:15.120
  • Window Created:               +45 ms
  • Window First Displayed:       +78 ms (Visible to user with live clock)
  • Video Initialization Started: +82 ms
  • First Video Frame Rendered:   +240 ms (Seamless playback crossfade)
```

---

## 🛠️ Architecture & Project Structure

```text
Moonlit/
├── Moonlit.sln                       # Solution file
├── PLAN.md                           # Master development plan
├── README.md                         # Project documentation
│
├── docs/
│   └── FEASIBILITY.md                # Windows Lock Screen platform research
│
├── src/
│   └── Moonlit/
│       ├── Moonlit.csproj            # .NET 10 LTS WPF project (net10.0-windows)
│       ├── App.xaml / App.xaml.cs    # Single-instance mutex, immediate window launch, IPC
│       ├── MainWindow.xaml / cs      # Settings dashboard, video picker, clock controls
│       │
│       ├── Views/
│       │   ├── VideoBackground.xaml  # Asynchronous video player with atmospheric canvas
│       │   ├── ClockOverlay.xaml     # Modular dynamic clock & date UserControl with dimming
│       │   └── LockExperienceWindow.xaml # Full-screen cinematic window & PIN/dismiss transitions
│       │
│       ├── Services/
│       │   ├── VideoService.cs       # Video file validation and format inspection
│       │   ├── SettingsService.cs    # JSON configuration storage & logging
│       │   ├── StartupService.cs     # Windows Task Scheduler registration & registry migration
│       │   ├── StartupDiagnosticsTracker.cs # High-resolution startup timestamp instrumentation
│       │   ├── StatusService.cs      # Three-tier readiness evaluation (Green/Amber/Red)
│       │   ├── LockSessionManager.cs # SessionSwitch events & window lifecycle coordination
│       │   ├── DiagnosticsService.cs # Screen metrics, OS build, and environment checks
│       │   └── TrayIconService.cs    # Native Windows System Tray icon & IPC coordination
│       │
│       ├── Integration/
│       │   └── WindowsIntegration.cs # Native LockWorkStation, screen bounds & policies interop
│       │
│       ├── Models/
│       │   ├── AppSettings.cs        # Preferences model (video, clock, dimming, startup)
│       │   └── StatusInfo.cs         # Status level model
│       │
│       ├── Resources/
│       │   └── Styles.xaml           # Dark graphite color palette & control styling
│       │
│       └── Assets/
│           └── app-icon.ico          # Moonlit icon
│
└── tests/
    └── Moonlit.Tests/                # 44 automated xUnit unit tests
```

---

## 💻 Building and Running

### Prerequisites
- Windows 10 (1809+) or Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Build Solution
```powershell
dotnet build Moonlit.sln -c Release
```

### Run Unit Tests
```powershell
dotnet test tests/Moonlit.Tests/Moonlit.Tests.csproj -c Release
```

### Launch Application
```powershell
dotnet run --project src/Moonlit/Moonlit.csproj
```
or execute the compiled binary directly:
```text
src\Moonlit\bin\Release\net10.0-windows\Moonlit.exe
```

### Command-Line Arguments & Shortcuts
| Argument | Description |
|---|---|
| `--lock` or `-l` | Instantly displays Moonlit's full-screen cinematic lock screen. If Moonlit is already active, signals it over IPC in microseconds. |
| `--boot` or `-b` | Launch mode for Windows Startup. Plays the cinematic lock screen immediately upon logon and transitions securely to Windows PIN/Password on dismissal. |
| `--setup-policies` | Executes an elevated one-click configuration to enable `Win + L` direct interception and bypass the Windows lock curtain (`NoLockScreen`). |

### Global Shortcuts & Navigation
- **`Win + L`**: Seamlessly launches Moonlit's cinematic lock screen when system policy is configured.
- **`Ctrl + Alt + L`**: Universal global shortcut, works out of the box on all Windows configurations.
- **`Ctrl + Shift + L`**: Alternative quick-lock shortcut.
- **Desktop & Start Menu Shortcuts**: Click "Create Desktop & Start Shortcuts" in Settings to place 1-click lock icons anywhere.

---

## 📄 License

MIT License. Designed with care for modern Windows.
