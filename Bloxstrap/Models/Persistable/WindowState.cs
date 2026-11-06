namespace Bloxstrap.Models.Persistable
{
    public class WindowState
    {
        public double Width { get; set; }

        public double Height { get; set; }

        public double Left { get; set; }

        public double Top { get; set; }

        // Tag of the settings navigation item that was open when the window was last closed
        public string LastSettingsTab { get; set; } = "";
    }
}
