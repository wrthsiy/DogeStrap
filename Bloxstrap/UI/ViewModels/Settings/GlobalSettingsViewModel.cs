using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class GlobalSettingsViewModel : NotifyPropertyChangedViewModel
    {
        private const string RobloxRunningMessage =
            "Roblox is running. Close Roblox (RobloxPlayerBeta) and save again - otherwise it would overwrite your changes when it exits. Nothing was saved.\n\n" +
            "Закройте Roblox и повторите сохранение. Изменения не сохранены.";

        private readonly GlobalSettingsManager _manager = new();

        // shown when the file doesn't contain the tag yet. A tag is only written to the file if you press Save.
        private double _graphicsQualityLevel = 10;
        private string _framerateCap = "60";
        private double _preferredTransparency = 1;
        private bool _lockFile = false;

        public ICommand SaveCommand => new RelayCommand(Save);

        public ICommand ReloadCommand => new RelayCommand(() => Load(true));

        public ObservableCollection<GlobalSettingEntry> AdvancedEntries { get; } = new();

        public GlobalSettingsViewModel()
        {
            if (EditorEnabled)
            {
                GlobalSettingsManager.EnsureFileExists();
                Load(true);
            }
        }

        public bool EditorEnabled
        {
            get => App.Settings.Prop.EnableGlobalSettingsEditor;
            set
            {
                App.Settings.Prop.EnableGlobalSettingsEditor = value;
                OnPropertyChanged(nameof(EditorEnabled));

                if (value)
                {
                    GlobalSettingsManager.EnsureFileExists();
                    Load(true);
                }
            }
        }

        public string FilePath => GlobalSettingsManager.FilePath;

        public string FileStatusText => File.Exists(GlobalSettingsManager.FilePath)
            ? $"{GlobalSettingsManager.FilePath}"
            : $"{GlobalSettingsManager.FilePath} (does not exist yet, it is created when you enable the editor or launch Roblox)";

        public double GraphicsQualityLevel
        {
            get => _graphicsQualityLevel;
            set
            {
                _graphicsQualityLevel = value;
                OnPropertyChanged(nameof(GraphicsQualityLevel));
            }
        }

        public string FramerateCap
        {
            get => _framerateCap;
            set
            {
                _framerateCap = value;
                OnPropertyChanged(nameof(FramerateCap));
            }
        }

        public double PreferredTransparency
        {
            get => _preferredTransparency;
            set
            {
                _preferredTransparency = value;
                OnPropertyChanged(nameof(PreferredTransparency));
            }
        }

        public bool LockFile
        {
            get => _lockFile;
            set
            {
                _lockFile = value;
                OnPropertyChanged(nameof(LockFile));
            }
        }

        private static double ParseNumber(string? text, double fallback, double min, double max)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) || double.IsNaN(result))
                return fallback;

            return Math.Clamp(result, min, max);
        }

        private void Load(bool reportErrors)
        {
            string? error = _manager.Load();

            if (error is not null)
            {
                if (reportErrors)
                    Frontend.ShowMessageBox($"{GlobalSettingsManager.FileName} could not be read:\n\n{error}", MessageBoxImage.Warning);

                OnPropertyChanged(nameof(FileStatusText));
                return;
            }

            GraphicsQualityLevel = ParseNumber(_manager.GetValue("GraphicsQualityLevel"), GraphicsQualityLevel, 1, 21);
            PreferredTransparency = ParseNumber(_manager.GetValue("PreferredTransparency"), PreferredTransparency, 0, 3);

            string? framerate = _manager.GetValue("FramerateCap");

            if (framerate is not null)
                FramerateCap = framerate;

            LockFile = GlobalSettingsManager.IsReadOnly();

            AdvancedEntries.Clear();

            foreach (var entry in _manager.GetAdvancedEntries())
                AdvancedEntries.Add(entry);

            OnPropertyChanged(nameof(FileStatusText));
        }

        private void Save()
        {
            // 1. never touch the file while Roblox is running
            if (GlobalSettingsManager.IsRobloxRunning())
            {
                Frontend.ShowMessageBox(RobloxRunningMessage, MessageBoxImage.Warning);
                return;
            }

            if (!_manager.IsLoaded)
            {
                Frontend.ShowMessageBox($"{GlobalSettingsManager.FileName} could not be read, so nothing was saved. Fix or delete the file, then press Reload.", MessageBoxImage.Warning);
                return;
            }

            // 2. validate everything before anything is changed
            if (!int.TryParse(FramerateCap.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int framerate) || framerate < -1 || framerate > 10000)
            {
                Frontend.ShowMessageBox("Framerate cap must be a whole number between -1 and 10000.", MessageBoxImage.Warning);
                return;
            }

            string? entriesError = _manager.ValidateEntries(AdvancedEntries);

            if (entriesError is not null)
            {
                Frontend.ShowMessageBox(entriesError, MessageBoxImage.Warning);
                return;
            }

            // 3. apply and write (the manager makes the .bak copy and checks for Roblox once more)
            _manager.SetValue("GraphicsQualityLevel", "int", ((int)Math.Round(GraphicsQualityLevel)).ToString(CultureInfo.InvariantCulture));
            _manager.SetValue("FramerateCap", "int", framerate.ToString(CultureInfo.InvariantCulture));
            _manager.SetValue("PreferredTransparency", "float", PreferredTransparency.ToString("0.###", CultureInfo.InvariantCulture));
            _manager.ApplyEntries(AdvancedEntries);

            switch (_manager.Save(LockFile, out string? error))
            {
                case GlobalSettingsSaveResult.Saved:
                    Load(false); // refresh the grid so newly added tags become regular rows
                    Frontend.ShowMessageBox($"Saved. The previous version was copied to {GlobalSettingsManager.FileName}.bak", MessageBoxImage.Information);
                    break;

                case GlobalSettingsSaveResult.RobloxRunning:
                    Frontend.ShowMessageBox(RobloxRunningMessage, MessageBoxImage.Warning);
                    break;

                case GlobalSettingsSaveResult.NotLoaded:
                    Frontend.ShowMessageBox("The settings file is not loaded, so nothing was saved. Press Reload.", MessageBoxImage.Warning);
                    break;

                default:
                    Frontend.ShowMessageBox($"Failed to save {GlobalSettingsManager.FileName}:\n\n{error}", MessageBoxImage.Error);
                    break;
            }
        }
    }
}
