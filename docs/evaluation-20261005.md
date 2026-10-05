# Windows desktop evaluation — October 5, 2026

The ChatGPT/Codex desktop plugin is independent of Muse Link. Its local stdio
server and bundled native helpers need neither Muse's broker, SSH connection,
configuration nor browser manager. Muse remains a separate companion package.

## Paired A/B results

Both arms used the same SideScreen action boundary and disposable native/WPF
apps on the virtual monitor. Arm A observed through CUA before each named action;
arm B tried a fresh native observation before falling back to CUA. Each task
replaced text, invoked a counter and toggled a checkbox. Six paired trials per
app used alternating arm/app order. Real app event handlers wrote the oracle.

| Fixture | A median | B median | A maximum | B maximum |
|---|---:|---:|---:|---:|
| Native controls | 4,220 ms | 3,065 ms | 6,230 ms | 5,469 ms |
| WPF controls | 5,170 ms | 4,237 ms | 8,474 ms | 8,364 ms |

All 24 tasks / 72 actions matched actual text, counter and checkbox state.
No action reported foreground changes or target activation. The native median
improved 27.4%. WPF's 18.0% sample difference is noisy and not a performance
guarantee. With six samples, the recorded empirical p95 is just the maximum;
it is not a reliable tail-latency estimate. Timings precede the final lease and
cancellation additions; later functional checks covered those additions.

`node tests/benchmark.mjs <private-output-directory> 6` reproduces this comparison.
The fixture must be built and CUA must already be available. Neither arm is
ChatGPT's built-in Windows backend. That backend's `list_windows` timed out on
three attempts, including the documented reset, so no built-in input baseline
was run. This evaluation does not establish parity with built-in computer use.

## Functional verification

| Suite | Passing checks | Scope |
|---|---:|---|
| Core build | 26 | Display boundaries/layout and native/CUA validation |
| Background live | 37 | Classic control handlers and refusal paths |
| CUA live | 61 | Native/WPF actual handlers and background delivery |
| Virtual input live | 118 | x64/x86 click, drag, wheel, keyboard and focus blocking |
| Supervisor recovery | 7 | Fake status timeout, retry, owned-daemon restart and pause |
| Independent MCP acceptance | 30 | Screenshot, Unicode, private text, kernel leases, EOF, cancellation and stop |
| Independent router/scope unit tests | 5 | Lease refusal, close, reused handle, moved window and backend replacement |
| Separate Muse companion tests | 44 | Its existing tools plus optional native-first compatibility |

The 30-check acceptance passed both the Node server and the packaged PowerShell
launcher with invalid Muse paths deliberately supplied. Cross-process leases
refused a second connection and released after disconnect/cancellation. Native
steps worked with a missing CUA executable. Manifest/MCP schemas and runtime
assets validated against the portable plugin schema. No existing human window
was moved and no real account sign-in was attempted.

One intermediate acceptance run changed text and counter but did not reach the
expected checkbox state. That run lacked a saved batch receipt, so its cause
was not established. Receipt capture was added; fresh independent and launcher
runs passed all checks without changing the action dispatch path. This remains
an observed intermittent failure, not evidence of perfect reliability.

Earlier registered-tool trials also verified 24 actual native/WPF actions with
no foreground activation. The human cursor moved during some actions; cursor
movement is informational and is allowed. This is useful concurrency evidence,
but not a test of arbitrary human foreground changes or every application.

## Limits and installation state

The plugin is a separate supported-background route. It does not replace
ChatGPT's built-in Windows input runtime. Coordinates require fresh window
captures; unsupported control types have no foreground fallback. Raw input,
elevation/security dialogs, GPU apps and browser account UI are not universally
supported. Shared Windows focus can make work stop when the human changes apps.
App handlers may themselves activate future dialogs outside the observation
period. Cross-process kernel leases require the new helpers; older helpers do
not participate in that boundary. Cancellation cannot undo an already dispatched
action and never replays it.

The host must support and trust Stop/Interrupt hooks. Their tool and SDK
cancellation behavior passed, but native desktop-app hook execution has not
been verified. Start a new chat after enabling the plugin to load its tools.
The public directory draft still needs OpenAI's local-MCP support route and
publisher review materials; it is not submitted or approved.

The already-running CUA supervisor was left in place. Its loaded code predates
the retry fix; automatic approval review blocked replacing the canonical script
in a combined cleanup/update command. The independent plugin bundles the fixed
script, but an existing supervisor's mutex prevents another supervisor from
taking over until that process exits. The running Muse broker was not upgraded
or restarted during this work.

Private raw receipts, fixture JSON and screenshots stay outside the repository
under the local acceptance directory. Public source includes only the harness,
aggregate findings, documentation, original logo and license notices.
