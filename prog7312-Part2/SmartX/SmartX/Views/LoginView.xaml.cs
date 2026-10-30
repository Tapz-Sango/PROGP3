using System.Windows;
using System.Windows.Controls;
using SmartX.Services;

namespace SmartX.Views
{
    public partial class LoginView : UserControl
    {
        private readonly ApiService _apiService = new();

        public LoginView() => InitializeComponent();

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            TxtError.Visibility = Visibility.Collapsed;
            BtnLogin.IsEnabled = false;

            try
            {
                var (ok, error) = await _apiService.AuthenticateAsync(TxtEmail.Text.Trim(), TxtPassword.Password);
                if (ok)
                {
                    // Route through the central service so the shell becomes the active root and overlay stack is reset.
                    NavigationService.GoToShell();
                }
                else
                {
                    TxtError.Text = error ?? "Authentication failed.";
                    TxtError.Visibility = Visibility.Visible;
                }
            }
            finally { BtnLogin.IsEnabled = true; }
        }

        private void BtnGuest_Click(object sender, RoutedEventArgs e)
        {
            TxtEmail.Text = "guest@smartx.io";
            TxtPassword.Password = "admin123";
        }
    }
}