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
    public partial class SelectBoardPage : PhoneApplicationPage
    {
        public static Pin TargetPin { get; set; }
        public static bool IsAttachMode { get; set; }
        public static bool IsRepinMode { get; set; }

        public SelectBoardPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (TargetPin == null || App.ViewModel.Boards == null)
            {
                if (NavigationService.CanGoBack)
                {
                    NavigationService.GoBack();
                }
                return;
            }

            // 1. Выбираем ТОЛЬКО СВОИ доски
            var myBoards = App.ViewModel.Boards
                .Where(b => b.IsMine)
                .ToList();

            // 2. Применяем фильтрацию в зависимости от сценария
            if (IsRepinMode)
            {
                PageHeader.Text = "save pin to";
                BoardsList.ItemsSource = myBoards;
            }
            else if (IsAttachMode)
            {
                PageHeader.Text = "attach to";
                // Свои доски, к которым этот пин еще не прикреплен
                BoardsList.ItemsSource = myBoards
                    .Where(b => !TargetPin.BelongsToBoard(b.Id, b.Name))
                    .ToList();
            }
            else
            {
                PageHeader.Text = "detach from";
                // Свои доски, на которых этот пин уже находится
                BoardsList.ItemsSource = myBoards
                    .Where(b => TargetPin.BelongsToBoard(b.Id, b.Name))
                    .ToList();
            }
        }

        private async void OnBoardSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedBoard = BoardsList.SelectedItem as Board;
            BoardsList.SelectedItem = null;

            if (selectedBoard == null || TargetPin == null) return;

            SetLoading(true, IsRepinMode ? "saving pin..." : (IsAttachMode ? "attaching..." : "detaching..."));

            var api = new PinryApiService();

            try
            {
                if (IsRepinMode)
                {
                    // 1. РЕЖИМ РЕПИНА (сохранение чужого пина к себе)
                    Uri imageUri = TargetPin.DisplayOriginalUrl
                        ?? TargetPin.DisplayStandardUrl
                        ?? TargetPin.DisplayThumbnailUrl;

                    if (imageUri == null)
                    {
                        MessageBox.Show("Не удалось получить прямую ссылку на изображение пина.", "Ошибка", MessageBoxButton.OK);
                        return;
                    }

                    string imageUrl = imageUri.AbsoluteUri;

                    // Если ссылка относительная (/media/...) — превращаем её в абсолютную
                    if (!imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                        !imageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        string baseServer = (StorageService.ServerUrl ?? string.Empty).TrimEnd('/');
                        imageUrl = baseServer + "/" + imageUrl.TrimStart('/');
                    }

                    // Автоматическое наследование приватности выбранной доски
                    bool pinPrivacy = selectedBoard.IsPrivate;

                    bool success = await api.CreatePinFromUrlAsync(
                        url: imageUrl,
                        desc: TargetPin.Description ?? string.Empty,
                        boardsList: null,
                        tagsList: TargetPin.Tags,
                        isPrivate: pinPrivacy,
                        targetBoardId: selectedBoard.Id);

                    if (success)
                    {
                        App.ViewModel.IsDataLoaded = false;
                        await App.ViewModel.LoadDataAsync();

                        string privacyNotice = pinPrivacy ? " (private board)" : "";
                        MessageBox.Show(
                            string.Format("Pin saved to \"{0}\"{1}!", selectedBoard.Name, privacyNotice),
                            "Saved",
                            MessageBoxButton.OK);

                        if (NavigationService.CanGoBack)
                        {
                            NavigationService.GoBack();
                        }
                    }
                    else
                    {
                        MessageBox.Show("Failed to save pin on server. Проверьте Debug Output.", "Error", MessageBoxButton.OK);
                    }
                }
                else if (IsAttachMode)
                {
                    // 2. ПРИКРЕПЛЕНИЕ СВОЕГО ПИНА К ДОСКЕ
                    bool success = await api.AddPinToBoardAsync(selectedBoard.Id, TargetPin.Id);
                    if (success)
                    {
                        if (TargetPin.Boards != null)
                        {
                            TargetPin.Boards.Add(selectedBoard.Id);
                        }
                        await App.ViewModel.LoadDataAsync();

                        if (NavigationService.CanGoBack)
                        {
                            NavigationService.GoBack();
                        }
                    }
                    else
                    {
                        MessageBox.Show("Failed to attach pin.", "Error", MessageBoxButton.OK);
                    }
                }
                else
                {
                    // 3. ОТКРЕПЛЕНИЕ СВОЕГО ПИНА ОТ ДОСКИ
                    bool success = await api.RemovePinFromBoardAsync(selectedBoard.Id, TargetPin.Id);
                    if (success)
                    {
                        if (TargetPin.Boards != null)
                        {
                            TargetPin.Boards.Remove(selectedBoard.Id);
                            TargetPin.Boards.Remove(selectedBoard.Id.ToString());
                            TargetPin.Boards.RemoveAll(item => item != null && item.ToString().Contains("\"id\":" + selectedBoard.Id));
                        }
                        await App.ViewModel.LoadDataAsync();

                        if (NavigationService.CanGoBack)
                        {
                            NavigationService.GoBack();
                        }
                    }
                    else
                    {
                        MessageBox.Show("Failed to detach pin.", "Error", MessageBoxButton.OK);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Operation error: " + ex.Message, "Error", MessageBoxButton.OK);
            }
            finally
            {
                SetLoading(false);
            }
        }

        private void SetLoading(bool isLoading, string text = "")
        {
            if (PageProgress != null)
            {
                PageProgress.IsVisible = isLoading;
                PageProgress.Text = text;
            }
            BoardsList.IsEnabled = !isLoading;
        }
    }
}