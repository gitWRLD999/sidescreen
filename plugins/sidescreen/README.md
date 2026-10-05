# SideScreen desktop plugin

Agent work beside your desktop. This Windows ChatGPT/Codex plugin has its own
local stdio MCP server with sixteen scoped desktop/session tools. It requires
neither Muse Link, SSH, a network broker nor a browser profile manager. Browser
DOM automation and Muse connectivity are separate products.

Prerequisites: Windows 10/11 signed-in interactive session, Node 22+, an active
MTT virtual display driver, and optional CUA Driver for modern controls. The
bundled JavaScript includes its MCP dependencies; no npm install is needed by
the plugin user. SideScreen 0.8 native helpers are bundled.

The helper defaults to this plugin's `native` folder; SIDESCREEN_HOME supports
another compatible helper location. The launcher can start the bundled CUA supervisor for a separately installed driver and respects
its explicit pause file. It does not launch apps or browsers.

Enable SideScreen from the local marketplace, then start a new chat. The assistant
finds the monitor, selects a current window, observes, acts and verifies state.
Per-connection cursors, keyboard state and private clipboard are scoped to a leased
window. Windows mutexes enforce leases across plugin processes and release them
when the connection closes or dies. Actions remain serialized.

The `sidescreen_stop` tool stops this connection and releases its leases. MCP
request cancellation also stops its worker. The manifest declares Stop/Interrupt
hooks; the host must support and trust them. Hook execution in the desktop app
has not been verified. Start resumed work with a fresh status call.

To build from source, run `native/prepare-native.ps1`, `build.ps1`, then `npm ci`
and `npm run build` inside `mcp`. From the repository root, run
`codex plugin marketplace add .` and `codex plugin add sidescreen@sidescreen-local`.
The release ZIP is portable; register its containing marketplace or use the
host's local plugin import. CUA Driver and the virtual display driver are not
included. Original native helpers retain MIT and MinHook license notices.

Supported controls use background messages or CUA. Pixel gestures need fresh
captures and supported input surfaces. Raw input, elevation/security prompts,
some GPU apps and Chrome UI are not universally supported. The plugin does not
patch ChatGPT's built-in Windows input backend: that backend activates windows
and is not a background fallback. Human foreground app changes can cause a
conservative stop without replaying input.

Disabling the plugin disconnects its tools. Uninstalling it does not remove
SideScreen, apps, browser profiles or user files. Stop the separate CUA supervisor
with its documented pause control.

Public directory status: local-MCP development package, not submitted or approved.
Current OpenAI guidance requires a remote HTTPS endpoint or an OpenAI contact
for local MCP support. See docs/plugin-submission.md in the source repository.
