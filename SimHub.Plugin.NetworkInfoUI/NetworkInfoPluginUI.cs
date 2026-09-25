using GameReaderCommon;
using SimHub.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Timers;
using System.Windows.Controls;
using System.Windows.Media;

namespace SimHub.Plugin.NetworkInfo
{
    [PluginDescription("Exposes WiFi/Ethernet IP, netmask, gateway, DHCP status and hostname as SimHub properties")]
    [PluginAuthor("Claude.ai")]
    [PluginName("Network Info Plugin UI")]
    public class NetworkInfoPlugin : IPlugin, IDataPlugin, IWPFSettingsV2
    {
        public PluginManager PluginManager { get; set; }

        // IWPFSettingsV2 - controls how the plugin shows up in SimHub's left-hand
        // settings menu and which control is displayed when it's selected.
        public ImageSource PictureIcon => null;
        public string LeftMenuTitle => "Network Info";

        public Control GetWPFSettingsControl(PluginManager pluginManager)
        {
            return new SettingsControl(this);
        }

        // Persisted settings - currently just the refresh interval. Read/written
        // via SimHub's common-settings helpers, so it survives restarts.
        public class Settings
        {
            public int RefreshIntervalSeconds = 5;
        }

        private Settings _settings;
        private Timer _localTimer;

        // Guards the fields below: written by _localTimer's Elapsed callback
        // (a background thread) and read by the settings screen's UI-thread
        // timer via GetStatusSnapshot.
        private readonly object _statusLock = new object();

        private string _hostname = "Starting...";
        private string _wifiIP = "Starting...";
        private string _wifiNetmask = "";
        private string _wifiGateway = "";
        private string _wifiDhcp = "";
        private string _ethernetIP = "Starting...";
        private string _ethernetNetmask = "";
        private string _ethernetGateway = "";
        private string _ethernetDhcp = "";
        private string _allIPs = "Starting...";

        public void Init(PluginManager pluginManager)
        {
            PluginManager = pluginManager;

            _settings = this.ReadCommonSettings<Settings>("GeneralSettings", () => new Settings());
            _settings.RefreshIntervalSeconds = ClampRefreshInterval(_settings.RefreshIntervalSeconds);

            this.AttachDelegate("NetworkInfoPluginUI.Hostname", () => _hostname);

            this.AttachDelegate("NetworkInfoPluginUI.WifiIP", () => _wifiIP);
            this.AttachDelegate("NetworkInfoPluginUI.WifiNetmask", () => _wifiNetmask);
            this.AttachDelegate("NetworkInfoPluginUI.WifiGateway", () => _wifiGateway);
            this.AttachDelegate("NetworkInfoPluginUI.WifiDhcp", () => _wifiDhcp);

            this.AttachDelegate("NetworkInfoPluginUI.EthernetIP", () => _ethernetIP);
            this.AttachDelegate("NetworkInfoPluginUI.EthernetNetmask", () => _ethernetNetmask);
            this.AttachDelegate("NetworkInfoPluginUI.EthernetGateway", () => _ethernetGateway);
            this.AttachDelegate("NetworkInfoPluginUI.EthernetDhcp", () => _ethernetDhcp);

            this.AttachDelegate("NetworkInfoPluginUI.AllIPs", () => _allIPs);

            _hostname = Dns.GetHostName();

            _localTimer = new Timer(_settings.RefreshIntervalSeconds * 1000);
            _localTimer.Elapsed += (s, e) => UpdateLocalInfo();
            _localTimer.AutoReset = true;
            _localTimer.Start();
            UpdateLocalInfo();
        }

        // 1-30s, matching the settings screen's slider and the README.
        private static int ClampRefreshInterval(int seconds)
        {
            if (seconds < 1) return 1;
            if (seconds > 30) return 30;
            return seconds;
        }

        // Called by the settings screen when the user changes the refresh
        // interval and clicks Save. Clamped, persisted, and applied to the
        // running timer immediately (no restart needed).
        public void SetRefreshIntervalSeconds(int seconds)
        {
            seconds = ClampRefreshInterval(seconds);

            _settings.RefreshIntervalSeconds = seconds;
            this.SaveCommonSettings("GeneralSettings", _settings);

            if (_localTimer != null)
            {
                _localTimer.Interval = seconds * 1000;
            }
        }

        public int GetRefreshIntervalSeconds() => _settings?.RefreshIntervalSeconds ?? 5;

        // Snapshot of the current values, in display order, for the settings
        // screen's status grid. Kept separate from the AttachDelegate calls
        // above since those feed SimHub's property system, not WPF binding.
        public List<StatusRow> GetStatusSnapshot()
        {
            lock (_statusLock)
            {
                return new List<StatusRow>
                {
                    new StatusRow("Hostname", _hostname),
                    new StatusRow("WiFi IP", _wifiIP),
                    new StatusRow("WiFi netmask", _wifiNetmask),
                    new StatusRow("WiFi gateway", _wifiGateway),
                    new StatusRow("WiFi DHCP", _wifiDhcp),
                    new StatusRow("Ethernet IP", _ethernetIP),
                    new StatusRow("Ethernet netmask", _ethernetNetmask),
                    new StatusRow("Ethernet gateway", _ethernetGateway),
                    new StatusRow("Ethernet DHCP", _ethernetDhcp),
                    new StatusRow("All IPs", _allIPs),
                };
            }
        }

        private void UpdateLocalInfo()
        {
            try
            {
                var upInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                    .ToList();

                var wifi = GetInterfaceDetails(upInterfaces, NetworkInterfaceType.Wireless80211);
                var ethernet = GetInterfaceDetails(upInterfaces, NetworkInterfaceType.Ethernet)
                               ?? GetInterfaceDetails(upInterfaces, NetworkInterfaceType.GigabitEthernet);

                var allAddresses = upInterfaces
                    .Where(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211
                               || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                               || ni.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet)
                    .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                    .Where(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(addr => addr.Address.ToString())
                    .Distinct();

                string allIPs = string.Join(" | ", allAddresses);
                if (string.IsNullOrEmpty(allIPs)) allIPs = "No IP found";

                lock (_statusLock)
                {
                    _wifiIP = wifi?.ip ?? "No WiFi link";
                    _wifiNetmask = wifi?.netmask ?? "";
                    _wifiGateway = wifi?.gateway ?? "";
                    _wifiDhcp = wifi?.dhcp ?? "";

                    _ethernetIP = ethernet?.ip ?? "No Ethernet link";
                    _ethernetNetmask = ethernet?.netmask ?? "";
                    _ethernetGateway = ethernet?.gateway ?? "";
                    _ethernetDhcp = ethernet?.dhcp ?? "";

                    _allIPs = allIPs;
                }
            }
            catch (Exception ex)
            {
                lock (_statusLock)
                {
                    _wifiIP = "Error";
                    _wifiNetmask = "";
                    _wifiGateway = "";
                    _wifiDhcp = "";

                    _ethernetIP = "Error";
                    _ethernetNetmask = "";
                    _ethernetGateway = "";
                    _ethernetDhcp = "";

                    _allIPs = "Error: " + ex.Message;
                }
            }
        }

        // Returns null (not a default tuple) when no matching adapter is found,
        // so callers can use ?. and ?? cleanly.
        private static (string ip, string netmask, string gateway, string dhcp)? GetInterfaceDetails(
            List<NetworkInterface> interfaces, NetworkInterfaceType type)
        {
            var ni = interfaces.FirstOrDefault(n => n.NetworkInterfaceType == type);
            if (ni == null) return null;

            var ipProps = ni.GetIPProperties();

            var unicast = ipProps.UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
            if (unicast == null) return null;

            string ip = unicast.Address.ToString();

            string netmask = "Unknown";
            try { netmask = unicast.IPv4Mask.ToString(); } catch { /* not available on some adapter types */ }

            var gatewayInfo = ipProps.GatewayAddresses
                .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);
            string gateway = gatewayInfo?.Address.ToString() ?? "None";

            string dhcp;
            switch (unicast.PrefixOrigin)
            {
                case PrefixOrigin.Dhcp:
                    dhcp = "DHCP";
                    break;
                case PrefixOrigin.Manual:
                    dhcp = "Manual";
                    break;
                default:
                    dhcp = unicast.PrefixOrigin.ToString();
                    break;
            }

            return (ip, netmask, gateway, dhcp);
        }

        public void End(PluginManager pluginManager)
        {
            _localTimer?.Stop();
            _localTimer?.Dispose();
        }

        // Required by IDataPlugin, but not needed here since network info is
        // refreshed on its own timer rather than per game-data frame.
        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
        }
    }

    // Simple Property/Value row for the settings screen's read-only status grid.
    public class StatusRow
    {
        public string Property { get; }
        public string Value { get; }

        public StatusRow(string property, string value)
        {
            Property = property;
            Value = value;
        }
    }
}
