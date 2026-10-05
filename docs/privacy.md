# SideScreen data handling

SideScreen is open-source local Windows software maintained at
https://github.com/gitWRLD999/sidescreen. The desktop plugin has no SideScreen
cloud service, analytics endpoint or developer collection of desktop data.

The independent local stdio MCP server reads the explicitly scoped task window's
accessibility tree and, when requested, screenshots. Returned content is visible
to the calling assistant and follows that provider's terms and privacy policy.
This plugin does not manage Chrome profiles or browser login credentials. Muse
connectivity and browser DOM automation are separate integrations.

Per-session clipboard text stays in the desktop server's memory and is not the
Windows clipboard. Observations expire after two minutes and permit one action
attempt. Expired UUID-named observation JSON/PNG files are removed after five
minutes when another observation occurs; this is not a deletion timer. Diagnostic
logs and user-created files remain until removed. The plugin ZIP contains no
credentials, profiles, private screenshots or device enrollment data.

CUA Driver is a separate upstream dependency. The supplied supervisor sets
CUA_DRIVER_RS_TELEMETRY_ENABLED=false. Upstream updates and independent driver
launches have their own settings. Third-party components have their own licenses
and policies; the bundled MCP runtime includes its dependency license notices.

SideScreen is not a security sandbox or an independent Windows session. Enable
only tools appropriate to the task. Disabling the plugin disconnects its local
tools; use the CUA pause control to stop the separate supervisor. Uninstalling
this plugin does not delete user apps, files or browser data.

For maintenance or privacy questions, open a GitHub issue. Do not include private
screenshots, tokens, profile data or credentials in public reports.
