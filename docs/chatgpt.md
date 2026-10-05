# ChatGPT and Codex computer use

The independent [SideScreen desktop plugin](../plugins/sidescreen/README.md) is the ChatGPT/Codex route. Its local stdio MCP server bundles the native SideScreen helpers and sixteen desktop/session tools. It has no Muse Link, broker, SSH or browser profile-manager dependency. [Muse Link](https://github.com/gitWRLD999/muse-link) is maintained separately for remote agents and browser automation.

Build the native tools and the `mcp` bundle, then from the repository root run `codex plugin marketplace add .` followed by `codex plugin add sidescreen@sidescreen-local`. Alternatively extract the desktop marketplace release ZIP and run those commands in its root. Enable the plugin and start a new chat. This does not restart Codex or rewrite built-in Windows computer use.

Start with `sidescreen_status` and `sidescreen_windows`. Lease the authorized window with `sideuser_open`, observe it, act on fresh returned tokens or screenshot coordinates, verify the actual result, and close the lease. Named steps use supported native controls before CUA. A missing CUA service does not block advertised native-only steps. The private clipboard does not read or replace the Windows clipboard. A second new-helper process cannot work inside another connection's window lease.

`sidescreen_stop`, MCP cancellation and disconnection stop the connection's worker and release leases without replay. The manifest declares Stop/Interrupt hooks; execution depends on the host supporting and trusting them. Hook execution in the desktop app has not been verified. A fresh status starts resumed work. Human mouse movement is allowed, but foreground focus changes can conservatively stop agent work. Unsupported raw input, security dialogs and application surfaces have no foreground fallback.

The `sidescreen` CLI skill remains available to agents that can run local Windows PowerShell. It teaches discovery, scoped background input and user-authorized window placement.

Example: run `agent.ps1 -Action Status`; find `agentScreen.id`, `deviceName`, `x`, `y`, `width`, `height`. Use `agent.ps1 -Action Windows` to list only windows on that display. Capture that display with `-Action Capture -ExpectedDisplayId <id> -OutputPath <new path>`, then inspect the PNG with your normal image tool. Use the `Candidates` list and `Move` with the expected display ID to place a specific approved window there.

The current ChatGPT computer-use Windows API targets app windows. Its input methods automatically activate the target window; using them on SideScreen will take foreground focus from the user. Read-only `get_window_state` can observe an occluded target window. For supported classic controls, use SideScreen `Inspect` and `Act` through the local shell instead of those built-in input methods. The installed skill explains this routing. For unsupported controls, use a verified app-specific backend or report the limitation. A separate Windows session or VM provides stronger isolation for arbitrary input; SideScreen ships neither.

When pairing the tools, first get the scoped `agent.ps1 -Action Windows` result. In computer use, select exactly one returned window by its app and title, and verify it matches the scoped list before observing it. Computer-use window IDs are opaque; do not assume they equal SideScreen's Win32 handles. Repeat the match after windows or display topology change. This is an agent operating rule, not a security sandbox for other tools.

The optional [CUA adapter](cua.md) supplies broader background app coverage and is installed separately. The plugin's bundled supervisor reuses a live service, backs off after status timeouts and respects the explicit pause control. Existing ChatGPT Windows computer use continues to use its own activating backend. CUA isolation remains app-specific; this plugin creates no separate Windows input session.

Public directory submission is a separate step. Current OpenAI guidance requires remote HTTPS MCP or contacting OpenAI for local MCP support. The local ZIP is not submitted or approved. See [submission materials and remaining gates](plugin-submission.md) and the [measured evaluation](evaluation-20261005.md).

The agent display is recognized by an active MTT virtual monitor path and ID, not by a hardcoded `DISPLAY5` number or screen coordinate. If the physical screen becomes unavailable, the tray app keeps the virtual display active and disables preview and turn-off until a physical display returns.
