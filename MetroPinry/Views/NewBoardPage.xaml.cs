using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using MetroPinry.Models;
using MetroPinry.Resources;
using MetroPinry.Services;

namespace MetroPinry.Views
{
    public partial class NewBoardPage : PhoneApplicationPage
    {
        public static string NewlyCreatedBoardName { get; set; }
        public static Board BoardToEdit { get; set; }

        public NewBoardPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            UpdateAppBarLocalization();

            if (BoardToEdit != null)
            {
                if (PageTitle != null)
                {
                    PageTitle.Text = AppResources.EditBoard;
                }

                TxtBoardName.Text = BoardToEdit.Name ?? string.Empty;
                ChkIsPrivate.IsChecked = BoardToEdit.IsPrivate;
            }
            else
            {
                if (PageTitle != null)
                {
                    PageTitle.Text = AppResources.CreateBoard;
                }

                if (e.NavigationMode == NavigationMode.New)
                {
                    NewlyCreatedBoardName = null;
                }
            }

            TxtBoardName.Focus();
            TxtBoardName.SelectionStart = TxtBoardName.Text.Length;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            if (e.NavigationMode == NavigationMode.Back)
            {
                BoardToEdit = null;
            }
        }

        private void UpdateAppBarLocalization()
        {
            if (ApplicationBar == null || ApplicationBar.Buttons.Count < 2) return;

            var saveBtn = ApplicationBar.Buttons[0] as ApplicationBarIconButton;
            var cancelBtn = ApplicationBar.Buttons[1] as ApplicationBarIconButton;

            if (saveBtn != null)
            {
                saveBtn.Text = AppResources.Save;
            }

            if (cancelBtn != null)
            {
                cancelBtn.Text = AppResources.Cancel;
            }
        }

        private async void OnSaveAppBarClick(object sender, EventArgs e)
        {
            this.Focus();

            string boardName = TxtBoardName.Text != null ? TxtBoardName.Text.Trim() : string.Empty;
            bool isPrivate = (ChkIsPrivate.IsChecked == true);

            System.Diagnostics.Debug.WriteLine(string.Format(">>> NEW BOARD SUBMIT: Name='{0}', IsPrivate={1}", boardName, isPrivate));

            if (string.IsNullOrEmpty(boardName))
            {
                MessageBox.Show(AppResources.EnterBoardName, AppResources.Warning, MessageBoxButton.OK);
                return;
            }

            SetLoadingState(true);

            var apiService = new PinryApiService();
            bool success = false;

            if (BoardToEdit != null)
            {
                success = await apiService.UpdateBoardAsync(
                    boardId: BoardToEdit.Id,
                    name: boardName,
                    description: string.Empty,
                    isPrivate: isPrivate);

                if (success)
                {
                    BoardToEdit.Name = boardName;
                    BoardToEdit.IsPrivate = isPrivate;
                }
            }
            else
            {
                success = await apiService.CreateBoardAsync(
                    name: boardName,
                    description: string.Empty,
                    isPrivate: isPrivate);

                if (success)
                {
                    NewlyCreatedBoardName = boardName;
                }
            }

            SetLoadingState(false);

            if (success)
            {
                BoardToEdit = null;
                App.ViewModel.IsDataLoaded = false;
                await App.ViewModel.LoadDataAsync();
                NavigationService.GoBack();
            }
            else
            {
                string errorMsg = BoardToEdit != null
                    ? AppResources.BoardUpdateError
                    : AppResources.BoardCreateError;

                MessageBox.Show(errorMsg, AppResources.Error, MessageBoxButton.OK);
            }
        }

        private void OnCancelAppBarClick(object sender, EventArgs e)
        {
            BoardToEdit = null;
            NavigationService.GoBack();
        }

        private void SetLoadingState(bool isLoading)
        {
            var indicator = SystemTray.ProgressIndicator;
            if (indicator != null)
            {
                indicator.IsVisible = isLoading;
                indicator.Text = isLoading ? AppResources.Saving : AppResources.AppTitle;
            }

            if (ApplicationBar != null)
            {
                foreach (var button in ApplicationBar.Buttons.OfType<ApplicationBarIconButton>())
                {
                    button.IsEnabled = !isLoading;
                }
            }

            TxtBoardName.IsEnabled = !isLoading;
            ChkIsPrivate.IsEnabled = !isLoading;
        }
    }
}