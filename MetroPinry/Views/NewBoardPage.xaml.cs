using System;
using System.Linq;
using System.Windows;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using MetroPinry.Models;
using MetroPinry.Services;

namespace MetroPinry.Views
{
    public partial class NewBoardPage : PhoneApplicationPage
    {
        // Название новой доски для автовыбора при создании пина
        public static string NewlyCreatedBoardName { get; set; }

        // Доска, переданная для редактирования
        public static Board BoardToEdit { get; set; }

        public NewBoardPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (BoardToEdit != null)
            {
                // Если в XAML есть TextBlock с x:Name="PageTitle", меняем заголовок
                var titleBlock = FindName("PageTitle") as System.Windows.Controls.TextBlock;
                if (titleBlock != null)
                {
                    titleBlock.Text = "edit board";
                }

                TxtBoardName.Text = BoardToEdit.Name ?? string.Empty;
                ChkIsPrivate.IsChecked = BoardToEdit.IsPrivate;
            }
            else
            {
                var titleBlock = FindName("PageTitle") as System.Windows.Controls.TextBlock;
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
            string boardName = TxtBoardName.Text != null ? TxtBoardName.Text.Trim() : string.Empty;
            bool isPrivate = ChkIsPrivate.IsChecked ?? false;

            if (string.IsNullOrEmpty(boardName))
            {
                MessageBox.Show("Введите название доски.", "Предупреждение", MessageBoxButton.OK);
                return;
            }

            SetLoadingState(true);

            var apiService = new PinryApiService();
            bool success = false;

            // 1. Редактирование существующей доски
            if (BoardToEdit != null)
            {
                success = await apiService.UpdateBoardAsync(BoardToEdit.Id, boardName, isPrivate);

                if (success)
                {
                    // Мгновенное обновление полей в памяти
                    BoardToEdit.Name = boardName;
                    BoardToEdit.IsPrivate = isPrivate;
                }
            }
            // 2. Создание новой доски
            else
            {
                success = await apiService.CreateBoardAsync(boardName, isPrivate);

                if (success)
                {
                    NewlyCreatedBoardName = boardName;
                }
            }

            SetLoadingState(false);

            if (success)
            {
                BoardToEdit = null;
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
            var progress = Resources.Contains("ProgressIndicator") ? (ProgressIndicator)Resources["ProgressIndicator"] : null;
            if (progress != null)
            {
                SystemTray.SetProgressIndicator(this, progress);
                progress.IsVisible = isLoading;
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