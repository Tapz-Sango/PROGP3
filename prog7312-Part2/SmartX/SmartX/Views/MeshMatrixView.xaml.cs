using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SmartX.Services;

namespace SmartX.Views
{
    public partial class MeshMatrixView : UserControl
    {
        private readonly DispatcherTimer _timer = new();
        private readonly ApiService _apiService = new();
        private readonly Random _rnd = new();

        private readonly ObservableCollection<AlertRow> _alerts = new();
        private readonly System.Collections.Generic.HashSet<string> _recentKeys = new();

        private const string Node1Mac = "AD:76:E3:5F:59:93";
        private const string Node2Mac = "C8:2B:96:11:02:AA";
        private const string Node3Mac = "1C:46:79:A5:5C:E5";

        public MeshMatrixView()
        {
            InitializeComponent();

            LstIssues.ItemsSource = _alerts;

            _timer.Interval = TimeSpan.FromMilliseconds(1500);
            _timer.Tick += Timer_Tick;

            Loaded += (_, __) => _timer.Start();
            Unloaded += (_, __) => _timer.Stop();
        }

        private async void Timer_Tick(object? sender, EventArgs e)
        {
            // Node 1 — Temperature
            double temp = Math.Round(18.0 + _rnd.NextDouble() * 22.0, 1);
            Val1.Text = $"Telemetry: {temp} °C";
            bool tempAlert = temp > 35.0;
            ApplyNodeState(Node1, Dot1, tempAlert);
            await _apiService.IngestTelemetryAsync(Node1Mac, temp, "°C", tempAlert);
            if (tempAlert)
                RaiseAlert("NODE-ESP-01", Node1Mac, $"Temperature {temp} °C exceeds 35 °C", "Critical");

            // Node 2 — Power
            int watts = _rnd.Next(400, 2500);
            Val2.Text = $"Telemetry: {watts} W";
            bool powerAlert = watts > 2000;
            ApplyNodeState(Node2, Dot2, powerAlert);
            await _apiService.IngestTelemetryAsync(Node2Mac, watts, "W", powerAlert);
            if (powerAlert)
                RaiseAlert("NODE-ESP-02", Node2Mac, $"Power spike {watts} W exceeds 2000 W", "Critical");

            // Node 3 — Actuator heartbeat
            bool valveActive = _rnd.NextDouble() > 0.15;
            Val3.Text = $"Valve: {(valveActive ? "ACTIVE" : "IDLE")}";
            ApplyNodeState(Node3, Dot3, alert: false);
            await _apiService.IngestTelemetryAsync(Node3Mac, valveActive ? 1 : 0, "STATE", false);

            TxtAlertCount.Text = $"Total alerts: {_alerts.Count}";
        }

        private void ApplyNodeState(Border node, System.Windows.Shapes.Ellipse dot, bool alert)
        {
            if (alert)
            {
                node.BorderBrush = Brushes.Red;
                dot.Fill = Brushes.Red;
            }
            else
            {
                var cyan = (Brush)Application.Current.Resources["AccentCyan"];
                node.BorderBrush = cyan;
                dot.Fill = cyan;
            }
        }

        private void RaiseAlert(string nodeName, string mac, string message, string severity)
        {
            string key = $"{mac}::{message}::{DateTime.UtcNow:HH:mm}";
            if (!_recentKeys.Add(key)) return;

            _alerts.Insert(0, new AlertRow
            {
                Timestamp = DateTime.Now,
                Node = nodeName,
                MacAddress = mac,
                Message = message,
                Severity = severity,
                SeverityBrush = severity switch
                {
                    "Critical" => new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)),
                    "Warning" => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
                    _ => new SolidColorBrush(Color.FromRgb(0x02, 0x84, 0xC7))
                }
            });

            while (_alerts.Count > 50) _alerts.RemoveAt(_alerts.Count - 1);
            _ = _apiService.PostIssueAsync($"[{nodeName}] {message}");
        }

        private void BtnClearFeed_Click(object sender, RoutedEventArgs e)
        {
            _alerts.Clear();
            _recentKeys.Clear();
            TxtAlertCount.Text = "Total alerts: 0";
        }

        //  Back button — now uses central NavigationService 
        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            NavigationService.PopOverlayOrGoToShell();
        }
    }

    public class AlertRow
    {
        public DateTime Timestamp { get; set; }
        public string Node { get; set; } = "";
        public string MacAddress { get; set; } = "";
        public string Message { get; set; } = "";
        public string Severity { get; set; } = "Info";
        public Brush SeverityBrush { get; set; } = Brushes.SteelBlue;
    }
}