using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using WOLe.Provisioner.Services;

namespace WOLe.Provisioner.Views
{
    public sealed partial class SplashPage : Page
    {
        private readonly DispatcherTimer _timer;

        public SplashPage()
        {
            InitializeComponent();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(4.7)
            };

            _timer.Tick += Timer_Tick;
            Loaded += SplashPage_Loaded;
            Unloaded += SplashPage_Unloaded;
            ActualThemeChanged += SplashPage_ActualThemeChanged;
        }

        private void SplashPage_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateSplashForTheme();
            _timer.Start();
        }

        private void SplashPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            _timer.Tick -= Timer_Tick;
        }

        private void SplashPage_ActualThemeChanged(FrameworkElement sender, object args)
        {
            UpdateSplashForTheme();
        }

        private void UpdateSplashForTheme()
        {
            var isDark = ActualTheme == ElementTheme.Dark;

            SplashRoot.Background = new SolidColorBrush(
                isDark ? Microsoft.UI.Colors.Black : Microsoft.UI.Colors.White);

            var splashPath = isDark
                ? "ms-appx:///Assets/StartupSplash-Dark.gif"
                : "ms-appx:///Assets/StartupSplash.gif";

            SplashImage.Source = new BitmapImage(new Uri(splashPath));

            if (isDark)
            {
                SplashImage.Width = 1000;
                SplashImage.Height = double.NaN;
                SplashImage.Stretch = Stretch.Uniform;
                SplashImage.HorizontalAlignment = HorizontalAlignment.Center;
                SplashImage.VerticalAlignment = VerticalAlignment.Center;
                SplashImage.Margin = new Thickness(0);
            }
            else
            {
                SplashImage.Width = 1000;
                SplashImage.Height = double.NaN;
                SplashImage.Stretch = Stretch.Uniform;
                SplashImage.HorizontalAlignment = HorizontalAlignment.Center;
                SplashImage.VerticalAlignment = VerticalAlignment.Center;
                SplashImage.Margin = new Thickness(0, 0, 0, 0);
            }
        }

        private void Timer_Tick(object? sender, object e)
        {
            _timer.Stop();
            NavigationService.Navigate(typeof(WelcomePage));
        }
    }
}