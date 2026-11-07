using System.Windows;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for DogeProfilesPage.xaml
    /// </summary>
    public partial class DogeProfilesPage
    {
        // Roblox only honours Fast Flags that are on its allowlist (all other flags are silently ignored).
        // Flags with OnAllowlist = false are kept because they were explicitly requested, but the user is told
        // that they most likely do nothing. Re-check the allowlist before adding any new flag here.
        private sealed record ProfileFlag(string Key, string Value, bool OnAllowlist = true);

        private static readonly ProfileFlag[] MaxFpsFlags =
        {
            new("DFIntTaskSchedulerTargetFps", "240", OnAllowlist: false),
            new("DFIntDebugFRMQualityLevelOverride", "1"),
            new("FIntDebugForceMSAASamples", "1"),
            new("DFFlagTextureQualityOverrideEnabled", "True"),
            new("DFIntTextureQualityOverride", "1")
        };

        private static readonly ProfileFlag[] CinematicFlags =
        {
            new("DFIntDebugFRMQualityLevelOverride", "21"),
            new("FIntDebugForceMSAASamples", "4")
        };

        private static readonly ProfileFlag[] PvpFlags =
        {
            new("DFIntDebugFRMQualityLevelOverride", "7"),
            new("FIntDebugForceMSAASamples", "1")
        };

        public DogeProfilesPage()
        {
            InitializeComponent();
        }

        private void MaxFpsButton_Click(object sender, RoutedEventArgs e) => ApplyProfile("Max FPS", MaxFpsFlags);

        private void CinematicButton_Click(object sender, RoutedEventArgs e) => ApplyProfile("Cinematic", CinematicFlags);

        private void PvpButton_Click(object sender, RoutedEventArgs e) => ApplyProfile("PVP", PvpFlags);

        private static void ApplyProfile(string profileName, ProfileFlag[] flags)
        {
            foreach (var flag in flags)
                App.FastFlags.SetValue(flag.Key, flag.Value);

            App.FastFlags.Save();

            var message = new StringBuilder();
            message.Append($"The \"{profileName}\" profile has been applied ({flags.Length} flags saved). It takes effect the next time Roblox is launched.");

            var ignored = flags.Where(x => !x.OnAllowlist).Select(x => x.Key).ToArray();

            if (ignored.Length > 0)
                message.Append($"\n\nNote: Roblox ignores Fast Flags that are not on its allowlist. These are currently not on it and will most likely have no effect: {string.Join(", ", ignored)}.");

            if (!App.Settings.Prop.UseFastFlagManager)
                message.Append("\n\nThe Fast Flag manager is turned off in the Fast Flags tab, so these flags will not be used until you turn it on and save your settings.");

            Frontend.ShowMessageBox(message.ToString(), MessageBoxImage.Information);
        }
    }
}
