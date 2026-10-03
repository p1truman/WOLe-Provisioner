using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using WOLe.Provisioner.Services;

namespace WOLe.Provisioner.Views
{
    public sealed partial class EnvironmentSetupPage : Page, WOLe.Provisioner.Models.IWizardPage
    {
        private readonly ArduinoCliInstaller _installer = new();
        private readonly IProgress<string> _progress;

        private bool _setupComplete = false;

        public EnvironmentSetupPage()
        {
            InitializeComponent();

            _progress = new Progress<string>(msg =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    LogTextBox.Text += msg + Environment.NewLine;
                    LogScroll.ChangeView(null, double.MaxValue, null);
                });
            });
        }

        private async void SetupButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;
            _setupComplete = false;

            SetupButton.IsEnabled = false;
            SkipButton.IsEnabled = false;

            Progress.Visibility = Visibility.Visible;
            Progress.IsIndeterminate = false;
            Progress.Value = 0;

            LogTextBox.Text = "Starting environment setup..." + Environment.NewLine;

            try
            {
                UpdateProgress(10);

                await _installer.EnsureEnvironmentAsync(_progress);

                UpdateProgress(100);
                _setupComplete = true;

                DispatcherQueue.TryEnqueue(() =>
                {
                    LogTextBox.Text += "Environment setup complete." + Environment.NewLine;
                    SetupButton.Content = "Completed";
                    SetupButton.IsEnabled = false;
                    SkipButton.IsEnabled = true;
                });
            }
            catch (Exception ex)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    LogTextBox.Text += $"ERROR: {ex.Message}" + Environment.NewLine;
                    SetupButton.IsEnabled = true;
                    SkipButton.IsEnabled = true;
                });
            }
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            _setupComplete = true;
            ErrorText.Visibility = Visibility.Collapsed;

            // No log text written when skipping
            WizardService.GoNext(this);
        }

        private void UpdateProgress(double value)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                Progress.Value = value;
            });
        }

        public bool ValidateAndSave()
        {
            if (!_setupComplete)
            {
                ErrorText.Text = "Environment setup must be completed before continuing.";
                ErrorText.Visibility = Visibility.Visible;
                return false;
            }

            ErrorText.Visibility = Visibility.Collapsed;
            return true;
        }
    }
}