# ChatGPT and Codex computer use

The `sidescreen` skill in this repository can be installed into a Codex skill directory. It teaches future sessions how to discover the SideScreen monitor and use its ID and bounds. The CLI is also usable by any agent that can run local Windows PowerShell.

Example: run `agent.ps1 -Action Status`; find `agentScreen.id`, `deviceName`, `x`, `y`, `width`, `height`. Use `agent.ps1 -Action Windows` to list only windows on that display. Capture that display with `-Action Capture -ExpectedDisplayId <id> -OutputPath <new path>`, then inspect the PNG with your normal image tool. Use the `Candidates` list and `Move` with the expected display ID to place a specific approved window there.

ChatGPT computer use currently targets Windows apps/windows. Its normal input can activate the target window. SideScreen cannot change the behavior of that input tool. To preserve the human's focus, use its read-only window observation and SideScreen's placement/capture commands, or use a separate background-capable input tool for the particular app. If such a tool is not available, be explicit that interacting may take focus. An actual separate Windows session or VM is stronger isolation for arbitrary input; SideScreen ships neither.

The agent display is recognized by an active MTT virtual monitor path and ID, not by a hardcoded `DISPLAY5` number or screen coordinate. If the physical screen becomes unavailable, the tray app keeps the virtual display active and disables preview and turn-off until a physical display returns.
