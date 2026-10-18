# Moonlit — Feasibility Investigation Report

> **Document:** `docs/FEASIBILITY.md`  
> **Author:** Moonlit Engineering Team  
> **Date:** October 2026  
> **Target OS:** Windows 10 (1809+) / Windows 11 (all versions including 24H2/25H2+)  
> **Status:** Completed — Decision Gate Evaluation

---

## 1. Executive Summary

Moonlit's primary objective is to bring **seamless video playback to the genuine Windows lock screen**, playing behind the native clock and date, while preserving the authentic Windows authentication mechanism and secure desktop boundaries.

Per **Section 5.1** and **Section 8** of `PLAN.md`, this mandatory feasibility investigation evaluated the Windows desktop architecture, documented Windows APIs, session isolation security models, and potential integration vectors.

### Key Finding
**There is no public, documented, or supported Windows API that allows an application to host arbitrary video playback (MP4, MKV, WebM, etc.) on the genuine Windows lock screen behind the native clock and date.**

Windows lock screen UI (managed by `LogonUI.exe` / `LockApp.exe`) is hosted on the isolated `Winlogon` desktop within the interactive window station (`WinSta0`). The native lock-screen background rendering pipeline exclusively accepts static images or an image slideshow; it does not host a video playback engine (`MediaElement` or `MediaPlayerElement`).

Alternative techniques exist in the wild, notably **lock-screen cache image replacement with animated GIFs** and **undocumented FeatureManagement overrides**. However, these methods:
1. Do not achieve genuine native video container playback (they require video-to-GIF conversion).
2. Require administrator elevation to hijack ownership and ACLs on protected system directories (`C:\ProgramData\Microsoft\Windows\SystemData`).
3. Require tampering with undocumented Windows kernel feature management flags (`HKLM\SYSTEM\CurrentControlSet\Control\FeatureManagement\Overrides`) to restore GIF animation on Windows 11 Build 26200+ (24H2/25H2).
4. Suffer from severe performance degradation, color quantization (256 colors), file-size inflation, and update fragility.

Under `PLAN.md` Section 5.3, this places the direct lock-screen video feature into **Outcome B / Outcome C**.

---

## 2. Windows Lock Screen Architecture & Security Model

### 2.1 Window Stations and Desktop Isolation
Windows isolates interactive sessions through window stations and desktops:
- **Interactive Window Station (`WinSta0`):** The only window station capable of interacting with the physical display, keyboard, and mouse.
- **Desktops within `WinSta0`:**
  - `Default` desktop: Where user shell processes (`explorer.exe`), standard Win32/WPF applications, and media players execute under user credentials.
  - `Winlogon` desktop: The secure desktop where `LogonUI.exe`, Credential Providers, UAC elevation dialogs, and the Lock Screen execute.
  - `Screen-saver` desktop: An isolated desktop where legacy screen savers (`.scr`) run.

Windows kernel security enforces strict desktop boundaries:
- A user process running on the `Default` desktop cannot draw onto, inject windows into, or layer surfaces over the `Winlogon` desktop.
- Any attempt to position a window on top of the lock screen from a standard process violates Windows security isolation (UIPI / Mandatory Integrity Control).

### 2.2 Lock Screen Hosting Component
- On Windows 10 and Windows 11, the lock screen interface is rendered via modern XAML/DirectComposition within the system shell context (`LockApp.exe` or `LogonUI.exe`).
- The XAML markup defining the lock screen background utilizes an `Image` control bound to the user's cached lock screen asset.
- No media playback pipeline (such as Media Foundation, DirectShow, or `MediaPlayerElement`) is instantiated on the lock screen background surface.

---

## 3. Evaluation of Technical Vectors

| Vector / Approach | Mechanism | Native Clock Preserved? | True Video Playback? | Supported by Microsoft? | Evaluation / Limitations |
|---|---|---|---|---|---|
| **1. Official WinRT API** (`Windows.System.UserProfile.LockScreen`) | `SetImageFileAsync` / `SetImageStreamAsync` | Yes | ❌ No (Static only) | ✅ Yes | Strictly limited to static image formats (JPEG, PNG). Passing video files results in `ArgumentException` or decoding failure. |
| **2. Group Policy / MDM / Registry** | `LockScreenImage` policy | Yes | ❌ No (Static only) | ✅ Yes | Only accepts a path to a static picture. |
| **3. Lock Screen Cache Injection (GIF)** | Overwrite `SystemData\<UserSID>\ReadOnly\LockScreen_*\LockScreen.jpg` with GIF frames | Yes | ⚠️ Partial (GIF animation, not video) | ❌ No (Requires ACL hijacking) | Requires converting video to GIF. Requires admin rights to modify `SystemData` ACLs. Blocked by Windows 11 build 26200+ unless feature `38943831` is disabled in the registry. |
| **4. Custom Credential Provider** | ICredentialProvider COM implementation | ❌ No (Login screen only) | ❌ No | ⚠️ Yes, for auth only | Credential Providers only render user tiles on the sign-in screen, not the lock screen background. Microsoft security rules prohibit heavy media rendering. |
| **5. Windows Screen Saver (`.scr`)** | Executable running on `Screen-saver` desktop | ❌ No (Native clock missing) | ✅ Yes | ✅ Yes (Legacy) | Explicitly rejected by `PLAN.md` Section 1.1 & 1.4. A screen saver is dismissed when `Win + L` is pressed and does not preserve the genuine native clock. |
| **6. Desktop Window Overlay** | Fullscreen topmost WPF window | ❌ No (Imitation clock) | ✅ Yes | ❌ No (Fake lock screen) | Explicitly rejected by `PLAN.md` Section 1.1 & 8. Cannot survive switching to `Winlogon` desktop. |

---

## 4. Deep-Dive: The Cache Injection Approach (LockscreenGif Technique)

Because the GIF cache injection technique is currently the only known mechanism allowing motion behind the native Windows 10/11 lock-screen clock, a rigorous evaluation was conducted:

### How it operates
1. **User Personalization State**: The user selects `Picture` mode in `Settings > Personalization > Lock screen`.
2. **Cache Initialization**: Windows caches the selected wallpaper in:
   ```text
   C:\ProgramData\Microsoft\Windows\SystemData\<UserSID>\ReadOnly\LockScreen_Z\LockScreen.jpg
   ```
3. **Privileged Escalation**: Standard user processes have deny-access to `SystemData`. An elevated helper process (prompting UAC) must grant write permissions to the user account on that folder.
4. **Media Transcoding**: The source video must be split into frames and encoded into an animated GIF via FFmpeg and Gifski.
5. **Cache Overwrite**: The animated GIF is written over `LockScreen.jpg` (and `LockScreen_XXX_notdimmed.jpg`).
6. **Windows 11 Feature Override (Build 26200+)**:
   Windows 11 24H2/25H2 disabled lock-screen GIF animation via Feature ID `38943831`. To re-enable it, the application must set:
   ```text
   HKLM\SYSTEM\CurrentControlSet\Control\FeatureManagement\Overrides\8\2920700556
       EnabledState = 1 (Disabled)
       EnabledStateOptions = 0
   ```
   and request a full system reboot.

### Critical Deficiencies and Risks
1. **Not Real Video**: Playback is limited to animated GIF frame sequencing (8-bit palette per frame, substantial loss of color depth, high file size, 15–30 fps max).
2. **Security & Stability**: Altering ACLs on `SystemData` and injecting undocumented feature flags into `CurrentControlSet` conflicts with enterprise security baselines and Windows Defender Application Control.
3. **OS Update Fragility**: Every major Windows 11 feature update has modified or restricted lock-screen behavior.
4. **System Overhead**: High-resolution animated GIFs in `LogonUI` consume large amounts of unpaged pool/memory and can cause visible lag when unlocking.

---

## 5. Decision Gate Recommendation

In accordance with **Section 5.3** of `PLAN.md`:

### Outcome Evaluation
- **Outcome A — Feasible natively**: **Negative**. No native API exists to stream video to the genuine lock screen.
- **Outcome B — Requires unsupported system modifications**: **Applicable if GIF cache injection with elevated ACL manipulation is chosen**. This requires explicit project-owner approval, administrative privileges, external transcoding tools (FFmpeg/Gifski), and registry overrides.
- **Outcome C — Genuine video playback is not feasible through an acceptable native approach**: **Applicable to true video playback (MP4/DirectX)**.

### Recommended Architectural Strategy for Moonlit
1. **Truth in UI and Architecture**:
   - The Moonlit application must adhere strictly to `PLAN.md` Rule 10: *"Do not claim that an API supports functionality without evidence."*
   - The status indicator must accurately report system readiness: **Red (Not Ready)** or **Amber (Prerequisites Required)**, distinguishing between a valid video file and lock-screen integration availability.
2. **Deliver Robust Core Application (Milestones 3–5)**:
   - Deliver a high-performance .NET 10 LTS WPF application with dark graphite design (`#111318`).
   - Implement native file selection, media validation, format probing, and in-app video preview.
   - Implement reliable JSON configuration persistence (`%LOCALAPPDATA%\Moonlit\settings.json`).
   - Implement Windows sign-in startup toggle (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`).
   - Implement a modular `LockScreenService` and `LockScreenIntegration` architecture that inspects system prerequisites, registry states, cache paths, and accurately reports diagnostic health without making unauthorized system changes.

---

## 6. Conclusion

Moonlit's foundation is built with full transparency regarding Windows platform constraints. By documenting these limitations formally, Moonlit ensures user system stability, preserves Windows authentication security, and delivers an intuitive utility designed to adopt any future official Windows dynamic lock-screen APIs as Microsoft evolves the platform.
