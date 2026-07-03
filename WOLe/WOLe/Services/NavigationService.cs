using Microsoft.UI.Xaml.Controls;
using System;

namespace WOLe.Provisioner.Services
{
    public static class NavigationService
    {
        private static Frame? _frame;

        public static void Initialize(Frame frame)
        {
            _frame = frame;
        }

        public static void Navigate(Type pageType, object? parameter = null)
        {
            _frame?.Navigate(pageType, parameter);
        }

        public static void GoBack()
        {
            if (_frame != null && _frame.CanGoBack)
                _frame.GoBack();
        }
    }
}
