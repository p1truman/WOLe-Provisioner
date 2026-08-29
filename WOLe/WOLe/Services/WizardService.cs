using System;
using System.Linq;
using WOLe.Provisioner.Models;
using WOLe.Provisioner.Views;

namespace WOLe.Provisioner.Services
{
    public static class WizardService
    {
        private static readonly Type[] FullWolePages =
        {
            typeof(WelcomePage),
            PageKeys.SystemConfigPageOne,
            PageKeys.SinricAccountPage,
            PageKeys.WifiPage,
            PageKeys.SinricPage,
            PageKeys.PcConfigPage,
            PageKeys.ShutdownSecretPage,
            PageKeys.ActionSwitchPage,
            PageKeys.ShutdownInstallPage,
            PageKeys.EnvironmentSetupPage,
            PageKeys.FlashFirmwarePage,
            PageKeys.FinalPage
        };

        private static readonly Type[] ShutdownOnlyPages =
        {
            typeof(WelcomePage),
            PageKeys.SystemConfigPageOne,
            PageKeys.ShutdownPcConfigPage,
            PageKeys.ShutdownSecretPage,
            PageKeys.ShutdownActionMappingPage,
            PageKeys.ShutdownInstallPage,
            PageKeys.ShutdownCompletePage
        };

        private static int _index = 0;
        private static int _entryIndex = 0;
        private static int _actionStepIndex = 0;

        private static Type[] ActivePages =>
            App.CurrentMode == WizardMode.ShutdownOnly
                ? ShutdownOnlyPages
                : FullWolePages;

        public static void Reset()
        {
            _index = 0;
            _entryIndex = 0;
            _actionStepIndex = 0;
        }

        public static void StartWizard()
        {
            ProvisioningState.Ensure();
            _index = 0;
            _entryIndex = 0;
            _actionStepIndex = 0;
            NavigationService.Navigate(ActivePages[0]);
        }

        public static void StartAfterWelcome()
        {
            ProvisioningState.Ensure();
            _index = 1;
            _entryIndex = 0;
            _actionStepIndex = 0;
            NavigationService.Navigate(ActivePages[_index]);
        }

        public static void StartFrom(Type pageType)
        {
            ProvisioningState.Ensure();

            var pages = ActivePages;
            var targetIndex = Array.IndexOf(pages, pageType);
            _index = targetIndex >= 0 ? targetIndex : 0;
            _entryIndex = _index;
            _actionStepIndex = 0;

            NavigationService.Navigate(pages[_index]);
        }

        public static void GoNext(object currentPage)
        {
            if (currentPage is IWizardPage wizardPage)
            {
                if (!wizardPage.ValidateAndSave())
                    return;
            }

            var pages = ActivePages;

            if (_index < pages.Length - 1)
            {
                if (App.CurrentMode == WizardMode.FullWoleSetup &&
                    currentPage is ShutdownSecretPage)
                {
                    var targets = GetEnhancedActionTargets();
                    if (targets.Count == 0)
                    {
                        _index = Array.IndexOf(pages, PageKeys.ShutdownInstallPage);
                        NavigationService.Navigate(pages[_index]);
                        return;
                    }

                    _actionStepIndex = 0;
                    _index = Array.IndexOf(pages, PageKeys.ActionSwitchPage);
                    NavigationService.Navigate(pages[_index], _actionStepIndex);
                    return;
                }

                if (App.CurrentMode == WizardMode.FullWoleSetup &&
                    currentPage is ActionSwitchPage)
                {
                    var targets = GetEnhancedActionTargets();
                    if (_actionStepIndex < targets.Count - 1)
                    {
                        _actionStepIndex++;
                        _index = Array.IndexOf(pages, PageKeys.ActionSwitchPage);
                        NavigationService.Navigate(pages[_index], _actionStepIndex);
                        return;
                    }

                    _index = Array.IndexOf(pages, PageKeys.ShutdownInstallPage);
                    NavigationService.Navigate(pages[_index]);
                    return;
                }

                _index++;
                NavigationService.Navigate(pages[_index]);
            }
        }

        public static void GoBack()
        {
            var pages = ActivePages;

            if (_index > 0)
            {
                if (_index == _entryIndex && _entryIndex > 1)
                {
                    _index = 0;
                    _entryIndex = 0;
                    NavigationService.Navigate(pages[_index]);
                    return;
                }

                if (App.CurrentMode == WizardMode.FullWoleSetup &&
                    pages[_index] == PageKeys.ActionSwitchPage)
                {
                    if (_actionStepIndex > 0)
                    {
                        _actionStepIndex--;
                        NavigationService.Navigate(pages[_index], _actionStepIndex);
                        return;
                    }

                    _index = Array.IndexOf(pages, PageKeys.ShutdownSecretPage);
                    NavigationService.Navigate(pages[_index]);
                    return;
                }

                if (App.CurrentMode == WizardMode.FullWoleSetup &&
                    pages[_index] == PageKeys.ShutdownInstallPage)
                {
                    var targets = GetEnhancedActionTargets();
                    if (targets.Count > 0)
                    {
                        _actionStepIndex = targets.Count - 1;
                        _index = Array.IndexOf(pages, PageKeys.ActionSwitchPage);
                        NavigationService.Navigate(pages[_index], _actionStepIndex);
                        return;
                    }

                    _index = Array.IndexOf(pages, PageKeys.ShutdownSecretPage);
                    NavigationService.Navigate(pages[_index]);
                    return;
                }

                _index--;
                NavigationService.Navigate(pages[_index]);
            }
        }

        public static void GoToWelcome()
        {
            var pages = ActivePages;
            _index = 0;
            _entryIndex = 0;
            _actionStepIndex = 0;
            NavigationService.Navigate(pages[_index]);
        }

        public static EnhancedActionTarget? GetCurrentEnhancedActionTarget()
        {
            var cfg = ProvisioningState.Current;
            if (cfg?.Pcs == null)
                return null;

            var targets = GetEnhancedActionTargets();
            if (targets.Count == 0)
                return null;

            if (_actionStepIndex < 0 || _actionStepIndex >= targets.Count)
                _actionStepIndex = 0;

            return targets[_actionStepIndex];
        }

        private static System.Collections.Generic.List<EnhancedActionTarget> GetEnhancedActionTargets()
        {
            var cfg = ProvisioningState.Current;
            var targets = new System.Collections.Generic.List<EnhancedActionTarget>();

            if (cfg?.Pcs == null)
                return targets;

            for (int pcIndex = 0; pcIndex < cfg.Pcs.Count; pcIndex++)
            {
                var pc = cfg.Pcs[pcIndex];
                var enhancedDevices = (pc.EnhancedDevices ?? new System.Collections.Generic.List<EnhancedDeviceConfig>())
                    .Where(d => !string.IsNullOrWhiteSpace(d.DeviceId))
                    .Take(ProvisioningLimits.MaxEnhancedDevicesPerPc)
                    .ToList();

                for (int enhancedIndex = 0; enhancedIndex < enhancedDevices.Count; enhancedIndex++)
                {
                    targets.Add(new EnhancedActionTarget
                    {
                        PcIndex = pcIndex,
                        EnhancedDeviceIndex = enhancedIndex,
                        DeviceId = enhancedDevices[enhancedIndex].DeviceId
                    });
                }
            }

            return targets;
        }
    }
}