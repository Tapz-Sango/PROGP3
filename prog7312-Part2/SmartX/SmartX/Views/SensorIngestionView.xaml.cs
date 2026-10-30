using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SmartX.Models;
using SmartX.Services;

namespace SmartX.Views
{
    public partial class SensorIngestionView : UserControl
    {
        private readonly ApiService _apiService = new();
        private readonly List<SensorRegistration> _localSensors = new();
        private string _selectedFilePath = string.Empty;

        public SensorIngestionView()
        {
            InitializeComponent();
            Loaded += async (_, __) => await LoadSensorsAsync();
        }

        private async System.Threading.Tasks.Task LoadSensorsAsync()
        {
            try
            {
                var devices = await _apiService.GetDevicesAsync();
                _localSensors.Clear();
                if (devices != null && devices.Count > 0)
                {
                    foreach (var d in devices)
                    {
                        _localSensors.Add(new SensorRegistration
                        {
                            MacAddress = d.MacAddress,
                            Location = string.IsNullOrWhiteSpace(d.Name) ? d.Location : $"{d.Name} @ {d.Location}",
                            Category = d.Category,
                            RegisteredAt = d.RegisteredAt == default ? DateTime.UtcNow : d.RegisteredAt
                        });
                    }
                }
                RefreshGrid();
            }
            catch { /* silent */ }
        }

        private void RefreshGrid()
        {
            DgSensors.ItemsSource = null;
            DgSensors.ItemsSource = _localSensors;
        }

        private async void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            string mac = TxtMac.Text.Trim();
            string location = TxtLocation.Text.Trim();
            string category = (CmbCategory.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Environmental";

            if (!Regex.IsMatch(mac, "^[0-9A-Fa-f]{2}(:[0-9A-Fa-f]{2}){5}$"))
            {
                MessageBox.Show("MAC address must follow XX:XX:XX:XX:XX:XX", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(location)) { MessageBox.Show("Enter a deployment location.", "Validation"); return; }

            var payload = new { MacAddress = mac, Name = mac, Location = location, Category = category, AlertState = "Normal" };
            _localSensors.Add(new SensorRegistration { MacAddress = mac, Location = location, Category = category, RegisteredAt = DateTime.UtcNow });
            RefreshGrid();
            TxtMac.Clear(); TxtLocation.Clear();

            bool ok = await _apiService.RegisterSensorAsync(payload);
            if (ok)
            {
                MessageBox.Show("Sensor registered via API!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                await System.Threading.Tasks.Task.Delay(300);
                await LoadSensorsAsync();
            }
            else MessageBox.Show("Registered locally, but API rejected/unreachable.", "API Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void BtnRandomSeed_Click(object sender, RoutedEventArgs e)
        {
            var rnd = new Random();
            var bytes = new byte[6];
            rnd.NextBytes(bytes);
            string mac = string.Join(":", bytes.Select(b => b.ToString("X2")));
            string location = $"Zone {rnd.Next(1, 10)} - SubFacility {rnd.Next(100, 999)}";
            string category = (CmbCategory.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Environmental";

            _localSensors.Add(new SensorRegistration { MacAddress = mac, Location = location, Category = category, RegisteredAt = DateTime.UtcNow });
            RefreshGrid();
        }

        private void BtnSelectFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Device Lists (*.txt;*.csv)|*.txt;*.csv|Config & Logs (*.log;*.json)|*.log;*.json|Images (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All Files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                _selectedFilePath = dlg.FileName;
                TxtFilePath.Text = Path.GetFileName(_selectedFilePath);
            }
        }

        private async void BtnUploadFile_Click(object sender, RoutedEventArgs e)
        {
            if (DgSensors.SelectedItem is not SensorRegistration selected)
            {
                MessageBox.Show("Select a target sensor first.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(_selectedFilePath) || !File.Exists(_selectedFilePath))
            {
                MessageBox.Show("Select a file first.", "File Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var (ok, message, imported) = await _apiService.UploadDeviceFileAsync(selected.MacAddress, _selectedFilePath);
            if (ok)
            {
                MessageBox.Show($"Uploaded & encrypted!\nDevices auto-imported: {imported}\n\n{message}",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                _selectedFilePath = string.Empty;
                TxtFilePath.Text = "No file attached";
                await System.Threading.Tasks.Task.Delay(400);
                await LoadSensorsAsync();
            }
            else MessageBox.Show($"Upload failed.\n\n{message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void BtnValidateTree_Click(object sender, RoutedEventArgs e)
        {
            var rootFacility = new DeploymentNode("Main Hydroponics Hub", "Facility A", true);
            var zone1 = new DeploymentNode("Zone 1 Automated Greenhouse", "Zone 1", true, rootFacility);
            var subZoneB = new DeploymentNode("Sub-Zone B Micro-controllers", "Sub-Zone B", true, zone1);
            var nodeESP = new DeploymentNode("ESP32-Soil-Node-09", "Node ID", true, subZoneB);

            bool ok = DeploymentNode.RecursiveValidateHierarchy(nodeESP, out string reason);
            MessageBox.Show(ok
                ? $"Recursive Validation Success!\nNode hierarchy [{nodeESP.Name}] safely nested. Base case reached."
                : $"Validation Failure: {reason}",
                ok ? "Hierarchy Audit Passed" : "Audit Failed",
                MessageBoxButton.OK,
                ok ? MessageBoxImage.Information : MessageBoxImage.Error);
        }

        //  Navigation — routed through the shell 
        private void BtnBack_Click(object sender, RoutedEventArgs e)
            => NavigationService.PopOverlayOrGoToShell();

        private void BtnOpenTelemetry_Click(object sender, RoutedEventArgs e)
            => NavigationService.PushOverlay(new TelemetryStreamView());
    }
}