# Moonlit — Development Plan

> **Tagline:** Your screen. Your atmosphere.  
> **Version:** 2.0.0  
> **Platform:** Windows 10 / Windows 11  
> **Language:** C#  
> **Framework:** .NET 10 LTS  
> **UI Framework:** WPF  
> **Project Type:** Native Windows desktop application

---

## 1. Project Vision

Moonlit is a custom cinematic lock-screen experience for Windows, built with **C# + .NET 10 LTS + WPF**.

The goal is to create a beautiful, customizable lock-screen interface that plays user-selected MP4 videos as animated backgrounds, with a custom clock, date, and visual elements displayed over the video.

Think of the experience provided by Wallpaper Engine, but designed for a cinematic lock-screen interface rather than the desktop.

**The primary objective is displaying Moonlit's own interface in the lock-screen experience.**

Do not redesign the project around static wallpapers, animated GIFs, or Windows' built-in slideshow functionality.

---

## 2. Core Requirements

### 2.1 Cinematic Video Background

- Allow users to select a local MP4 video.
- Play the selected video as a full-screen background.
- Support seamless looping where practical.
- Preserve the video's aspect ratio and provide appropriate scaling (`UniformToFill` / `Uniform`).
- Handle unsupported codecs, missing files, and playback failures gracefully.
- Keep CPU and memory consumption reasonable.
- Provide a preview in the application settings.

### 2.2 Custom Clock and Date

Render our own clock and date above the video.

Requirements:

- Large, customizable digital clock.
- Current date and day of the week.
- Accurate local system time.
- Configurable 12-hour and 24-hour formats.
- Customizable typography, positioning, and alignment.
- A clean, cinematic visual style.
- Readable text against both bright and dark video backgrounds (with drop shadows and subtle readability gradients).

The clock must be rendered dynamically by Moonlit. It must not be baked into the video.

### 2.3 Customization

The architecture should allow future support for:

- Multiple clock layouts (Center, Bottom-Left, Top-Left, Bottom-Right).
- Font and text-size settings.
- Clock and date positioning.
- Text color and opacity.
- Optional seconds display.
- Video dimming or a subtle readability gradient overlay.
- Additional visual elements and animations.

Keep the initial implementation simple. Prioritize the video, clock, date, and display experience before adding advanced customization.

---

## 3. Lock-Screen Display Objective

Moonlit must prioritize displaying its cinematic interface when the user locks the computer.

Investigate and implement the most appropriate Windows integration strategy for presenting the Moonlit interface during the lock-screen experience.

Do not stop at the first discovery that Windows does not offer a straightforward public API for custom video lock screens. Treat the display mechanism as a core engineering problem that must be investigated and prototyped.

Evaluate viable implementation approaches, including:

- A dedicated full-screen WPF rendering window (`LockExperienceWindow`).
- Appropriate window ownership, display behavior (`Topmost`, borderless, multi-monitor aware), and lifecycle management.
- Integration with Windows session-lock and session-unlock events (`Microsoft.Win32.SystemEvents.SessionSwitch`, `WTSRegisterSessionNotification`).
- Supported Windows sign-in transitions (`LockWorkStation`).
- Alternative application integration mechanisms if the ordinary WPF window cannot appear over the required Windows surface.

The architecture should keep the rendering layer separate from the lock-session integration layer so that the display mechanism can be changed without rewriting the video player or clock.

### Important implementation principle

Focus on **how to display our own video and interface**, not on modifying Windows' built-in lock-screen image system.

Do not make registry feature overrides, protected lock-screen cache replacement, or GIF conversion the primary implementation strategy.

Do not assume that a normal application window automatically appears on the genuine Windows lock screen. Prototype the display behavior and document what actually works on the target Windows build.

If a proposed approach cannot provide the intended display behavior, investigate another approach and report the specific technical blocker instead of silently replacing the feature with a static image or ordinary screensaver.

---

## 4. Authentication and Session Behavior

Moonlit must integrate with Windows authentication rather than replacing it.

### Expected experience

1. The user locks the computer (or triggers Moonlit's lock experience / system lock).
2. Moonlit displays the cinematic interface (`LockExperienceWindow`).
3. The video plays behind Moonlit's custom clock and date.
4. The user presses a key, clicks, or otherwise requests sign-in.
5. Moonlit dismisses its visual layer or initiates the appropriate Windows sign-in transition (`LockWorkStation`).
6. Windows presents its genuine authentication experience, including Windows Hello, PIN, or password as configured.
7. After successful authentication, Moonlit stops displaying the lock interface and restores its appropriate application state.

### Security requirements

- Windows remains responsible for authentication.
- Never collect, store, or simulate Windows credentials.
- Never implement a fake password prompt.
- Never assume that displaying a full-screen window means the computer is securely locked.
- Never expose the desktop or bypass Windows authentication during a transition.
- Handle lock, unlock, suspend, resume, and user-session changes correctly.
- If secure integration cannot be established, report the limitation and do not claim the authentication flow is complete.

The goal is to achieve the intended user experience while preserving the genuine Windows security boundary.

---

## 5. Technical Stack and Architecture

- **Language:** C# 13+
- **Framework:** .NET 10 LTS (`net10.0-windows`)
- **Desktop UI:** WPF (XAML)
- **Configuration:** JSON (`System.Text.Json`)
- **Logging:** File-based logging (`%LOCALAPPDATA%\Moonlit\logs\moonlit.log`)
- **Video Playback:** WPF `MediaElement` with hardware acceleration
- **Testing:** xUnit (`tests/Moonlit.Tests`)

### Modular Architecture Components

1. **`VideoBackground`** (`Views/VideoBackground.xaml` / `Services/VideoService.cs`):
   - Media playback, looping, scaling, and error handling.
2. **`ClockOverlay`** (`Views/ClockOverlay.xaml`):
   - Live dynamic clock and date rendering, 12h/24h toggle, optional seconds, typography, readability gradient and drop shadow.
3. **`LockExperienceWindow`** (`Views/LockExperienceWindow.xaml`):
   - Dedicated full-screen composition hosting `VideoBackground` and `ClockOverlay`.
   - Listens for dismiss triggers (keypress, mouse move/click, escape), smoothly initiating sign-in / lock transition.
4. **`LockSessionManager`** (`Services/LockSessionManager.cs`):
   - Manages session lifecycle, hooks `SystemEvents.SessionSwitch`, detects lock/unlock, coordinates workstation locking via `LockWorkStation()`.
5. **`WindowsIntegration`** (`Integration/WindowsIntegration.cs`):
   - Win32 interoperability (`LockWorkStation`, `WTSRegisterSessionNotification`, monitor metrics, session state).
6. **`SettingsService`** (`Services/SettingsService.cs`):
   - Configuration loading and saving (`%LOCALAPPDATA%\Moonlit\settings.json`).
7. **`DiagnosticsService`** (`Services/DiagnosticsService.cs`):
   - Environment checks, session switch notification health, and integration test diagnostics.
8. **`MainWindow`** (`MainWindow.xaml`):
   - Settings dashboard with video picker, media preview, live clock preview, customization toggles, launch-at-sign-in, and lock experience testing.

---

## 6. Application Settings

The main application provides:

- Video file picker.
- Video preview with play, pause, and loop controls.
- Clock and date live preview with 12h/24h format, seconds toggle, alignment, and dimming level.
- Launch-at-sign-in setting.
- Lock-screen experience enable/disable setting.
- Instant "Test Lock Experience" action.
- Integration status & diagnostic information.
- Clear error messages when a feature is unsupported or unavailable.

### Persistence Paths

- Settings: `%LOCALAPPDATA%\Moonlit\settings.json`
- Logs: `%LOCALAPPDATA%\Moonlit\logs\moonlit.log`

---

## 7. Development Phases

### Phase 1 — Project Review and Display Prototype
- Inspect existing codebase, preserve useful services and tests.
- Implement the modular rendering components: `VideoBackground`, `ClockOverlay`, and `LockExperienceWindow`.
- Build the full-screen cinematic lock interface with dynamic clock, date, readability shadow, and looping MP4 playback.
- Implement user interaction handling (key press, mouse click) to dismiss and trigger secure Windows sign-in.

### Phase 2 — Lock-Session Integration
- Implement `LockSessionManager` listening to `SystemEvents.SessionSwitch`.
- Coordinate seamless transition between Moonlit's cinematic window and genuine Windows authentication (`LockWorkStation()`).
- Add settings for clock layout, 12h/24h format, seconds, video dimming, and enable/disable lock experience.
- Wire dashboard controls to allow instant testing and configuration.
- Comprehensive automated test coverage and documentation.
