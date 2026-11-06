using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

using Wpf.Ui.Controls.Interfaces;
using Wpf.Ui.Mvvm.Contracts;

using Bloxstrap.UI.ViewModels.Settings;

using WpfNavigationItem = Wpf.Ui.Controls.NavigationItem;

namespace Bloxstrap.UI.Elements.Settings
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INavigationWindow
    {
        private Models.Persistable.WindowState _state => App.State.Prop.SettingsWindow;

        public MainWindow(bool showAlreadyRunningWarning)
        {
            var viewModel = new MainWindowViewModel();

            viewModel.RequestSaveNoticeEvent += (_, _) => SettingsSavedSnackbar.Show();
            viewModel.RequestCloseWindowEvent += (_, _) => Close();

            DataContext = viewModel;
            
            InitializeComponent();

            App.Logger.WriteLine("MainWindow", "Initializing settings window");

            if (showAlreadyRunningWarning)
                ShowAlreadyRunningSnackbar();

            LoadState();

            // wait until the navigation has finished its own initial navigation, otherwise it would override ours
            Loaded += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(RestoreLastSettingsTab));
        }

        private const string FastFlagEditorTag = "fastflageditor";

        private IEnumerable<WpfNavigationItem> GetNavigationItems() => RootNavigation.Items.Concat(RootNavigation.Footer).OfType<WpfNavigationItem>();

        private string? GetCurrentTabTag()
        {
            Type? pageType = RootFrame.Content?.GetType();

            if (pageType is null)
                return null;

            string? tag = GetNavigationItems().FirstOrDefault(x => x.PageType == pageType)?.Tag as string;

            // the editor is a hidden sub-page that can't be opened directly, so fall back to its parent tab
            return tag == FastFlagEditorTag ? "fastflags" : tag;
        }

        private void RestoreLastSettingsTab()
        {
            string tag = _state.LastSettingsTab;

            if (string.IsNullOrEmpty(tag) || tag == FastFlagEditorTag)
                return;

            var item = GetNavigationItems().FirstOrDefault(x => x.PageType is not null && x.Visibility == Visibility.Visible && x.Tag as string == tag);

            if (item is not null)
                RootNavigation.Navigate(item.PageType);
        }

        public void LoadState()
        {
            if (_state.Left > SystemParameters.VirtualScreenWidth)
                _state.Left = 0;

            if (_state.Top > SystemParameters.VirtualScreenHeight)
                _state.Top = 0;

            if (_state.Width > 0)
                this.Width = _state.Width;

            if (_state.Height > 0)
                this.Height = _state.Height;

            if (_state.Left > 0 && _state.Top > 0)
            {
                this.WindowStartupLocation = WindowStartupLocation.Manual;
                this.Left = _state.Left;
                this.Top = _state.Top;
            }
        }

        private async void ShowAlreadyRunningSnackbar()
        {
            await Task.Delay(500); // wait for everything to finish loading
            AlreadyRunningSnackbar.Show();
        }

        #region INavigationWindow methods

        public Frame GetFrame() => RootFrame;

        public INavigation GetNavigation() => RootNavigation;

        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

        public void SetPageService(IPageService pageService) => RootNavigation.PageService = pageService;

        public void ShowWindow() => Show();

        public void CloseWindow() => Close();

        #endregion INavigationWindow methods

        private void WpfUiWindow_Closing(object sender, CancelEventArgs e)
        {
            if (App.FastFlags.Changed || App.PendingSettingTasks.Any())
            {
                var result = Frontend.ShowMessageBox(Strings.Menu_UnsavedChanges, MessageBoxImage.Warning, MessageBoxButton.YesNo);

                if (result != MessageBoxResult.Yes)
                    e.Cancel = true;
            }
            
            _state.Width = this.Width;
            _state.Height = this.Height;

            _state.Top = this.Top;
            _state.Left = this.Left;

            _state.LastSettingsTab = GetCurrentTabTag() ?? _state.LastSettingsTab;

            App.State.Save();
        }

        private void WpfUiWindow_Closed(object sender, EventArgs e)
        {
            if (App.LaunchSettings.TestModeFlag.Active)
                LaunchHandler.LaunchRoblox(LaunchMode.Player);
            else
                App.SoftTerminate();
        }
    }
}
