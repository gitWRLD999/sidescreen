# SideUser: original free per-agent input

SideScreen 0.6 adds a user-mode adapter for independent input state during scoped
events. Muse Link 1.3 exposes it through seven `sideuser_*` MCP tools. The native
adapter is original MIT code using MinHook 1.3.4 under BSD-2-Clause. It needs no
MouseMux license, subscription, elevated installer or kernel driver.

## Agent workflow

1. Call `sidescreen_status`, then `sidescreen_windows` to discover the current
   display ID and window handle. The entire app window must fit on SideScreen.
2. Call `sideuser_open` with that handle, display ID and a short cursor label.
   A five-minute idle lease binds the process, process start time and geometry
   to this MCP connection. Other agents must choose another window.
3. Call `sideuser_observe`. Request `include_screenshot:true` for pointer input.
   Select a fresh CUA token, or pixels in that returned image.
4. Call `sideuser_act` once. `invoke`/`set_value` use existing semantic routes.
   `move`, `click` (count 1 or 2), `drag`, `scroll`, `type` and `press` use the
   original native adapter. `paste` types the session's private clipboard.
   The returned app state verifies progress; inspect it before further input.
5. Close with `sideuser_close`. It releases virtual capture/focus; the app stays
   open. Reconnects and broker restarts require new sessions and observations.

`sideuser_run` accepts up to 12 named-control steps, with a fresh tree before
each step. An already-attempted `request_id` is refused instead of replaying.
`sideuser_clipboard` stores text privately in the broker and never touches the
Windows clipboard. `sideuser_status` lists only this connection's sessions.
The preview paints separately labeled blue cursors; the human cursor is drawn
from Windows' actual cursor state.

## How input works

The controller loads a DLL into only the target GUI thread using a Windows
thread hook. Both x64 and x86 DLLs ship; a separate x86 helper handles 32-bit
apps. The DLL validates a private mapping and forwards a bounded event to a
child control belonging to the same root, process and GUI thread.

During that event, cursor position, pointer hit testing, keyboard modifiers,
focus and capture queries use the event's virtual state. Activation and cursor
movement attempts are suppressed. Outside virtual events the APIs normally
pass through, except activation attempts on a recently assigned root are
suppressed for at most 120 seconds after its last event, or until release.
The target DLL is pinned until the app exits so its API hooks cannot refer to
unloaded code. Close agent apps before upgrading adapter DLLs.

Mouse capture is virtual. A drag is restricted to its initial client control,
2–64 points and 1.5 seconds. Keyboard input requires a freshly corroborated,
enabled native Edit control. Password and read-only controls are refused.
Ctrl+A uses the native edit selection command because WinForms shortcut
preprocessing does not run for directly delivered messages. Ctrl+C/V/X,
Windows shortcuts and Alt combinations are not exposed. Use the private text
clipboard and semantic controls for those supported tasks.

## Verification and scope

Local tests exercise actual standard button handlers, cursor queries, Unicode
text, Ctrl+A selection/replacement, separate modifiers, dragging and virtual
capture, both wheel axes, double-clicks and deliberate activation attempts in
32-bit and 64-bit WinForms apps. They also verify observation replay refusal,
password/read-only refusal and geometry boundaries.

This provides cooperative scheduling and input virtualization for supported
apps. Input operations are serialized; separate session state lets agents
interleave work in different windows. It is not a security boundary or a
separate Windows input session. Raw Input, DirectInput, protected/elevated
apps, input processing on other threads, custom message loops and asynchronously
opened dialogs need app-specific support or a separate desktop/VM. API hooks
do not establish universal compatibility. Check every focus receipt and app
effect; on unknown outcomes, stop and observe without replay or focus restore.

Regular signed-in Chrome uses the existing extension/DOM route. Do not inject
this native adapter into the human browser. Any MCP-capable agent can use the
same SideUser API. Built-in ChatGPT computer-use tools are not automatically
rerouted; an integration must explicitly map their actions to this API.

## Build

Run `native\prepare-native.ps1` before `build.ps1 -Test`. It verifies the pinned
Zig archive and clean MinHook commit, then builds both DLLs. Existing local
build tools can instead be passed to `native\build-native.ps1`.
`tests\virtual-live.ps1` runs opt-in real desktop tests on disposable SideScreen
windows. It requires the installed virtual display and official CUA Driver.

References: [Windows thread hooks](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowshookexw),
[MinHook source and license](https://github.com/TsudaKageyu/minhook/tree/v1.3.4).
