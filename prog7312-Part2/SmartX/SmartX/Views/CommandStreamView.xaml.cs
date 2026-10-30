using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SmartX.Models;
using SmartX.Services;

namespace SmartX.Views
{
    public partial class CommandStreamView : UserControl
    {
        private readonly ApiService _api = new();
        private readonly DispatcherTimer _autoStream = new() { Interval = TimeSpan.FromMilliseconds(1800) };
        private readonly DispatcherTimer _chartTick = new() { Interval = TimeSpan.FromMilliseconds(2000) };
        private List<Device> _allDevices = new();
        private readonly Random _rnd = new();
        private bool _isReady;

        public CommandStreamView()
        {
            InitializeComponent();
            _autoStream.Tick += AutoStream_Tick;
            _chartTick.Tick += ChartTick_Tick;

            Loaded += async (_, __) =>
            {
                await LoadDevicesAsync();
                await LoadHistoryAsync();
                await LoadInsightsAsync();
                await RefreshChartAsync();
                _chartTick.Start();
                _isReady = true;
            };

            Unloaded += (_, __) =>
            {
                _autoStream.Stop();
                _chartTick.Stop();
            };
        }

        private async System.Threading.Tasks.Task LoadDevicesAsync()
        {
            _allDevices = await _api.GetDevicesAsync() ?? new List<Device>();
            ApplyFilter();
        }

        private async System.Threading.Tasks.Task LoadHistoryAsync()
        {
            var history = await _api.GetCommandHistoryAsync();
            LstHistory.ItemsSource = history;
            TxtUndoHint.Text = $"Stack: {history.Count} command(s)";
        }

        private async System.Threading.Tasks.Task LoadInsightsAsync()
        {
            var insights = await _api.GetInsightsAsync();
            LstInsights.ItemsSource = insights;
        }

        private async System.Threading.Tasks.Task RefreshChartAsync()
        {
            try
            {
                var points = await _api.GetTimelineAsync(take: 40);
                var chartPoints = points
                    .OrderBy(p => p.Timestamp)
                    .Select(p => (p.Timestamp, p.Value, p.IsCritical))
                    .ToList();
                Chart.SetPoints(chartPoints);
            }
            catch { /* silent */ }
        }

        private async void ChartTick_Tick(object? sender, EventArgs e)
        {
            await RefreshChartAsync();
            await UpdateQueuePillsAsync();
        }

        private async System.Threading.Tasks.Task UpdateQueuePillsAsync()
        {
            var snapshot = await _api.GetQueueSnapshotAsync();
            if (snapshot != null)
            {
                TxtCriticalCount.Text = $"CRITICAL: {snapshot.Critical?.Count ?? 0}";
                TxtFifoCount.Text = $"FIFO: {snapshot.Standard?.Count ?? 0}";
            }
        }

        //  Filter (guarded against constructor-time events) 
        private void ApplyFilter()
        {
            if (!_isReady || TxtSearch == null || DgDevices == null) return;
            var devices = _allDevices ?? new List<Device>();
            string term = TxtSearch.Text?.Trim() ?? "";
            string? cat = (CmbCategory?.SelectedItem as ComboBoxItem)?.Content?.ToString();
            string? state = (CmbAlertState?.SelectedItem as ComboBoxItem)?.Content?.ToString();

            var filtered = devices.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(term))
                filtered = filtered.Where(d =>
                    (d.MacAddress ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (d.Name ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (d.Location ?? "").Contains(term, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(cat) && cat != "All")
                filtered = filtered.Where(d => (d.Category ?? "").Equals(cat, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(state) && state != "All")
                filtered = filtered.Where(d => (d.AlertState ?? "").Equals(state, StringComparison.OrdinalIgnoreCase));

            DgDevices.ItemsSource = filtered.ToList();
        }

        private async void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
            if (!_isReady) return;
            var term = TxtSearch.Text?.Trim();
            if (string.IsNullOrEmpty(term) || term.Length < 3) return;

            var cat = (CmbCategory?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            var suggestions = await _api.LogSearchAsync(term, cat);
            var insights = await _api.GetInsightsAsync();
            LstInsights.ItemsSource = suggestions.Concat(insights).ToList();
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isReady) return;
            ApplyFilter();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadDevicesAsync();
            await LoadInsightsAsync();
        }

        private void DgDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgDevices.SelectedItem is Device d)
            {
                TxtSelectedDevice.Text = $"Selected: {d.Name} ({d.MacAddress})";
                TxtSelectedForChart.Text = $"{d.Name}";
                _ = LoadChartForDeviceAsync(d.MacAddress);
            }
        }

        private async System.Threading.Tasks.Task LoadChartForDeviceAsync(string mac)
        {
            var points = await _api.GetTimelineAsync(mac, take: 40);
            var chartPoints = points
                .OrderBy(p => p.Timestamp)
                .Select(p => (p.Timestamp, p.Value, p.IsCritical))
                .ToList();
            Chart.SetPoints(chartPoints);
        }

        //  Commands 
        private async void BtnPumpOn_Click(object sender, RoutedEventArgs e) => await IssueCommandAsync("PUMP_ON");
        private async void BtnPumpOff_Click(object sender, RoutedEventArgs e) => await IssueCommandAsync("PUMP_OFF");
        private async void BtnRelayToggle_Click(object sender, RoutedEventArgs e) => await IssueCommandAsync("RELAY_TOGGLE");

        private async System.Threading.Tasks.Task IssueCommandAsync(string command)
        {
            if (DgDevices.SelectedItem is not Device dev)
            {
                MessageBox.Show("Select a device first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            bool ok = await _api.IssueCommandAsync(dev.MacAddress, command, dev.AlertState);
            if (ok)
            {
                await LoadHistoryAsync();
                await LoadInsightsAsync();
            }
            else MessageBox.Show("Command failed. Check API connectivity.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private async void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            var result = await _api.UndoLastCommandAsync();
            MessageBox.Show(result?.Message ?? "Nothing to undo.", "Undo", MessageBoxButton.OK, MessageBoxImage.Information);
            await LoadHistoryAsync();
            await LoadInsightsAsync();
        }

        //  Simulator 
        private async void BtnSimulateNormal_Click(object sender, RoutedEventArgs e)
            => await SendRandomTelemetryAsync(false);

        private async void BtnSimulateCritical_Click(object sender, RoutedEventArgs e)
            => await SendRandomTelemetryAsync(true);

        private void BtnStartAuto_Click(object sender, RoutedEventArgs e) => _autoStream.Start();
        private void BtnStopAuto_Click(object sender, RoutedEventArgs e) => _autoStream.Stop();

        private async void AutoStream_Tick(object? sender, EventArgs e)
        {
            bool critical = _rnd.NextDouble() < 0.30;
            await SendRandomTelemetryAsync(critical);
        }

        private async System.Threading.Tasks.Task SendRandomTelemetryAsync(bool forceCritical)
        {
            if (_allDevices == null || _allDevices.Count == 0) return;
            var dev = _allDevices[_rnd.Next(_allDevices.Count)];

            double value;
            string unit;
            var cat = dev.Category ?? "";
            if (cat.Contains("Moisture", StringComparison.OrdinalIgnoreCase))
            {
                value = forceCritical ? _rnd.Next(5, 19) : _rnd.Next(30, 80);
                unit = "%";
            }
            else if (cat.Contains("Power", StringComparison.OrdinalIgnoreCase))
            {
                value = forceCritical ? _rnd.Next(2100, 3500) : _rnd.Next(300, 1800);
                unit = "W";
            }
            else if (cat.Contains("Temperature", StringComparison.OrdinalIgnoreCase))
            {
                value = forceCritical ? _rnd.Next(36, 48) : _rnd.Next(18, 32);
                unit = "°C";
            }
            else
            {
                value = _rnd.Next(0, 100);
                unit = "";
            }

            await _api.IngestTelemetryAsync(dev.MacAddress, value, unit, forceCritical);
            await LoadDevicesAsync();
            await UpdateQueuePillsAsync();
            await LoadInsightsAsync();
            await RefreshChartAsync();
        }
    }
}