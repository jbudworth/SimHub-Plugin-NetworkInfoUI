# SimHub.Plugin.NetworkInfo

A small SimHub plugin that exposes local network information as SimHub
properties, so it can be bound to any dashboard element (including an
Arduino character LCD) alongside your existing telemetry.

## Properties exposed

Refreshed on the interval set in the settings screen (5 seconds by default):

- `NetworkInfoPluginUI.Hostname`
- `NetworkInfoPluginUI.WifiIP`
- `NetworkInfoPluginUI.WifiNetmask`
- `NetworkInfoPluginUI.WifiGateway`
- `NetworkInfoPluginUI.WifiDhcp` (`"DHCP"` or `"Manual"`)
- `NetworkInfoPluginUI.EthernetIP`
- `NetworkInfoPluginUI.EthernetNetmask`
- `NetworkInfoPluginUI.EthernetGateway`
- `NetworkInfoPluginUI.EthernetDhcp`
- `NetworkInfoPluginUI.AllIPs` (every active WiFi/Ethernet IPv4 address, joined with `|`)

## Settings screen

The plugin has its own page under **Settings > Plugins > Network Info** in
SimHub, styled to match SimHub's other plugin settings screens
(`SHSection`, `SHButtonPrimary`, etc.):

- **Current network status** - a live, read-only table of everything listed
  above, refreshing every 2 seconds so you can confirm values without
  leaving the page.
- **Settings** - a slider for the refresh interval (1-30s, default 5s) used
  by the background timer that actually updates the SimHub properties.
  Click **Save settings** (top-right) to apply and persist a new value -
  it's picked up immediately, no restart needed.

## Setup

1. Open `SimHub.Plugin.NetworkInfoUI.slnx` in Visual Studio (Community edition is fine).
2. Open `SimHub.Plugin.NetworkInfoUI/SimHub.Plugin.NetworkInfoUI.csproj` and check
   the `<SimHubInstallDir>` property near the top - update it if SimHub isn't
   installed at the default path (`C:\Program Files (x86)\SimHub`).
3. Build the solution (Ctrl+Shift+B).
   - The project references `SimHub.Plugins.dll` and `GameReaderCommon.dll`
     directly from your SimHub install folder, so SimHub must already be
     installed on this machine before building.
   - After a successful build, the project automatically copies
     `SimHub.Plugin.NetworkInfoUI.dll` into your SimHub folder. If the copy
     fails with a permissions error, either run Visual Studio as
     Administrator or copy the DLL from
     `SimHub.Plugin.NetworkInfoUI\bin\Debug\net48\SimHub.Plugin.NetworkInfoUI.dll`
     into the SimHub folder by hand.
4. Start (or restart) SimHub. It should detect the new plugin - enable it
   under **Settings > Plugins** if it isn't already active (listed as
   "Network Info Plugin").
5. In Dash Studio (or your ini-based LCD template), bind a text element to
   whichever `NetworkInfoPluginUI.*` property you want to display, the same
   way you'd bind any other SimHub property.

## Notes

- Network info is refreshed on an internal timer (interval configurable in
  the settings screen, default 5s) rather than every game-data frame, since
  it doesn't change quickly and doesn't need to be checked 60 times a second.
- No internet/public IP lookup is included - everything here is read
  locally from the machine's network adapters.
- If you have multiple WiFi or Ethernet adapters, the plugin picks the
  first "up" one of each type. Let me know if you need it to prefer a
  specific adapter by name.
- Settings (currently just the refresh interval) are stored by SimHub in
  its own plugin-settings store, under the plugin's `GeneralSettings` key -
  no separate config file to manage.
- The `IWPFSettingsV2.GetWPFSettingsControl` / `PictureIcon` interface
  members follow the shape used across other SimHub plugin projects
  (`SimHub.Plugin.HueControl`, `SonoffSwitch`, `TapoSwitch`); if a build
  error points at that interface specifically, the SimHub SDK demo project
  at `C:\Program Files (x86)\SimHub\PluginSdk\User.PluginSdkDemo` is the
  authoritative reference for its exact signature on your installed
  SimHub version.
