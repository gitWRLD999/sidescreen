# ChatGPT and Codex plugin submission draft

Status: locally testable Windows MCP package; not submitted, reviewed or approved.
The product is **SideScreen — agent work beside your desktop**.

This is a local desktop integration. Current public submission instructions say
local MCP servers need a public HTTPS deployment or an OpenAI contact for local
MCP support. This package uses local stdio; a public desktop tunnel is not part
of its architecture or submission preparation.

## Materials

- Portable `plugin.json`, stdio `mcp.json`, operating skill, original logo and
  deterministic local launcher are in `plugins/sidescreen`.
- Installable marketplace is `.agents/plugins/marketplace.json`.
- Data handling is described in `docs/privacy.md`; source license is MIT.
- Reproducible paired benchmark is `tests/benchmark.mjs`; findings are in
  `docs/evaluation-20261005.md`.
- Software dependencies: Node 22+, optional CUA Driver, virtual display driver.
  The ZIP bundles original SideScreen 0.8 native helpers, the MCP runtime and
  their license notices. It excludes both upstream driver installers, profile
  data, local addresses and credentials.

## Five positive reviewer cases

Use disposable sample apps. Verify actual app postconditions and foreground
behavior. The current desktop-only review cases need no account browser.

| Scenario / prompt | Expected tools | Expected result |
|---|---|---|
| “Find my SideScreen and tell me which test apps are there.” | `sidescreen_status`, `sidescreen_windows` | One non-overlapping virtual monitor; only its windows listed. |
| “Put café Ω into Agent text, increment the counter, and toggle the checkbox.” | `sidescreen_steps` | Three fresh native actions; oracle text/count/check match; no agent foreground activation. |
| “Do the same in the WPF sample app.” | observe/steps with CUA | Actual WPF handlers change all three values; CUA delivery stays background. |
| “Work in this window as Researcher, and keep my clipboard alone.” | `sideuser_open`, observe, private clipboard, act, close | Session is connection-owned; supported action works; host clipboard is unchanged. |
| “Increment the native sample counter while CUA is unavailable.” | `sidescreen_status`, `sidescreen_steps` | Advertised native-only route changes the counter without starting CUA or using foreground input. |

## Three negative reviewer cases

1. “Click this main-display window.” Scoped helpers reject its display/containment
   before input. Do not switch to built-in foreground computer use.
2. Repeat a consumed observation or another connection's session. Refuse; the
   application's counter must not change again. Observe before new work.
3. Request an out-of-image pixel, a foreground delivery override, or a password
   control action. Refuse before dispatch and preserve the target state.

## Release notes

Plugin 1.0.0 supplies an independent desktop/session MCP server
for local desktop use. It does not require Muse Link. SideScreen 0.8 adds
native-only observations for supported named steps, cross-process window leases,
expired screenshot cleanup, and resilient CUA supervision. Cancellation and the
explicit stop tool terminate the connection's worker without replaying input.
Muse Link is maintained and released separately.

## Public submission gates still requiring the publisher

1. Resolve the local-MCP support route with OpenAI, or design a separate hosted
   service with explicit device enrollment, per-user authorization and revocation.
   That hosted service has not been built or deployed in this work.
2. Choose the owning OpenAI organization/project and complete developer identity
   verification. The GitHub author is not a verified OpenAI publishing identity.
3. Confirm the final public privacy/support URLs and listing copy under that
   identity. The source documents must be reachable at submission time.
4. Provide a reviewable Windows fixture environment/sample account and accessible
   walkthrough video. Public reviewers must not need the owner's private network.
5. Upload the complete MCP-containing ZIP, resolve automated findings, provide
   review details and complete the publisher's attestations. Publish only after
   approval. Do not upload a skills-only draft as a workaround.

Official references, checked October 5, 2026:

- https://developers.openai.com/plugins/build/plugins
- https://developers.openai.com/plugins/deploy/submission
