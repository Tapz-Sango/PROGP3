using System.Windows;
using System.Windows.Controls;
using SmartX.Services;

namespace SmartX.Views
{
    //Landing Page
    public partial class LandingView : UserControl
    {
        public LandingView() => InitializeComponent();

        private void BtnEnter_Click(object sender, RoutedEventArgs e)
            => NavigationService.GoToLogin();
    }
}