using System.Windows;
using SmartX.Views;

namespace SmartX
{
    public partial class MainWindow : Window
    {
        public static MainWindow? Instance { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;
            NavigateTo(new LandingView()); // was the LoginView
        }

        public void NavigateTo(UIElement view)
        {
            MainContentFrame.Content = view;
        }
    }
}