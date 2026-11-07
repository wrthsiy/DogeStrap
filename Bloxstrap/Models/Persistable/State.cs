namespace Bloxstrap.Models.Persistable
{
    public class State
    {
        public bool PromptWebView2Install { get; set; } = true;

        public bool ForceReinstall { get; set; } = false;

        public WindowState SettingsWindow { get; set; } = new();

        // auto-rejoin: the public server that is currently being played (set when joining, cleared when leaving)
        public long LastPlaceId { get; set; } = 0;
        public string LastJobId { get; set; } = "";

        // auto-rejoin: crash-loop protection
        public int AutoRejoinAttempts { get; set; } = 0;
        public DateTime LastAutoRejoinTime { get; set; } = DateTime.MinValue;
    }
}
