# SideScreen

**A Windows side display for agent work.**

Give agent windows somewhere to live, watch them in a passive preview, and bring them back when you need them. SideScreen manages an existing MTT virtual monitor and supplies a small JSON command interface for display status, window placement and screenshots.

**Focus-preserving placement is implemented. Independent mouse and keyboard input is not.** Windows monitors in the same desktop share foreground focus and a pointer. SideScreen's agent commands never call `SetForegroundWindow`, `SendInput`, or `SetCursorPos`. Your automation backend must also support background operation; PyAutoGUI, ordinary desktop clicks and foreground keyboard automation can still interrupt your work.

## What you get

- A tray app to enable/disable an installed virtual display, preview it, and recover windows.
- Window placement using `SWP_NOACTIVATE`, with a receipt reporting whether foreground focus and cursor position stayed the same.
- JSON commands that agents can invoke locally or through an existing secure remote shell.
- A passive preview that sends no keyboard or pointer input to the agent screen.
- Support for physical monitors beyond the original laptop model, including displays positioned left of the primary monitor.
- A guard that refuses to disable the virtual monitor when no usable physical screen is active.

Early release: Windows x64, one active MTT virtual monitor. Driver binaries, remote access, and an AI model are not included.

## Quick start

1. Install [VirtualDrivers' Virtual Display Driver](https://github.com/VirtualDrivers/Virtual-Display-Driver) from its official release. Keep a physical display enabled and choose **Extend these displays** in Windows Display Settings.
2. Download `SideScreen-0.1.0-win-x64.zip` from this repository's [releases](https://github.com/gitWRLD999/sidescreen/releases), or build from source below. Extract the whole folder. Run `SideScreen.exe`, or run `install-user.ps1 -StartTray -StartAtLogin` to copy the tools to `%LOCALAPPDATA%\SideScreenTools`, install the Codex skill, and start the tray at future sign-ins. Omit `-StartAtLogin` if you want to launch it manually.
3. Open the tray controls. Use **View screen** for a preview; use the commands below to place a normal window on the agent screen.

Windows requests elevation only when enabling or disabling the driver. Viewing, listing, capture and placement run as your ordinary Windows user. The app does not install a driver, change screen resolutions, or start a remote server.

If only the virtual display is active, preview/recovery and **Turn off** are unavailable. Enable a physical screen in Windows Display Settings; opening the laptop lid may be enough. This preserves the only remaining display instead of leaving you without remote video.

## Agent commands

Run from the extracted release folder with Windows PowerShell:

```powershell
.\agent.ps1 -Action Status
.\agent.ps1 -Action Windows
.\agent.ps1 -Action Candidates
.\agent.ps1 -Action Move -WindowHandle 123456 -Destination Agent -ExpectedDisplayId '<id from Status.agentScreen>'
.\agent.ps1 -Action Capture -ExpectedDisplayId '<id from Status.agentScreen>' -OutputPath "$env:TEMP\agent-screen-1.png"
.\agent.ps1 -Action Move -WindowHandle 123456 -Destination Main -ExpectedDisplayId '<id from Status.physicalScreens>'
```

`Status.agentScreen` identifies the sole MTT monitor by its Windows device name, hardware ID and current bounds; its `id` pins a subsequent operation to the same display. Re-read it after a display topology change. `Windows` lists only windows on the agent screen. `Candidates` is the explicit all-window list to use when placing a user-selected window there. Replace `123456` with a handle from a fresh result. Normal, visible windows are supported. Minimized/maximized windows must be restored by the user first because restoration can activate an app. CLI recovery requires exactly one physical display; the tray preview prefers the primary physical display when several are active.

Commands return JSON. Exit code `0` means success, `1` means a refused/failed operation, and `2` means placement completed but foreground focus or cursor position changed during the operation. Other processes and the target application can change focus themselves, so check the receipt and observe the result before continuing. Capture requires a new output filename and saves only the agent monitor.

## Keeping your screen yours

| Layer | What it provides |
| --- | --- |
| Virtual Display Driver | Additional monitor space in your Windows desktop |
| SideScreen | Placement without activation, passive preview, capture, recovery |
| Background automation backend | App-specific input without taking foreground focus, when supported |
| Separate VM/session | Stronger separation for arbitrary foreground mouse/keyboard workflows |

Browser DOM automation and supported accessibility actions are possible companion approaches. SideScreen does not implement or guarantee their input behavior. See [related projects](docs/related-projects.md) for options and the [agent integration guide](docs/agents.md) for the operating contract.

For Codex/ChatGPT computer use, install the [SideScreen skill](skills/sidescreen/SKILL.md) and read the [computer-use integration note](docs/chatgpt.md). It gives agents a repeatable discovery and scoping workflow; it does not alter the computer-use tool's input behavior.
The release's `install-user.ps1` installs the skill into the current user's Codex skill directory. Restart Codex or begin a new task if it does not discover a newly installed skill immediately.

## Build and verify

No NuGet packages or SDK downloads are required. Use Windows x64 with .NET Framework 4.8 and its C# compiler:

```powershell
.\build.ps1 -Test
.\dist\SideScreen.Tests.exe --live-placement
```

The first command compiles the app and runs display-selection/layout tests. The second opens a disposable, nonactivating test window, moves it, checks foreground/cursor preservation and closes it. It does not move your existing windows. Do not run the live check while deliberately moving the mouse, since that correctly makes the cursor-preservation assertion fail.

CI builds on Windows and runs the non-UI checks. See [validation](docs/validation.md) for the checks performed for this release and their limits.

## Privacy and lifecycle

The application has no network listener, telemetry, credential store or model connection. Window titles and screenshots can contain private information; command output goes to the caller and captures stay at the path you specify. Runtime logs are stored under `%LOCALAPPDATA%\SideScreen`, outside the checkout. Nothing is uploaded by the app.

The tray app starts only when launched. To start it at sign-in, put a shortcut to `SideScreen.exe` in your own `shell:startup` folder. Driver enabled/disabled state is managed by Windows and can persist across restarts. Closing the tray app does not disable the display. No automatic topology restoration or input isolation is promised.

## Credits and license

SideScreen grew out of a personal Windows virtual-display tray helper. The separately installed [VirtualDrivers / MikeTheTech driver](https://github.com/VirtualDrivers/Virtual-Display-Driver) creates the monitor; this project does not bundle or claim authorship of it. Research comparisons informed the documentation; no code from the compared agent projects is included. SideScreen's code is MIT licensed.
