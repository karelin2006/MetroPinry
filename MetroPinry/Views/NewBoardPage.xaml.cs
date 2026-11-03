using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using MetroPinry.Models;
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

            var titleBlock = FindName("PageTitle") as TextBlock;

            if (BoardToEdit != null)
            {
                if (titleBlock != null)
                {
                    titleBlock.Text = "edit board";
                }

                TxtBoardName.Text = BoardToEdit.Name ?? string.Empty;
                ChkIsPrivate.IsChecked = BoardToEdit.IsPrivate;
            }
            else
            {
                if (titleBlock != null)
                {
                    titleBlock.Text = "create board";
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

        private async void OnSaveAppBarClick(object sender, EventArgs e)
        {
            // 1. Принудительно забираем фокус у клавиатуры/текстовых полей
            this.Focus();

            string boardName = TxtBoardName.Text != null ? TxtBoardName.Text.Trim() : string.Empty;

            // 2. Строгая проверка чекбокса
            bool isPrivate = (ChkIsPrivate.IsChecked == true);

            System.Diagnostics.Debug.WriteLine(string.Format(">>> NEW BOARD SUBMIT: Name='{0}', IsPrivate={1}", boardName, isPrivate));

            if (string.IsNullOrEmpty(boardName))
            {
                MessageBox.Show("Введите название доски.", "Предупреждение", MessageBoxButton.OK);
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
                    ? "Не удалось обновить доску. Проверьте права доступа и сеть."
                    : "Не удалось создать доску. Возможно, доска с таким именем уже существует или нет связи с сервером.";

                MessageBox.Show(errorMsg, "Ошибка", MessageBoxButton.OK);
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
                indicator.Text = isLoading ? "сохранение..." : "MetroPinry";
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