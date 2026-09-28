# Agent operating contract

1. Inspect `agent.ps1 -Action Status`. Use `agentScreen.id` and the returned bounds. If `available` is false, report that condition. The Windows display name can change after a topology change, so do not cache it forever.
2. `Windows` lists only windows on the agent screen. Use `Candidates` only to locate a specific window the user asked you to move. Re-enumerate before each move: handles can be reused after windows close.
3. Call `Move` with the fresh handle and `-ExpectedDisplayId` from Status. Read `FocusPreserved` and `CursorPreserved`. Stop and report interference if either is false; do not compensate with foreground activation.
4. Capture the agent monitor to a new file with that same expected display ID when visual verification is useful. Read the image through your normal image tool. Keep it local unless the user authorized sharing it.
5. Use a separately configured background input tool for input. SideScreen has no click/type tool. Do not infer input isolation from display placement.
6. Recover windows on request. Do not turn off the only active display. Leave driver enable/disable and elevation prompts to the user unless they explicitly requested those changes.

Example secure remote invocation: run Windows PowerShell through your existing SSH connection with strict host verification. SideScreen does not provision SSH, expose HTTP, distribute keys, or configure your agent account. Its scripts can also be wrapped as MCP tools by the host application; no MCP server ships in this release.

For ChatGPT computer use, use the SideScreen command to identify the Windows display and its bounds, then match one computer-use app/window to the scoped `Windows` list by title and app. The computer-use Windows API automatically activates its target for input; SideScreen cannot override that. Its read-only window snapshot can observe an occluded target. Use a verified background-capable tool for input when maintaining the user's focus matters. If only computer-use foreground input is available, stop and ask the user to choose whether a brief interruption is acceptable. Treat "on SideScreen" as a placement rule, not proof of input isolation.
