# Moonlit — Project Plan

> **Tagline:** Your screen. Your atmosphere.  
> **Version:** 1.0.0  
> **Platform:** Windows 10 / Windows 11  
> **Language:** C#  
> **Framework:** .NET 10 LTS  
> **UI Framework:** WPF  
> **Project Type:** Native Windows desktop application

---

# 1. Project Scope

## 1.1 Objective

Moonlit aims to bring video playback to the genuine Windows lock screen.

Instead of displaying a static background, the application should allow users to select a local video and use it as the lock-screen background while preserving the native Windows clock, date, and standard authentication experience.

### The intended experience

1. The user selects a video in Moonlit.
2. The user configures and applies the desired settings.
3. The user presses `Win + L`.
4. Windows displays its genuine native lock screen.
5. The selected video plays behind the native clock and date instead of a static wallpaper.
6. The video loops continuously.
7. When the user unlocks the computer, Windows handles authentication normally.

**The genuine Windows lock screen is the primary objective. A conventional screen saver or custom imitation is not an acceptable substitute.**

## 1.2 Critical Technical Constraint

Windows does not expose a standard public API that allows an ordinary desktop application to replace the native lock-screen background with arbitrary video playback.

Consequently, achieving the intended experience cannot be assumed to be possible using WPF, a normal desktop window, or the Windows screen-saver mechanism alone.

The first development milestone must investigate whether a technically credible and acceptably safe implementation exists.

The project must distinguish between:

- The genuine Windows lock screen.
- The Windows sign-in interface.
- The ordinary desktop.
- A full-screen application window.
- A Windows screen saver.

Displaying video in one of the latter environments does not demonstrate that native lock-screen video playback works.

If the primary objective cannot be achieved through an acceptable approach, document the limitation and obtain approval before changing the product's scope.

## 1.3 Core Features

### A. Video Selection

- Select a local video through the native Windows file picker.
- Display the selected filename and path.
- Preview the video inside the Moonlit application.
- Validate that the file exists and is accessible.
- Detect unsupported formats or codecs where possible.
- Persist the selected video path.
- Handle files that have been moved, renamed, deleted, or made inaccessible.
- Support common video formats according to the chosen playback component's capabilities.

### B. Native Lock-Screen Video

This is the defining feature of Moonlit.

The intended integration must:

- Display video behind the genuine native Windows lock-screen elements.
- Preserve the native clock and date.
- Support continuous looping where technically possible.
- Preserve normal locking and unlocking.
- Avoid interfering with authentication.
- Operate reliably on the Windows versions explicitly supported by the project.

This feature is considered implemented only after a real proof of concept demonstrates the behavior on the genuine lock screen.

### C. Application Settings

The application must allow users to:

- Select and preview a video.
- Save configuration.
- Enable or disable optional launch-at-sign-in behavior.
- Enable or disable video looping where supported.
- Test the available integration without waiting for the lock-screen idle workflow.
- Inspect the current configuration status.

### D. Optional Startup

Provide a setting named `Launch at Windows Sign-in`.

- Allow users to enable or disable application startup.
- Use a suitable per-user Windows startup mechanism.
- Verify whether the requested startup configuration was applied.
- Report failures clearly.
- Avoid duplicate application instances.
- Do not automatically launch full-screen playback as a substitute for native lock-screen integration.

This setting controls the Moonlit configuration application, not Windows authentication or the lock screen itself.

### E. Status Indicator

Display a small, visually appealing status indicator on the dashboard.

- **Green — Ready:** All required configuration checks pass and the selected integration is verified as available.
- **Amber — Attention:** A non-critical issue or optional preference needs attention.
- **Red — Not Ready:** A required setting, video file, or integration prerequisite is missing or invalid.

The status indicator must represent actual verified state.

Selecting a video alone must not cause Moonlit to report that native lock-screen video playback is operational.

### F. Settings Persistence

Store application settings as JSON under the current user's application-data directory.

Persist:

- Selected video path.
- Video looping preference.
- Optional startup preference.

The application must create default settings when necessary and handle malformed configuration or write failures gracefully.

## 1.4 Out of Scope

Unless explicitly approved in a future version, exclude:

- Online video streaming.
- Cloud synchronization.
- User accounts.
- Video editing.
- Automatic video downloading.
- Complex playlists.
- Plugin systems.
- Wallpaper-engine functionality.
- Custom authentication interfaces.
- Credential collection or storage.
- A custom clock presented as the native Windows clock.
- A screen saver presented as successful native lock-screen integration.

## 1.5 Definition of Done

The primary objective is considered achieved only when:

1. The user can select a local video.
2. The selected video and preferences persist across application restarts.
3. Pressing `Win + L` displays the genuine Windows lock screen.
4. The native clock and date remain visible.
5. The selected video plays behind the native lock-screen elements.
6. Playback loops as expected.
7. Windows handles unlocking and authentication normally.
8. The application reports configuration health accurately.
9. Errors are handled without destabilizing Windows.
10. The implementation's compatibility and limitations are documented.

If the native integration cannot be achieved through an acceptable implementation, the project must report that limitation instead of claiming completion.

---

# 2. Technology Stack

## 2.1 Primary Technologies

| Component | Technology |
|---|---|
| Programming language | C# |
| Target framework | .NET 10 LTS |
| Project target | `net10.0-windows` |
| Desktop UI | WPF |
| UI markup | XAML |
| Settings format | JSON |
| JSON serialization | `System.Text.Json` |
| Initial preview playback | WPF `MediaElement` |
| Native Windows integration | Appropriate documented Windows APIs |
| Startup integration | Per-user Windows startup mechanism |
| Development environment | Antigravity or another .NET-compatible editor |

## 2.2 Development Principles

- Prefer built-in .NET functionality where practical.
- Keep the project small and understandable.
- Avoid unnecessary external dependencies.
- Separate configuration, playback, UI, and integration responsibilities.
- Avoid administrator privileges unless a justified operation requires them.
- Use asynchronous operations when appropriate.
- Validate user-selected paths.
- Handle errors explicitly.
- Do not hardcode machine-specific paths.
- Do not assume that a Windows API supports an undocumented operation.

## 2.3 Media Playback

Use WPF `MediaElement` initially for video preview within the settings application.

The preview component is not automatically suitable for playback on the genuine Windows lock screen.

The feasibility investigation must independently determine whether the chosen integration supports video rendering in the required Windows context.

If another playback technology is necessary, document the reason before introducing it.

## 2.4 Runtime and Deployment

Develop against .NET 10 LTS.

The final deployment approach must be selected after testing the application's Windows integration requirements. Consider framework-dependent or self-contained publishing based on the intended installation experience.

Do not require users to install the full Visual Studio IDE.

---

# 3. Project Structure

Keep the initial project structure straightforward.

```text
Moonlit/
├── Moonlit.sln
├── PLAN.md
├── README.md
├── .gitignore
│
├── docs/
│   └── FEASIBILITY.md
│
└── src/
    └── Moonlit/
        ├── Moonlit.csproj
        ├── App.xaml
        ├── App.xaml.cs
        ├── MainWindow.xaml
        ├── MainWindow.xaml.cs
        │
        ├── Services/
        │   ├── VideoService.cs
        │   ├── SettingsService.cs
        │   ├── StartupService.cs
        │   ├── StatusService.cs
        │   └── LockScreenService.cs
        │
        ├── Integration/
        │   └── LockScreenIntegration.cs
        │
        ├── Models/
        │   └── AppSettings.cs
        │
        ├── Assets/
        │   └── app-icon.ico
        │
        └── Resources/
            └── Styles.xaml
```

## 3.1 File Responsibilities

### App.xaml / App.xaml.cs

- Initialize the application.
- Load settings.
- Handle startup and application lifecycle.
- Prevent duplicate settings application instances where appropriate.
- Handle unexpected application errors.
- Initialize services.

### MainWindow.xaml / MainWindow.xaml.cs

- Implement the settings dashboard.
- Provide video selection and preview.
- Display configuration status.
- Expose application preferences.
- Allow users to save settings and test the available integration.

### VideoService.cs

- Validate selected files.
- Check path accessibility.
- Support video preview operations.
- Report validation and playback errors.

### SettingsService.cs

- Load and save JSON configuration.
- Create default settings.
- Handle missing or malformed configuration.
- Report persistence errors.

### StartupService.cs

- Apply the optional launch-at-sign-in preference.
- Verify the resulting startup configuration.
- Enable or disable the application's own startup entry.
- Report errors.

### StatusService.cs

- Evaluate required configuration checks.
- Distinguish critical failures from optional warnings.
- Generate concise status messages.
- Refresh status after configuration changes.
- Avoid reporting unverified integration success.

### LockScreenService.cs

- Coordinate the approved lock-screen integration strategy.
- Check whether required integration prerequisites are satisfied.
- Report integration availability and failures.
- Avoid implementing unverified behavior.

### LockScreenIntegration.cs

Contains the integration implementation only after the feasibility investigation identifies a credible approach.

Do not fill this file with a fake screen saver, desktop overlay, or custom lock-screen imitation.

If no acceptable integration is identified, document the result and stop before implementing an unsupported workaround.

### AppSettings.cs

Define the minimum settings model:

- `VideoPath`
- `LoopPlayback`
- `LaunchAtSignIn`

Additional properties may be introduced only when required by the verified implementation.

### Styles.xaml

Define shared colors, typography, spacing, and reusable WPF styles.

## 3.2 Configuration Storage

Store settings in:

```text
%LOCALAPPDATA%\Moonlit\
├── settings.json
└── logs\
```

Do not copy selected videos into the application's installation directory.

Store the selected path and validate it when needed.

Logging must be lightweight and must not expose credentials or unnecessary personal information.

## 3.3 Executable and Packaging Strategy

Do not assume that the `.scr` extension or a normal WPF executable provides native lock-screen integration.

The packaging strategy must follow the verified implementation.

Before release:

- Identify the required executable and dependencies.
- Verify the required Windows execution context.
- Test installation and removal.
- Test the implementation on a clean Windows environment.
- Document compatibility restrictions.

Do not create unnecessary executables or projects before the feasibility milestone is complete.

---

# 4. User Interface

## 4.1 Design Direction

Moonlit should feel like a polished, lightweight Windows utility.

Use:

- A dark graphite background.
- Subtle borders and rounded controls.
- Clean typography.
- Comfortable spacing.
- Minimal visual clutter.
- A compact status indicator.
- Clear messages and corrective guidance.

Suggested palette:

| Purpose | Color |
|---|---|
| Main background | `#111318` |
| Panel background | `#20242D` |
| Primary text | `#F4F5F7` |
| Secondary text | `#A6ACB8` |
| Accent | `#8AB4FF` |
| Ready | `#45D483` |
| Warning | `#F6C453` |
| Error | `#FF737B` |

These values are starting points and may be refined during implementation.

## 4.2 Dashboard Layout

### A. Header

Display:

- Moonlit application icon.
- Application name.
- Tagline: `Your screen. Your atmosphere.`
- Compact readiness indicator.

The indicator must contain a readable label in addition to its color.

### B. Video Selection

Display:

- Selected video filename.
- File path or a shortened readable path.
- File accessibility state.
- `Choose Video` button.
- `Preview` button.

When no video is selected, show a helpful empty state.

Use the native Windows file picker and filter common video formats.

### C. Preferences

Include:

**Launch at Windows Sign-in**

- Toggle to enable or disable the preference.
- Explain that it starts the settings application.
- Report whether the change was successfully applied.

**Loop Video**

- Toggle continuous playback where supported.
- Enabled by default.

### D. Integration Status

Display:

- A small status light.
- A concise status heading.
- A short explanation.
- A corrective action when appropriate.

Examples:

- `Ready — Integration verified`
- `Choose a video to continue`
- `Video file is unavailable`
- `Integration is not available`
- `Integration verification required`

The interface must distinguish between a configured video and a verified native lock-screen integration.

## 4.3 User Actions

Provide:

- **Choose Video:** Open the native file picker.
- **Preview:** Preview the selected video in the settings application.
- **Save Settings:** Persist configuration.
- **Test Integration:** Run a test supported by the verified implementation.
- **Refresh Status:** Recheck relevant configuration.

Do not label a conventional full-screen video window as a successful native lock-screen test.

Disable actions when prerequisites are missing and explain why.

## 4.4 Status Indicator Logic

### Green — Ready

Show green only when:

- A selected video exists and is accessible.
- Required settings are saved.
- Required integration checks pass.
- The implementation has been verified to support the intended native lock-screen behavior.

### Amber — Attention

Show amber when the application is usable but a non-critical issue exists.

Examples:

- Optional startup is disabled.
- A non-critical preference has not been applied.
- Additional integration verification is required.

### Red — Not Ready

Show red when a required condition fails.

Examples:

- No video is selected.
- The selected file is missing.
- Required settings cannot be saved.
- The native lock-screen integration is unavailable or invalid.

The status service must not report green solely because the selected video is valid.

---

# 5. Functional Requirements and Feasibility

## 5.1 Mandatory Feasibility Investigation

Before implementing the native integration, investigate:

1. Official Windows lock-screen customization capabilities.
2. Whether a documented API supports dynamic or video-based backgrounds.
3. Whether the native clock and date can remain visible during playback.
4. Whether video rendering is possible in the appropriate Windows execution context.
5. Whether a proposed implementation requires unsupported system modifications.
6. Compatibility across the intended Windows versions and editions.
7. Whether the approach preserves normal Windows authentication.

Record the results in `docs/FEASIBILITY.md`.

Use official documentation and reproducible experiments. Clearly distinguish documented capabilities from assumptions and experimental findings.

## 5.2 Proof of Concept

Create a minimal proof of concept only after identifying a credible integration path.

The proof of concept must demonstrate:

- The genuine Windows lock screen is displayed.
- The native clock and date remain visible.
- Video is rendered behind the native lock-screen elements.
- Playback continues as expected.
- Locking and unlocking work normally.
- Windows authentication remains intact.

A normal desktop window or a screen saver does not pass this test.

## 5.3 Decision Gate

After the feasibility investigation, choose one outcome.

**Outcome A — Feasible**

Implement the verified approach and proceed with the full application.

**Outcome B — Requires unsupported system modifications**

Document the risks, limitations, compatibility concerns, and maintenance costs. Do not perform these modifications automatically or without explicit project-owner approval.

**Outcome C — Not feasible through an acceptable approach**

Stop native integration development and present the evidence to the project owner.

Do not silently change the product into a conventional screen saver.

Any fallback requires explicit approval.

## 5.4 Security

- Preserve Windows authentication.
- Never collect, intercept, or store Windows credentials.
- Never disable or bypass the secure sign-in process.
- Never attempt to draw over the secure desktop.
- Do not weaken security settings.
- Avoid unnecessary administrator privileges.
- Make configuration changes reversible wherever possible.

---

# 6. Development Milestones

Implement incrementally. Each milestone must be reviewed before proceeding.

## Milestone 1 — Feasibility Research

- Research official Windows lock-screen APIs and customization capabilities.
- Document supported and unsupported behaviors.
- Identify credible integration options, if any.
- Create `docs/FEASIBILITY.md`.

**Acceptance criteria:** A documented technical assessment exists before committing to the integration architecture.

## Milestone 2 — Proof of Concept

- Implement the smallest possible test of the proposed integration.
- Verify native clock and date visibility.
- Verify video playback on the genuine lock screen.
- Test locking and unlocking.
- Record Windows-version limitations.

**Acceptance criteria:** The primary objective is demonstrated experimentally, or the project owner receives a clear report explaining why it cannot be achieved through an acceptable approach.

## Milestone 3 — Application Foundation

After approval of the integration strategy:

- Create the .NET 10 WPF project.
- Build the dashboard.
- Implement video selection and preview.
- Implement JSON settings persistence.
- Add basic validation and error messages.

**Acceptance criteria:** The application launches, selects a video, previews it, and restores saved settings after restarting.

## Milestone 4 — Integration, Startup, and Status

- Implement the approved integration.
- Add optional application startup.
- Implement configuration status checks.
- Connect status updates to the UI.
- Add actionable error messages.
- Ensure normal authentication remains intact.

**Acceptance criteria:** The app accurately reports whether the required configuration and integration are operational.

## Milestone 5 — Testing and Packaging

- Test clean installation and launch.
- Test settings persistence.
- Test invalid and missing files.
- Test startup enable/disable behavior.
- Test native locking and unlocking.
- Verify native clock visibility.
- Verify normal authentication.
- Test supported Windows configurations.
- Document installation and limitations.

**Acceptance criteria:** The application builds reproducibly and all advertised capabilities have been verified.

---

# 7. Testing Checklist

## Video

- [ ] Select a valid video.
- [ ] Preview the selected video.
- [ ] Enable and disable looping.
- [ ] Handle unsupported formats.
- [ ] Handle deleted or inaccessible files.
- [ ] Preserve settings across restarts.

## Native Lock Screen

- [ ] Press `Win + L`.
- [ ] Confirm the genuine Windows lock screen appears.
- [ ] Confirm the native clock and date remain visible.
- [ ] Confirm the selected video plays behind the native elements.
- [ ] Confirm playback loops.
- [ ] Confirm normal unlocking behavior.
- [ ] Confirm Windows authentication remains intact.

## Startup

- [ ] Enable application startup.
- [ ] Verify the startup configuration.
- [ ] Disable application startup.
- [ ] Verify that the application's startup entry is deactivated or removed.
- [ ] Confirm startup does not launch a fake lock screen or full-screen video overlay.

## Status and Settings

- [ ] Show green only when all required checks pass.
- [ ] Show amber for appropriate warnings.
- [ ] Show red for required configuration failures.
- [ ] Display useful diagnostic messages.
- [ ] Save and reload settings.
- [ ] Handle malformed configuration.
- [ ] Report persistence failures.

## Packaging and Security

- [ ] Build in Release mode.
- [ ] Verify required dependencies.
- [ ] Test on a clean Windows environment.
- [ ] Verify installation and removal.
- [ ] Verify that authentication is never bypassed.
- [ ] Document compatibility limitations.

---

# 8. Rules for AI-Assisted Development

1. Treat the genuine Windows lock screen as the primary objective.
2. Do not substitute a screen saver for the requested functionality.
3. Do not create a fake lock screen or custom clock and call it native integration.
4. Complete the feasibility investigation before implementing integration.
5. Use official documentation and reproducible tests.
6. Keep the architecture simple.
7. Avoid unnecessary dependencies and additional projects.
8. Build and test incrementally.
9. Fix compilation errors before continuing to the next milestone.
10. Do not claim that an API supports functionality without evidence.
11. Preserve Windows authentication and system security.
12. Do not introduce unsupported system modifications without explicit approval.
13. Report genuine platform limitations clearly.
14. If the primary objective cannot be achieved acceptably, stop and request approval before implementing an alternative.

---

# 9. Final Deliverable

The intended deliverable is Moonlit: a native Windows application that allows users to select a local video and play it behind the genuine Windows lock-screen clock and date while preserving normal Windows locking and authentication.

The first priority is proving the core capability. The settings dashboard, startup option, and readiness indicator follow once the integration strategy is established.

A conventional screen saver, desktop overlay, or custom lock-screen imitation does not satisfy the primary objective.

**First prove the native lock-screen integration. Then build the application around what has actually been verified.**
