# SideScreen

**A Windows side display for agent work.**

Give agent windows somewhere to live, watch them in a passive preview, and bring them back when you need them. SideScreen manages an existing MTT virtual monitor and supplies a small JSON command interface for display status, window placement and screenshots.

**Background input works for classic Windows controls, with optional CUA support for modern apps.** Native controls use direct messages. The [CUA adapter](docs/cua.md) adds snapshot-bound accessibility and pixel actions confined to a window on the virtual monitor, with background delivery fixed and foreground fallback refused. Windows monitors still share one input session. Use a [persistent VM](docs/isolation.md) for broader independent mouse/keyboard delivery. Provider and app behavior can still change focus; monitoring detects interference within its observation period.

## What you get

- A tray app to enable/disable an installed virtual display, preview it, and recover windows.
- Window placement using `SWP_NOACTIVATE`, with a receipt reporting whether foreground focus and cursor position stayed the same.
- JSON commands that agents can invoke locally or through an existing secure remote shell.
- Scoped background input with one-use inspections, process identity checks, control readback, and foreground/keyboard-focus monitoring.
- An optional persistent CUA adapter, a per-user restart supervisor, and eight scoped Muse/MCP tools. CUA is installed separately. [Muse Link](https://github.com/gitWRLD999/muse-link) combines these with regular Chrome tools in one agent connection.
- A free [SideCursor](docs/sidecursor.md) software pointer, scoped to a fresh window screenshot, with its own blue marker in the preview. Supported background operations preserve the human cursor; this does not create a separate Windows input session.
- Free [SideUser input](docs/sideusers.md): original x64/x86 per-event cursor, modifier and capture state, drag, wheel, double-click and native edit keyboard operations. Muse Link adds window leases, labeled cursors, private text clipboards and named macros for agents.
- A passive preview that sends no keyboard or pointer input to the agent screen.
- Support for physical monitors beyond the original laptop model, including displays positioned left of the primary monitor.
- A guard that refuses to disable the virtual monitor when no usable physical screen is active.

Early release: Windows x64, one active MTT virtual monitor. Driver binaries, remote access, and an AI model are not included.

## Quick start

1. Install [VirtualDrivers' Virtual Display Driver](https://github.com/VirtualDrivers/Virtual-Display-Driver) from its official release. Keep a physical display enabled and choose **Extend these displays** in Windows Display Settings.
2. Download `SideScreen-0.6.0-win-x64.zip` from this repository's [releases](https://github.com/gitWRLD999/sidescreen/releases), or build from source below. Extract the whole folder. Run `SideScreen.exe`, or run `install-user.ps1 -StartTray -StartAtLogin` to copy the tools to `%USERPROFILE%\AgentTools\SideScreen`, install the Codex skill, and start the tray at future sign-ins. Omit `-StartAtLogin` if you want to launch it manually. For separately installed CUA, add `-StartCua -CuaAtLogin`; see [CUA setup](docs/cua.md).
3. Open the tray controls. Use **View screen** for a preview; use the commands below to place a normal window on the agent screen.

Windows requests elevation only when enabling or disabling the driver. Viewing, listing, capture and placement run as your ordinary Windows user. The app does not install a driver, change screen resolutions, or start a remote server.
To update an existing installation, exit the SideScreen tray app before running `install-user.ps1` again.

If only the virtual display is active, preview/recovery and **Turn off** are unavailable. Enable a physical screen in Windows Display Settings; opening the laptop lid may be enough. This preserves the only remaining display instead of leaving you without remote video.

## Agent commands

Run from the extracted release folder with Windows PowerShell:

```powershell
.\agent.ps1 -Action Status
.\agent.ps1 -Action Windows
.\agent.ps1 -Action Inspect -WindowHandle 123456 -ExpectedDisplayId '<id from Status.agentScreen>'
.\agent.ps1 -Action Act -WindowHandle 123456 -ExpectedDisplayId '<same display id>' -ObservationId '<id from Inspect>' -ElementId 3 -Operation SetValue -Value 'Hello'
.\agent.ps1 -Action Candidates
.\agent.ps1 -Action Move -WindowHandle 123456 -Destination Agent -ExpectedDisplayId '<id from Status.agentScreen>'
.\agent.ps1 -Action Capture -ExpectedDisplayId '<id from Status.agentScreen>' -OutputPath "$env:TEMP\agent-screen-1.png"
.\agent.ps1 -Action Move -WindowHandle 123456 -Destination Main -ExpectedDisplayId '<id from Status.physicalScreens>'
```

`Status.agentScreen` identifies the sole MTT monitor by its Windows device name, hardware ID and current bounds; its `id` pins a subsequent operation to the same display. Re-read it after a display topology change. `Windows` lists only windows on the agent screen. `Candidates` is the explicit all-window list to use when placing a user-selected window there. Replace `123456` with a handle from a fresh result. Normal, visible windows are supported. Minimized/maximized windows must be restored by the user first because restoration can activate an app. CLI recovery requires exactly one physical display; the tray preview prefers the primary physical display when several are active.

Commands return JSON. Exit code `0` means success, `1` means a refused/failed operation, and `2` means placement completed but foreground focus or cursor position changed during the operation. Other processes and the target application can change focus themselves, so check the receipt and observe the result before continuing. Capture requires a new output filename and saves only the agent monitor.

For input, inspect first and select a returned element's advertised `Actions`. Never guess an element number. Each inspection expires after two minutes and allows one action attempt; re-inspect after each action or refusal. Input requires the entire target window to fit inside the agent monitor. A timeout or focus-change report means stop and observe: the action may already have happened. Human mouse movement is allowed and reported separately.

## Keeping your screen yours

| Layer | What it provides |
| --- | --- |
| Virtual Display Driver | Additional monitor space in your Windows desktop |
| SideScreen | Placement, preview, capture, recovery, and background messages to supported classic controls |
| Background automation backend | App-specific input without taking foreground focus, when supported |
| Separate VM/session | Stronger separation for arbitrary foreground mouse/keyboard workflows |

Browser DOM automation and MouseMux are companion approaches for broader app coverage. The optional CUA adapter is tested on a disposable native app and WPF app. Broader compatibility remains app-specific. See [related projects](docs/related-projects.md) and the [agent integration guide](docs/agents.md).

For Codex/ChatGPT computer use, install the [SideScreen skill](skills/sidescreen/SKILL.md) and read the [computer-use integration note](docs/chatgpt.md). Use read-only computer-use screenshots for observation and SideScreen `Inspect`/`Act` for supported background actions. Built-in ChatGPT Windows clicks/typing still activate their target; the skill does not patch that tool.
The release's `install-user.ps1` installs the skill into the current user's Codex skill directory. Restart Codex or begin a new task if it does not discover a newly installed skill immediately.

## Build and verify

No NuGet packages or SDK downloads are required. Use Windows x64 with .NET Framework 4.8 and its C# compiler:

```powershell
.\native\prepare-native.ps1 # pinned compiler and MinHook, builds both DLLs
.\build.ps1 -Test
.\dist\SideScreen.Tests.exe --live-placement
.\tests\background-live.ps1
.\tests\cua-live.ps1 # optional: CUA 0.31 service must already be running
.\tests\virtual-live.ps1 # optional: actual x64/x86 virtual input effects
node .\tests\muse-adapter.mjs
```

The native preparation command builds both adapters from pinned dependencies. The C# build compiles the app and runs display-selection/layout tests. Live checks use disposable nonactivating windows and verify actual app effects, focus preservation and refusal paths. Keep the pointer still for the placement check; background-input tests allow human pointer movement. These tests do not move existing user windows.

CI builds on Windows and runs the non-UI checks. See [validation](docs/validation.md) for the checks performed for this release and their limits.

## Privacy and lifecycle

The tray and native input helper have no network listener, telemetry, credential store or model connection. Optional CUA uses a local named pipe; the SideScreen supervisor turns CUA child-process telemetry off. The Muse adapter uses the existing authenticated broker and adds no listener. Window titles/screenshots can contain private information; command output goes to the caller. Logs, short-lived observation records and CUA PNGs remain under `%USERPROFILE%\AgentTools\SideScreen\state`, outside the checkout. Native helpers have a 15-second outer timeout and CUA helpers a 30-second outer timeout. Retained CUA PNGs require user cleanup. Nothing is uploaded by the tray app.

The tray app starts only when launched. To start it at sign-in, put a shortcut to `SideScreen.exe` in your own `shell:startup` folder. Driver enabled/disabled state is managed by Windows and can persist across restarts. Closing the tray app does not disable the display. No automatic topology restoration or input isolation is promised.

## Credits and license

SideScreen grew out of a personal Windows virtual-display tray helper. The separately installed [VirtualDrivers / MikeTheTech driver](https://github.com/VirtualDrivers/Virtual-Display-Driver) creates the monitor; this project does not bundle or claim authorship of it. The original virtual-input adapter uses [MinHook 1.3.4](https://github.com/TsudaKageyu/minhook/tree/v1.3.4), which retains its bundled BSD-2-Clause license. SideScreen's original code is MIT licensed; it contains no MouseMux implementation code.
