using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using SmartX.Views;

namespace SmartX.Services
{
    
    public static class NavigationService
    {
        private static MainShellView? _currentShell;
        private static readonly Stack<(UserControl View, UserControl? ReturnTo)> _overlayStack = new();

        // ROOT NAVIGATION — used by Landing / Login / Logout

        public static void GoToRoot(UserControl view)
        {
            _overlayStack.Clear();
            _currentShell = view as MainShellView;
            MainWindow.Instance?.NavigateTo(view);
        }

        public static void GoToLogin() => GoToRoot(new LoginView());
        public static void GoToLanding() => GoToRoot(new LandingView());

        //Shows the tabbed shell after successful login.
        public static void GoToShell()
        {
            _overlayStack.Clear();
            var shell = new MainShellView();
            _currentShell = shell;
            MainWindow.Instance?.NavigateTo(shell);
        }

 
        // OVERLAY NAVIGATION — opens a view on top of the shell

        
        public static void PushOverlay(UserControl view)
        {
            if (_currentShell == null)
            {
                // Fallback: no shell currently active — show full-screen.
                MainWindow.Instance?.NavigateTo(view);
                return;
            }

            _overlayStack.Push((view, null));
            _currentShell.ShowOverlay(view);
        }
        public static void PopOverlayOrGoToShell()
        {
            if (_overlayStack.Count > 0)
            {
                var entry = _overlayStack.Pop();
                if (_overlayStack.Count == 0)
                {
                    // Last overlay popped — close the overlay panel entirely.
                    _currentShell?.HideOverlay();
                }
                else
                {
                    // Show the previous overlay in the stack.
                    var previous = _overlayStack.Peek().View;
                    _currentShell?.ShowOverlay(previous);
                }
            }
            else
            {
                // No overlays open — just show the shell.
                if (_currentShell == null) _currentShell = new MainShellView();
                MainWindow.Instance?.NavigateTo(_currentShell);
            }
        }

        // SHELL-AWARE HELPERS used by CommandStreamView's in-tab nav
        public static void GoToSensorIngestionTab()
        {
            if (_currentShell != null)
            {
                _currentShell.ActivateTab(1);
            }
            else
            {
                GoToShell();
            }
        }

        public static void GoToCommandStreamTab()
        {
            if (_currentShell != null)
            {
                _currentShell.ActivateTab(2);
            }
            else
            {
                GoToShell();
            }
        }

        // Logout which clears auth and returns to landing.
        public static void Logout()
        {
            new ApiService().Logout();
            GoToLanding();
        }
    }
}