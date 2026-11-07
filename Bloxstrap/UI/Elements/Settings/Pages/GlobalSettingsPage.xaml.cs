using System.Windows.Controls;
using System.Windows.Input;

using Bloxstrap.UI.ViewModels.Settings;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for GlobalSettingsPage.xaml
    /// </summary>
    public partial class GlobalSettingsPage
    {
        public GlobalSettingsPage()
        {
            DataContext = new GlobalSettingsViewModel();
            InitializeComponent();
        }

        private void ValidateFramerate(object sender, TextCompositionEventArgs e)
        {
            // digits and a leading minus only; the value is range-checked again on save
            e.Handled = !e.Text.All(c => char.IsDigit(c) || c == '-');
        }

        private void AdvancedGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            // for tags that already exist in the file only the value is editable
            if (e.Row.Item is GlobalSettingEntry { IsExisting: true } && (e.Column.Header as string) != "Value")
                e.Cancel = true;
        }
    }
}
