using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Runtime.InteropServices;

using WOLe.Provisioner.Models;
using WOLe.Provisioner.Services;
using WOLe.Provisioner.Views;

namespace WOLe.Provisioner
{
    public sealed partial class MainWindow : Window
    {
        // Logical design size — the window will always open at least this large
        // in logical pixels regardless of DPI/scaling.
        private const int DesignWidth  = 1050;
        private const int DesignHeight = 840;

        public MainWindow()
        {
            InitializeComponent();

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            // Scale logical design size to physical pixels for the current monitor DPI.
            // This ensures the window is always DesignWidth x DesignHeight logical pixels
            // regardless of what scaling the user has set (100%, 125%, 150%, 175%, 200%...).
            int dpi = GetDpiForWindow(hwnd);
            double scale = dpi / 96.0;

            int physicalWidth  = (int)Math.Round(DesignWidth  * scale);
            int physicalHeight = (int)Math.Round(DesignHeight * scale);

            appWindow.Resize(new Windows.Graphics.SizeInt32(physicalWidth, physicalHeight));

            // Set a minimum window size so users cannot shrink it below the design size.
            // This requires subclassing the window procedure via SetWindowSubclass.
            SetMinimumSize(hwnd, physicalWidth, physicalHeight);

            appWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));

            NavigationService.Initialize(RootFrame);
            NavigationService.Navigate(typeof(SplashPage));

            RootFrame.Navigated += RootFrame_Navigated;
        }

        // ----------------------------------------------------------------
        //  MINIMUM SIZE ENFORCEMENT via WM_GETMINMAXINFO
        // ----------------------------------------------------------------

        private static WndProcDelegate? _wndProcDelegate;
        private static IntPtr _originalWndProc = IntPtr.Zero;
        private static int _minWidth;
        private static int _minHeight;

        private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        private static void SetMinimumSize(IntPtr hwnd, int minWidth, int minHeight)
        {
            _minWidth  = minWidth;
            _minHeight = minHeight;

            _wndProcDelegate = CustomWndProc;
            _originalWndProc = SetWindowLongPtr(hwnd, GWLP_WNDPROC,
                Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
        }

        private static IntPtr CustomWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_GETMINMAXINFO)
            {
                var info = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                info.ptMinTrackSize.x = _minWidth;
                info.ptMinTrackSize.y = _minHeight;
                Marshal.StructureToPtr(info, lParam, false);
                return IntPtr.Zero;
            }

            return CallWindowProc(_originalWndProc, hwnd, msg, wParam, lParam);
        }

        // ----------------------------------------------------------------
        //  WIN32 INTEROP
        // ----------------------------------------------------------------

        private const int  GWLP_WNDPROC    = -4;
        private const uint WM_GETMINMAXINFO = 0x0024;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [DllImport("user32.dll")] private static extern int GetDpiForWindow(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int nIndex, IntPtr newProc);
        [DllImport("user32.dll")] private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        // ----------------------------------------------------------------
        //  NAVIGATION
        // ----------------------------------------------------------------

        private void RootFrame_Navigated(object sender, NavigationEventArgs e)
        {
            var page = e.SourcePageType;

            WizardFooter.Visibility = page == typeof(WelcomePage) || page == typeof(SplashPage)
                ? Visibility.Collapsed
                : Visibility.Visible;

            if (page == typeof(FinalPage) || page == typeof(ShutdownCompletePage))
            {
                NextButton.Visibility = Visibility.Collapsed;
                BackButton.Visibility = Visibility.Collapsed;
                return;
            }

            if (page == typeof(SplashPage))
            {
                NextButton.Visibility = Visibility.Collapsed;
                BackButton.Visibility = Visibility.Collapsed;
                return;
            }

            NextButton.Visibility = Visibility.Visible;
            BackButton.Visibility = Visibility.Visible;

            bool isDonePage =
                (page == typeof(SystemConfigPageOne) && App.CurrentMode == WizardMode.FullWoleSetup) ||
                page == typeof(SinricAccountPage);

            NextButton.Content = isDonePage ? "Done" : "Next";
        }

        private void Back_Click(object sender, RoutedEventArgs e)  => WizardService.GoBack();
        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (RootFrame.Content is SystemConfigPageOne step1Page)
            {
                if (!step1Page.ValidateAndSave()) return;

                if (App.CurrentMode == WizardMode.FullWoleSetup)
                {
                    SetStepComplete(cfg => cfg.Step1Completed = true);
                    WizardService.GoToWelcome();
                }
                else
                {
                    WizardService.GoNext(RootFrame.Content);
                }
                return;
            }

            if (RootFrame.Content is SinricAccountPage step2Page)
            {
                if (!step2Page.ValidateAndSave()) return;

                SetStepComplete(cfg => cfg.Step2Completed = true);
                WizardService.GoToWelcome();
                return;
            }

            WizardService.GoNext(RootFrame.Content);
        }

        private static void SetStepComplete(Action<ProvisioningConfig> setFlag)
        {
            var cfg = ProvisioningState.Current ?? new ProvisioningConfig();
            setFlag(cfg);
            ProvisioningState.Update(cfg);
        }
    }
}