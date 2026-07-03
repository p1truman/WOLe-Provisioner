using System;

namespace WOLe.Provisioner.Models
{
    public static class PageKeys
    {
        public static readonly Type WelcomePage          = typeof(WOLe.Provisioner.Views.WelcomePage);
        public static readonly Type SystemConfigPageOne  = typeof(WOLe.Provisioner.Views.SystemConfigPageOne);
        public static readonly Type SinricAccountPage    = typeof(WOLe.Provisioner.Views.SinricAccountPage);
        public static readonly Type WifiPage             = typeof(WOLe.Provisioner.Views.WifiPage);
        public static readonly Type SinricPage           = typeof(WOLe.Provisioner.Views.SinricPage);
        public static readonly Type PcConfigPage         = typeof(WOLe.Provisioner.Views.PcConfigPage);
        public static readonly Type ActionSwitchPage     = typeof(WOLe.Provisioner.Views.ActionSwitchPage);
        public static readonly Type ShutdownSecretPage   = typeof(WOLe.Provisioner.Views.ShutdownSecretPage);
        public static readonly Type ShutdownInstallPage  = typeof(WOLe.Provisioner.Views.ShutdownInstallPage);
        public static readonly Type EnvironmentSetupPage = typeof(WOLe.Provisioner.Views.EnvironmentSetupPage);
        public static readonly Type FirmwarePage         = typeof(WOLe.Provisioner.Views.FirmwarePage);
        public static readonly Type FlashFirmwarePage    = typeof(WOLe.Provisioner.Views.FlashFirmwarePage);
        public static readonly Type FinalPage            = typeof(WOLe.Provisioner.Views.FinalPage);
        public static readonly Type ShutdownPcConfigPage = typeof(WOLe.Provisioner.Views.ShutdownPcConfigPage);
        public static readonly Type ShutdownActionMappingPage = typeof(WOLe.Provisioner.Views.ShutdownActionMappingPage);
        public static readonly Type ShutdownCompletePage = typeof(WOLe.Provisioner.Views.ShutdownCompletePage);
    }
}