# CUA Driver integration

SideScreen 0.3 adds an optional adapter for the separately installed [Cua Driver](https://github.com/trycua/cua/tree/main/libs/cua-driver). The tested Windows version is 0.31.0. CUA adds accessibility actions for modern windowless controls and snapshot-bound pixel actions where its background route works. This broadens app coverage; it does not create an independent Windows input session.

## Install and lifecycle

Install CUA from its [official installation guide](https://cua.ai/docs/how-to-guides/driver/install). To use SideScreen's own supervisor, pass `-NoAutoStart` to the downloaded official installer. SideScreen does not bundle CUA binaries or automatically download them. Existing CUA services and configurations need not be replaced.

Run the release's `install-user.ps1 -StartTray -StartAtLogin -StartCua -CuaAtLogin`. The optional supervisor uses an ordinary per-user startup shortcut, launches hidden, restarts after a daemon exit, and requires a signed-in, unlocked interactive desktop. It uses `\\.\pipe\SideScreen.Cua.<Windows session ID>`, separately from the default CUA service. It sets content-free CUA telemetry off for its child processes. No TCP port, firewall change or elevation is needed. A missing binary prevents the supervisor from starting.

To pause this supervisor, create `%USERPROFILE%\AgentTools\SideScreen\state\cua-service\paused`. It stops the child it owns within roughly one second, then exits. If it is reusing a separately launched daemon on its pipe, it does not stop that process. Remove the pause file and run `start-cua.ps1` to resume. Remove `SideScreen-Cua.lnk` from your Startup folder to disable sign-in startup. These actions do not disable the virtual display or affect another CUA pipe.

## Observe and act

```powershell
.\agent.ps1 -Action Status
.\agent.ps1 -Action Windows
.\agent.ps1 -Action CuaObserve -WindowHandle <returned handle> -ExpectedDisplayId '<Status.agentScreen.id>'
.\agent.ps1 -Action CuaAct -WindowHandle <same handle> -ExpectedDisplayId '<same id>' -ObservationId '<observationId>' -Tool click -ArgumentsJson '{"element_token":"<token from state.elements>"}'
```

`CuaObserve` returns CUA's accessibility tree, tokens and a retained PNG path. `-NoScreenshot` requests only the tree. A pixel click requires that screenshot, its `capture_id`, and image-pixel coordinates. Version 0.4 supports bounded image dimensions, element/depth limits and image-only observations. Always use returned image dimensions; the preview may show a scaled image. It does not accept a different image, desktop target, target override, session override or delivery-mode override.

Each observation expires after two minutes and is consumed by one action attempt, including refusals. Window handle, process/start time, geometry, current display ID and full containment are rechecked. The monitor must not overlap a physical screen. A same-user mutex serializes SideScreen native and CUA calls. Tokens must belong to the saved observation, and CUA also checks its live snapshot bindings. Independent raw CUA clients can invalidate CUA observations; reobserve if that happens.

Tools exposed: `click`, `set_value`, `type_text`, token-based `scroll`, `press_key` and `hotkey`. Text/keyboard actions require an element token. Native classic edit writes use SideScreen's verified `WM_SETTEXT` backend; native push buttons, system checkboxes and list items also prefer SideScreen messages. Password/read-only native edits are refused. Unsupported native text controls do not gain a CUA typing fallback. Other CUA routes are experimental and app-specific. Inspect the advertised actions and `sidescreen_*` route fields before acting.

CUA calls always receive `delivery_mode:"background"`. The adapter does not expose foreground escalation, desktop input, activation, launching, shell execution, direct clipboard or browser-profile setup. CUA's raw tools have additional capabilities; registering them directly does not enforce SideScreen's screen boundary. Use this adapter for confined host work.

Read `ok`, `stop`, `dispatched`, `focus` and `driver`. The focus guard records foreground events and keyboard-focus changes during dispatch and another 250 ms. It detects interference; it cannot prevent every provider or application side effect. Human mouse movement is allowed and reported separately. A CUA `effect:"unverifiable"` means the app state still needs verification even if dispatch succeeded. Never replay an unknown action. Foreground-restoring code is not used by this adapter.

CUA observations use unique lifecycle labels, so an idle-ended previous session cannot break later work. Action attempts end their owned CUA lifecycle on a best-effort basis; CUA also cleans idle lifecycles after its TTL. Retained PNGs remain local under `%USERPROFILE%\AgentTools\SideScreen\state\cua-observations`; remove them when no longer needed. Expired JSON records are cleaned on later observations.

## Muse and other agents

Version 0.4's preferred connection is the separate [Muse Link](https://github.com/gitWRLD999/muse-link)
package. Its combined `agent` channel exposes regular Chrome and eight scoped
SideScreen tools, including find, wait, act-and-observe and bounded named steps.
The helper's `--server` mode retains one CUA MCP transport. Each step still
requires a fresh observation and preserves all existing scope/focus checks.
Legacy one-shot callers retain CLI transport, since closing an MCP client ends
its associated CUA sessions. UIA properties are cached within each inspection;
live identity and capabilities are rechecked before input. Helper and CUA
readiness are probed separately from configured engine names.

The preferred install path is `%USERPROFILE%\AgentTools\SideScreen`. Set
`SIDESCREEN_CUA_BINARY` when the separately installed driver is elsewhere;
the startup supervisor also discovers `%USERPROFILE%\AgentTools\Cua\bin`.
The supervisor requests reduced cursor motion. The preview targets 10 fps,
includes layered windows, and explicitly composites the physical cursor.

The following describes the retained four-tool legacy integration:

`integrations/muse/sidescreen-engine.mjs` exports an adapter for the existing authenticated Muse/OpenClaw broker. It depends only on Node built-ins, spawns `SideScreen.Cua.exe` with JSON stdin, and exports four MCP-shaped tools: `sidescreen_status`, `sidescreen_windows`, `sidescreen_observe`, `sidescreen_act`. Observations include image content for agents that support images. The existing broker keeps its loopback bearer authentication, SSH host-key verification and serial dispatch. This adapter adds no listener.

Copy the module into the broker folder, import `createSideScreenEngine`, route `engine:'sidescreen'` to that handler, and include `sidescreen` in the MCP proxy's engine allowlist. Use `SIDESCREEN_HOME` when installation is elsewhere. The local Muse broker in the development setup has these changes. Its SSH/MCP example retains the existing hostname and identity configuration; the public SideScreen repository contains no personal endpoint or key.

From that broker folder, `node muse.mjs sidescreen list` discovers the tools. `node mcp.mjs sidescreen` supplies the stdio MCP interface, locally or over the existing verified SSH connection. Run the broker in the signed-in interactive session. SSH runs the proxy, not the desktop helper directly. Add a separate `muse-windows-sidescreen` entry to your agent's existing MCP configuration. Remote Muse registration and remote execution must be checked from the remote environment; a local handshake alone does not prove them.

Built-in ChatGPT Windows clicks still use its original input backend. The SideScreen skill or the scoped MCP adapter can use this CUA route; installing CUA does not patch the built-in tool.

## Full input separation

See [isolated desktop](isolation.md). A host virtual monitor is workspace, not another input seat. CUA's [Windows guide](https://github.com/trycua/cua/blob/main/libs/cua-driver/rust/Skills/cua-driver/WINDOWS.md) explicitly refuses some background actions. Run a driver inside a persistent VM to use its foreground input without changing host focus; control the guest through its own broker, rather than clicking the VM viewer on the host.
