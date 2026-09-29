# Background input

SideScreen 0.2 uses UI Automation to inspect a window, then sends documented Win32 control messages directly to supported native controls. It never uses global mouse/keyboard injection, activates a window, attaches input queues, or restores the user's focus after an action. No service or network listener is required.

## Supported operations

| Operation | Supported target | Verification |
| --- | --- | --- |
| SetValue | Editable, non-password classic `Edit` and WinForms edit controls | Text readback, including Unicode and empty values |
| Invoke | Standard push buttons and WinForms push buttons | Notification delivered; inspect the resulting app state |
| Toggle | System-rendered checkboxes | Changed check state |
| Select | Single-selection string list boxes | Selected index readback |

Custom-drawn checkboxes, WPF/WinUI controls without native handles, Chromium page controls, rich text editors, drag gestures, shortcut keys and arbitrary coordinates are unsupported. Their action list is empty. Do not infer support merely because a UIA pattern exists: a live test found that a classic text-field UIA adapter injected foreground input. SideScreen uses UIA only for discovery and readback, never pattern invocation.

## Agent sequence

1. Read `agent.ps1 -Action Status` and `-Action Windows`.
2. Inspect one window: `-Action Inspect -WindowHandle <handle> -ExpectedDisplayId <id>`.
3. Choose one returned element by its name, type and advertised `Actions`.
4. Act with the same handle/display, `-ObservationId <observation.Id> -ElementId <element.Id> -Operation <action>`. `SetValue` also requires `-Value`, which can be empty and replaces all text.
5. Read the receipt, then inspect or capture again. Never blindly retry a timeout, refusal or unknown outcome.

Inspection records expire after two minutes and are consumed by one action attempt, including a refused attempt. Window process/start time, UIA runtime identity, element name/type, enabled state and display bounds are checked again. The entire window must be inside the virtual monitor, and that monitor must not overlap another active display. A same-user mutex prevents concurrent helper operations.

## Focus behavior

The helper records foreground-window events, polls keyboard focus, and observes for another 250 ms after dispatch. If focus changes, it reports `ok:false`, `stop:true` and whether dispatch started. It never steals focus back, since that would interrupt the user again. App handlers can still open dialogs or activate windows themselves; monitoring detects interference during its observation period and cannot prevent every application side effect or a delayed change later.

The human may move the mouse while commands run. `CursorPreserved` is informational: a change cannot reliably be attributed to the human versus an app. Foreground/keyboard changes cause a conservative stop even when they came from the human switching windows. No pointer-moving API is called by the input helper.

This is bounded background control, not a security sandbox or a universal second keyboard. For browsers use a separately verified DOM/CDP route. For arbitrary GUI input, evaluate per-window virtualization such as MouseMux or an isolated desktop/session.

## Research

[MouseMux](https://www.mousemux.com/ai-agents/) describes independent virtual users locked to app windows and a local, armed MCP interface. Its [manual](https://www.mousemux.com/pages/manual/) explains per-window routing and app-specific compatibility. [Licensing](https://www.mousemux.com/pricing/) limits free app sessions; no MouseMux binaries or proprietary engine code are included here, and SideScreen has no tested MouseMux adapter.

[Microsoft's winapp CLI](https://github.com/microsoft/winappCli/blob/main/docs/ui-automation.md) distinguishes background control patterns/messages from global input and documents gaps for modern controls. These ideas informed explicit capabilities, scoped targets and refusal instead of foreground fallback. The implementation is original SideScreen code.
