using System.Windows;
using System.Windows.Controls;
using SmartX.Services;

namespace SmartX.Views
{
    public partial class GatewayMenuView : UserControl
    {
        public GatewayMenuView() => InitializeComponent();

        //All navigate within the shell as overlays so Back always returns.
        private void BtnSensorIngestion_Click(object sender, RoutedEventArgs e)
            => NavigationService.PushOverlay(new SensorIngestionView());

        private void BtnCommandStream_Click(object sender, RoutedEventArgs e)
            => NavigationService.GoToCommandStreamTab();

        private void BtnTelemetryStream_Click(object sender, RoutedEventArgs e)
            => NavigationService.PushOverlay(new TelemetryStreamView());

        private void BtnMeshMatrix_Click(object sender, RoutedEventArgs e)
            => NavigationService.PushOverlay(new MeshMatrixView());
    }
}