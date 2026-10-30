using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SmartX.Services;

namespace SmartX.Views
{
    public partial class MainShellView : UserControl
    {
        private SensorIngestionView? _ingestionView;
        private CommandStreamView? _commandView;

        public MainShellView()
        {
            InitializeComponent();
            TxtUser.Text = AuthSession.Email ?? "guest";

            // Cache both tab views so switching preserves state.
            _ingestionView = new SensorIngestionView();
            _commandView = new CommandStreamView();
            IngestionContentHost.Children.Add(_ingestionView);
            HostCommand.Children.Add(_commandView);

            // Start on Tab 2 so the marker sees the Part 2 feature first.
            ActivateTab(2);
        }

        // TABS
        private void Tab1_Click(object sender, RoutedEventArgs e) => ActivateTab(1);
        private void Tab2_Click(object sender, RoutedEventArgs e) => ActivateTab(2);

        public void ActivateTab(int index)
        {
            // Closing an overlay when the user clicks a tab is the natural behaviour — otherwise they'd be stuck looking at the overlay.
            HideOverlay();

            HostIngestion.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
            HostCommand.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;

            StyleTab(Tab1, index == 1);
            StyleTab(Tab2, index == 2);
        }

        private void StyleTab(Button tab, bool active)
        {
            tab.Foreground = active
                ? (Brush)Application.Current.Resources["AccentBlue"]
                : (Brush)Application.Current.Resources["TextSecondary"];

            if (tab.Template.FindName("bd", tab) is Border bd)
            {
                bd.BorderBrush = active
                    ? (Brush)Application.Current.Resources["AccentBlue"]
                    : Brushes.Transparent;
            }
        }

        // OVERLAY
        public void ShowOverlay(UserControl view, string? title = null)
        {
            OverlayContentHost.Children.Clear();
            OverlayContentHost.Children.Add(view);
            TxtOverlayTitle.Text = title ?? view.GetType().Name.Replace("View", "");
            OverlayHost.Visibility = Visibility.Visible;
        }

        public void HideOverlay()
        {
            OverlayContentHost.Children.Clear();
            OverlayHost.Visibility = Visibility.Collapsed;
            TxtOverlayTitle.Text = string.Empty;
        }

        private void BtnOverlayBack_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.PopOverlayOrGoToShell();
        }

        // PART 1 ARCHIVE STRIP — routes through NavigationService
        private void OpenMeshRadar_Click(object sender, RoutedEventArgs e)
            => NavigationService.PushOverlay(new MeshMatrixView());

        private void OpenTelemetryStream_Click(object sender, RoutedEventArgs e)
            => NavigationService.PushOverlay(new TelemetryStreamView());

        private void OpenGatewayMenu_Click(object sender, RoutedEventArgs e)
            => NavigationService.PushOverlay(new GatewayMenuView());

        // LOGOUT
        private void BtnLogout_Click(object sender, RoutedEventArgs e)
            => NavigationService.Logout();
    }
}