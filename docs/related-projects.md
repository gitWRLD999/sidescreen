# Related projects

Reviewed September 28, 2026 using each project's own repository. Feature statements below summarize their documentation, not independent compatibility tests.

| Project | Relevant approach | Relationship to SideScreen |
| --- | --- | --- |
| [MouseMux](https://www.mousemux.com/ai-agents/) | Per-window virtual users and local MCP input routing | Inspiration for scoped input; commercial runtime is not bundled or integrated |
| [Microsoft winapp CLI](https://github.com/microsoft/winappCli/blob/main/docs/ui-automation.md) | UIA discovery, control messages, explicit foreground-input modes | Informs capability limits and refusing global-input fallback |
| [VirtualDrivers / Virtual Display Driver](https://github.com/VirtualDrivers/Virtual-Display-Driver) | Windows indirect display driver, configurable virtual monitors | Required external monitor provider; mature driver work is reused through installation, not reimplemented |
| [Trope CUA](https://github.com/tropeai/trope-cua) | Windows/macOS agent tools that choose background routes where OS and app support them | Potential input companion; end-to-end integration has not been tested |
| [Ghost](https://github.com/NORTHTEKDevs/ghost) | Background focus policy and operator-controlled focus lock; unsupported background actions fail | Useful example of refusing foreground fallback instead of claiming a monitor isolates input |
| [Deskwright](https://github.com/tristanmuzzu/deskwright) | GNOME/Wayland agent computer use, including a separate invisible GNOME session | Illustrates stronger session separation; different OS and architecture |

## Improvements applied here

The original helper assumed one specific laptop panel, Intel adapter and fixed resolution. The public version recognizes active physical connectors without a personal hardware ID, keeps the sole-display protection, and explains why preview is unavailable. Placement refuses minimized/maximized windows rather than restoring and activating them. Agent operations use JSON, explicit window handles, negative-coordinate-safe layout and focus/cursor receipts. Logs are outside the source folder. A repeatable build, focused tests and Windows CI accompany the source.

## Honest scope

A virtual monitor on an existing Windows desktop is not a separate input session. Full background input requires another component, and some applications cannot support it. A future integration should require a backend to declare supported operations and refuse unsupported ones; it must not silently fall back to global input. Separate VM/session support is a possible future direction, not a shipped feature.
