# Moonlit

> **Your screen. Your atmosphere.**  
> A custom cinematic lock-screen experience for Windows built with C# and .NET 10 LTS.

---

## 🌟 Overview

Moonlit brings a customizable, cinematic lock-screen experience to Windows. Inspired by Wallpaper Engine but tailored for lock-screen atmospheres, Moonlit renders high-definition MP4 video backgrounds with a dynamic, typography-rich digital clock and date overlay, while seamlessly coordinating with genuine Windows authentication (PIN, Windows Hello, and passwords).

---

## 🚀 Key Features

### 🎬 Cinematic Video Background
- **Native MP4 Playback**: Full-screen video rendering with aspect ratio preservation (`UniformToFill`).
- **Seamless Looping**: Automatic restart when playback reaches the end.
- **Resource Efficient**: Low CPU and memory footprint with hardware-accelerated WPF media decoding.
- **In-App Preview**: Integrated media player in settings with Play, Pause, and status feedback.

### ⏰ Dynamic Custom Clock & Date
- **Live System Synchronization**: Smooth, accurate time updates with sub-second accuracy.
- **12-Hour / 24-Hour Formats**: Configurable AM/PM or 24-hour time display.
- **Optional Seconds Display**: Toggleable `:ss` display for precision.
- **Multiple Alignments**: Position clock at `Center`, `Bottom-Left`, `Top-Left`, or `Bottom-Right`.
- **Contrast & Readability**: Atmospheric vignette, drop shadows, and an adjustable readability dimming slider (0% to 70%) ensuring text clarity against both light and dark video backgrounds.

### 🔒 Seamless Windows Authentication Transition
- **Genuine Windows Security**: Moonlit never prompts for, stores, or intercepts credentials.
- **Smooth Dismissal**: Press any key, click the mouse, or move the cursor to dismiss the cinematic interface.
- **Workstation Locking**: Directly triggers `LockWorkStation` so Windows presents its official authentication surface (Windows Hello, PIN, password).
- **Session Lifecycle**: Subscribes to `SystemEvents.SessionSwitch` to automatically handle session locking and unlocking.

### ⚙️ Preferences & Persistence
- **Instant Test Actions**:
  - `Test Lock Experience`: Full-screen preview dismissed back to the desktop on keypress/click.
  - `Lock PC with Moonlit`: Full-screen experience transitioning immediately into the genuine Windows lock screen upon dismissal.
- **Launch at Windows Sign-in**: Automatic startup entry in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` with verification.
- **Human-Readable Settings**: JSON configuration stored under `%LOCALAPPDATA%\Moonlit\settings.json`.
- **Diagnostic Logging**: Non-sensitive logging to `%LOCALAPPDATA%\Moonlit\logs\moonlit.log`.

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
│       ├── App.xaml / App.xaml.cs    # Single-instance mutex, global exception logging
│       ├── MainWindow.xaml / cs      # Settings dashboard, video picker, clock controls
│       │
│       ├── Views/
│       │   ├── VideoBackground.xaml  # Modular video player UserControl with looping
│       │   ├── ClockOverlay.xaml     # Modular dynamic clock & date UserControl with dimming
│       │   └── LockExperienceWindow.xaml # Full-screen cinematic window & dismiss transitions
│       │
│       ├── Services/
│       │   ├── VideoService.cs       # Video file validation and format inspection
│       │   ├── SettingsService.cs    # JSON configuration storage & logging
│       │   ├── StartupService.cs     # Windows sign-in startup registry integration
│       │   ├── StatusService.cs      # Three-tier readiness evaluation (Green/Amber/Red)
│       │   ├── LockSessionManager.cs # SessionSwitch events & window lifecycle coordination
│       │   └── DiagnosticsService.cs # Screen metrics, OS build, and environment checks
│       │
│       ├── Integration/
│       │   └── WindowsIntegration.cs # Native LockWorkStation and screen bounds interop
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
    └── Moonlit.Tests/                # 35 automated xUnit unit tests
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
dotnet test Moonlit.sln -c Release
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
| `--boot` or `-b` | Launch mode for Windows Startup. Plays the cinematic lock screen on startup (if enabled) and transitions securely to Windows PIN/Password on dismissal. |
| `--setup-policies` | Executes an elevated one-click configuration to enable `Win + L` direct interception and bypass the Windows lock curtain (`NoLockScreen`). |

### Global Shortcuts & Navigation
- **`Win + L`**: Seamlessly launches Moonlit's cinematic lock screen when system policy is configured.
- **`Ctrl + Alt + L`**: Universal global shortcut, works out of the box on all Windows configurations.
- **`Ctrl + Shift + L`**: Alternative quick-lock shortcut.
- **Desktop & Start Menu Shortcuts**: Click "Create Desktop & Start Shortcuts" in Settings to place 1-click lock icons anywhere.

---

## 📄 License

MIT License. Designed with care for modern Windows.
