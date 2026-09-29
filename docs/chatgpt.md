# ChatGPT and Codex computer use

The `sidescreen` skill in this repository can be installed into a Codex skill directory. It teaches future sessions how to discover the SideScreen monitor and use its ID and bounds. The CLI is also usable by any agent that can run local Windows PowerShell.

Example: run `agent.ps1 -Action Status`; find `agentScreen.id`, `deviceName`, `x`, `y`, `width`, `height`. Use `agent.ps1 -Action Windows` to list only windows on that display. Capture that display with `-Action Capture -ExpectedDisplayId <id> -OutputPath <new path>`, then inspect the PNG with your normal image tool. Use the `Candidates` list and `Move` with the expected display ID to place a specific approved window there.

The current ChatGPT computer-use Windows API targets app windows. Its input methods automatically activate the target window; using them on SideScreen will take foreground focus from the user. Read-only `get_window_state` can observe an occluded target window. For supported classic controls, use SideScreen `Inspect` and `Act` through the local shell instead of those built-in input methods. The installed skill explains this routing. For unsupported controls, use a verified app-specific backend or report the limitation. A separate Windows session or VM provides stronger isolation for arbitrary input; SideScreen ships neither.

When pairing the tools, first get the scoped `agent.ps1 -Action Windows` result. In computer use, select exactly one returned window by its app and title, and verify it matches the scoped list before observing it. Computer-use window IDs are opaque; do not assume they equal SideScreen's Win32 handles. Repeat the match after windows or display topology change. This is an agent operating rule, not a security sandbox for other tools.

The agent display is recognized by an active MTT virtual monitor path and ID, not by a hardcoded `DISPLAY5` number or screen coordinate. If the physical screen becomes unavailable, the tray app keeps the virtual display active and disables preview and turn-off until a physical display returns.
