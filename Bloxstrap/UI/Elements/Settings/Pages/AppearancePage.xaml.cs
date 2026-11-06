using Bloxstrap.UI.ViewModels.Settings;

using System.Windows;
using System.Windows.Controls;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for AppearancePage.xaml
    /// </summary>
    public partial class AppearancePage
    {
        public AppearancePage()
        {
            DataContext = new AppearanceViewModel(this);
            InitializeComponent();
        }

        private bool _loadingAccent = false;

        // the ComboBox can't be named (x:Name) because it lives inside an OptionControl, which has its own name scope
        private void DogeAccentComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            var comboBox = (ComboBox)sender;

            // selecting an item programmatically raises SelectionChanged, so don't treat it as a user choice
            _loadingAccent = true;

            comboBox.ItemsSource = DogeAccents.Names;
            comboBox.SelectedItem = DogeAccents.Names.Contains(App.Settings.Prop.DogeAccent) ? App.Settings.Prop.DogeAccent : DogeAccents.SystemDefault;

            _loadingAccent = false;
        }

        private void DogeAccentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loadingAccent || ((ComboBox)sender).SelectedItem is not string name)
                return;

            App.Settings.Prop.DogeAccent = name == DogeAccents.SystemDefault ? "" : name;
            DogeAccents.Apply(App.Settings.Prop.DogeAccent);
        }

        public void CustomThemeSelection(object sender, SelectionChangedEventArgs e)
        {
            AppearanceViewModel viewModel = (AppearanceViewModel)DataContext;

            viewModel.SelectedCustomTheme = (string)((ListBox)sender).SelectedItem;
            viewModel.SelectedCustomThemeName = viewModel.SelectedCustomTheme;

            viewModel.OnPropertyChanged(nameof(viewModel.SelectedCustomTheme));
            viewModel.OnPropertyChanged(nameof(viewModel.SelectedCustomThemeName));
        }
    }
}
