using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SimHub.Plugin.NetworkInfo
{
    public partial class SettingsControl : UserControl
    {
        private readonly NetworkInfoPlugin _plugin;
        private readonly ObservableCollection<StatusRow> _statusRows = new ObservableCollection<StatusRow>();
        private readonly DispatcherTimer _uiRefreshTimer;

        // Parameterless constructor kept for the XAML designer only - SimHub
        // always uses the (NetworkInfoPlugin) constructor below at runtime.
        public SettingsControl()
        {
            InitializeComponent();
        }

        public SettingsControl(NetworkInfoPlugin plugin)
        {
            InitializeComponent();
            _plugin = plugin;

            StatusGrid.ItemsSource = _statusRows;
            RefreshIntervalSlider.Value = _plugin.GetRefreshIntervalSeconds();

            RefreshStatusGrid();

            // Refreshes the on-screen status table only; independent of the
            // plugin's own background timer, which drives the SimHub properties.
            _uiRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _uiRefreshTimer.Tick += (s, e) => RefreshStatusGrid();
            _uiRefreshTimer.Start();

            // Unloaded fires every time the user switches to another SimHub
            // page, and the same control instance may be shown again later -
            // so restart on Loaded rather than stopping permanently.
            Loaded += (s, e) =>
            {
                RefreshStatusGrid();
                _uiRefreshTimer.Start();
            };
            Unloaded += (s, e) => _uiRefreshTimer.Stop();
        }

        private void RefreshStatusGrid()
        {
            if (_plugin == null) return;

            var latest = _plugin.GetStatusSnapshot();
            for (int i = 0; i < latest.Count; i++)
            {
                if (i < _statusRows.Count)
                {
                    if (_statusRows[i].Value != latest[i].Value)
                    {
                        _statusRows[i] = latest[i];
                    }
                }
                else
                {
                    _statusRows.Add(latest[i]);
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_plugin == null) return;

            int seconds = (int)Math.Round(RefreshIntervalSlider.Value);
            _plugin.SetRefreshIntervalSeconds(seconds);

            // Reflect any clamping the plugin applied back into the slider.
            RefreshIntervalSlider.Value = _plugin.GetRefreshIntervalSeconds();

            StatusText.Text = $"Saved. Refresh interval set to {_plugin.GetRefreshIntervalSeconds()}s.";
        }
    }
}
