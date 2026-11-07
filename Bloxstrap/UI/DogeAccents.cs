using System.Windows.Media;

using Wpf.Ui.Appearance;

namespace Bloxstrap.UI
{
    /// <summary>
    /// Doge accent colours. Uses Wpf.Ui's Accent.Apply, which sets SystemAccentColor and also derives the
    /// Primary/Secondary/Tertiary accent resources and brushes that the controls actually read.
    /// </summary>
    static class DogeAccents
    {
        public const string SystemDefault = "System default";

        private static readonly Dictionary<string, Color> _accents = new()
        {
            { "Doge Orange", Color.FromRgb(0xF2, 0x8C, 0x28) },
            { "Shiba Tan", Color.FromRgb(0xD9, 0xA0, 0x66) },
            { "Dark Moon", Color.FromRgb(0x62, 0x72, 0xA4) }
        };

        public static IReadOnlyList<string> Names { get; } = new[] { SystemDefault }.Concat(_accents.Keys).ToArray();

        /// <summary>
        /// Applies the named accent. Returns false (and changes nothing) for an empty/unknown name or the system default.
        /// </summary>
        public static bool TryApply(string? name)
        {
            if (string.IsNullOrEmpty(name) || !_accents.TryGetValue(name, out Color color))
                return false;

            Accent.Apply(color, App.Settings.Prop.Theme.GetFinal() == Enums.Theme.Dark ? ThemeType.Dark : ThemeType.Light);
            return true;
        }

        public static void Apply(string? name)
        {
            if (!TryApply(name))
                Accent.ApplySystemAccent();
        }
    }
}
