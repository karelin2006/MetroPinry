using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Xna.Framework.Media;
using MetroPinry.Models;
using MetroPinry.Services;

namespace MetroPinry.Views
{
    public partial class PinDetailPage : PhoneApplicationPage
    {
        public static Pin SelectedPin { get; set; }

        public PinDetailPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (SelectedPin != null)
            {
                BindPinData();
            }
            else
            {
                if (NavigationService.CanGoBack)
                {
                    NavigationService.GoBack();
                }
            }
        }

        private async void BindPinData()
        {
            PinDescription.Text = !string.IsNullOrEmpty(SelectedPin.Description)
                ? SelectedPin.Description
                : "без описания";

            TxtAuthor.Text = SelectedPin.Submitter != null && !string.IsNullOrEmpty(SelectedPin.Submitter.Username)
                ? SelectedPin.Submitter.Username
                : "—";

            if (SelectedPin.Tags != null && SelectedPin.Tags.Count > 0)
            {
                TxtTags.Text = string.Join(", ", SelectedPin.Tags.Select(t => "#" + t.Trim().TrimStart('#')));
            }
            else
            {
                TxtTags.Text = "—";
            }

            if (SelectedPin.IsPrivate)
            {
                TxtVisibilityIcon.Text = "\uE1F6"; // Замочек
                TxtVisibility.Text = "приватный";
            }
            else
            {
                TxtVisibilityIcon.Text = "\uE12B"; // Глобус
                TxtVisibility.Text = "публичный";
            }

            UpdateBoardsText();

            if (FullImage.Source == null)
            {
                await LoadPinImageAsync();
            }
        }

        private void UpdateBoardsText()
        {
            if (SelectedPin == null || App.ViewModel.Boards == null)
            {
                TxtBoards.Text = "—";
                return;
            }

            var attached = App.ViewModel.Boards
                .Where(b => SelectedPin.BelongsToBoard(b.Id, b.Name))
                .Select(b => b.Name)
                .ToList();

            TxtBoards.Text = attached.Count > 0 ? string.Join(", ", attached) : "—";
        }

        // ==========================================
        // МЕНЮ: ПРИКРЕПИТЬ И ОТКРЕПИТЬ
        // ==========================================

        private void OnAttachBoardMenuClick(object sender, EventArgs e)
        {
            if (App.ViewModel.Boards == null || App.ViewModel.Boards.Count == 0)
            {
                MessageBox.Show("У вас пока нет ни одной доски.", "Информация", MessageBoxButton.OK);
                return;
            }

            var unattached = App.ViewModel.Boards
                .Where(b => !SelectedPin.BelongsToBoard(b.Id, b.Name))
                .ToList();

            if (unattached.Count == 0)
            {
                MessageBox.Show("Этот пин уже прикреплен ко всем вашим доскам.", "Информация", MessageBoxButton.OK);
                return;
            }

            SelectBoardPage.TargetPin = SelectedPin;
            SelectBoardPage.IsAttachMode = true;
            NavigationService.Navigate(new Uri("/Views/SelectBoardPage.xaml", UriKind.Relative));
        }

        private async void OnDetachBoardMenuClick(object sender, EventArgs e)
        {
            if (App.ViewModel.Boards == null || App.ViewModel.Boards.Count == 0)
            {
                MessageBox.Show("У вас пока нет досок.", "Информация", MessageBoxButton.OK);
                return;
            }

            // Контекст: если открыто прямо из доски, сразу открепляем от неё
            var currentBoard = BoardDetailsPage.SelectedBoard;
            if (currentBoard != null && SelectedPin.BelongsToBoard(currentBoard.Id, currentBoard.Name))
            {
                var confirm = MessageBox.Show(
                    string.Format("Открепить этот пин от доски \"{0}\"?", currentBoard.Name),
                    "Открепление",
                    MessageBoxButton.OKCancel);

                if (confirm == MessageBoxResult.OK)
                {
                    SetLoadingState(true, "открепление...");
                    var api = new PinryApiService();
                    bool success = await api.RemovePinFromBoardAsync(currentBoard.Id, SelectedPin.Id);
                    SetLoadingState(false);

                    if (success)
                    {
                        if (SelectedPin.Boards != null)
                        {
                            SelectedPin.Boards.Remove(currentBoard.Id);
                            SelectedPin.Boards.Remove(currentBoard.Id.ToString());
                            SelectedPin.Boards.RemoveAll(item => item != null && item.ToString().Contains("\"id\":" + currentBoard.Id));
                        }
                        UpdateBoardsText();
                        await App.ViewModel.LoadDataAsync();
                    }
                    else
                    {
                        MessageBox.Show("Не удалось открепить пин от доски.", "Ошибка", MessageBoxButton.OK);
                    }
                }
                return;
            }

            // Если открыто из ленты
            var attached = App.ViewModel.Boards
                .Where(b => SelectedPin.BelongsToBoard(b.Id, b.Name))
                .ToList();

            if (attached.Count == 0)
            {
                MessageBox.Show("Этот пин не прикреплен ни к одной доске.", "Информация", MessageBoxButton.OK);
                return;
            }

            // Открываем экран выбора для открепления
            SelectBoardPage.TargetPin = SelectedPin;
            SelectBoardPage.IsAttachMode = false;
            NavigationService.Navigate(new Uri("/Views/SelectBoardPage.xaml", UriKind.Relative));
        }

        // ==========================================
        // БАЗОВЫЕ ДЕЙСТВИЯ (IMAGE, EDIT, DOWNLOAD, DELETE)
        // ==========================================

        private async Task LoadPinImageAsync()
        {
            if (SelectedPin == null) return;

            Uri imageUri = SelectedPin.DisplayOriginalUrl ?? SelectedPin.DisplayStandardUrl ?? SelectedPin.DisplayThumbnailUrl;
            if (imageUri == null) return;

            SetLoadingState(true, "loading image...");

            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "MetroPinry/1.0 (Windows Phone 8.1)");

                    if (!string.IsNullOrEmpty(StorageService.AuthToken))
                    {
                        client.DefaultRequestHeaders.Authorization =
                            new AuthenticationHeaderValue("Token", StorageService.AuthToken);
                    }

                    var bytes = await client.GetByteArrayAsync(imageUri);
                    using (var stream = new MemoryStream(bytes))
                    {
                        var bmp = new BitmapImage();
                        bmp.SetSource(stream);
                        FullImage.Source = bmp;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("IMAGE LOAD ERROR: " + ex.Message);
                FullImage.Source = new BitmapImage(imageUri);
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        private void SetLoadingState(bool isLoading, string statusText = "MetroPinry")
        {
            if (AppProgressIndicator != null)
            {
                AppProgressIndicator.Text = statusText;
                AppProgressIndicator.IsVisible = isLoading;
            }

            if (ApplicationBar != null)
            {
                foreach (var button in ApplicationBar.Buttons.OfType<ApplicationBarIconButton>())
                {
                    button.IsEnabled = !isLoading;
                }

                foreach (var menuItem in ApplicationBar.MenuItems.OfType<ApplicationBarMenuItem>())
                {
                    menuItem.IsEnabled = !isLoading;
                }
            }
        }

        private void OnEditAppBarClick(object sender, EventArgs e)
        {
            if (SelectedPin == null) return;

            NewPinPage.PinToEdit = SelectedPin;
            NavigationService.Navigate(new Uri("/Views/NewPinPage.xaml", UriKind.Relative));
        }

        private async void OnDownloadAppBarClick(object sender, EventArgs e)
        {
            if (SelectedPin == null) return;

            Uri imageUri = SelectedPin.DisplayOriginalUrl ?? SelectedPin.DisplayStandardUrl;
            if (imageUri == null)
            {
                MessageBox.Show("Ссылка на изображение недоступна.", "Ошибка", MessageBoxButton.OK);
                return;
            }

            SetLoadingState(true, "скачивание...");

            try
            {
                using (var client = new HttpClient())
                {
                    if (!string.IsNullOrEmpty(StorageService.AuthToken))
                    {
                        client.DefaultRequestHeaders.Authorization =
                            new AuthenticationHeaderValue("Token", StorageService.AuthToken);
                    }

                    var bytes = await client.GetByteArrayAsync(imageUri);
                    using (var stream = new MemoryStream(bytes))
                    {
                        var library = new MediaLibrary();
                        string fileName = string.Format("Pin_{0}_{1}.jpg", SelectedPin.Id, DateTime.Now.Ticks);
                        library.SavePicture(fileName, stream);

                        MessageBox.Show("Изображение сохранено в галерею (Saved Pictures).", "Готово", MessageBoxButton.OK);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось сохранить изображение: " + ex.Message, "Ошибка", MessageBoxButton.OK);
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        private async void OnDeleteAppBarClick(object sender, EventArgs e)
        {
            if (SelectedPin == null) return;

            var result = MessageBox.Show(
                "Вы действительно хотите удалить этот пин? Это действие нельзя отменить.",
                "Удаление пина",
                MessageBoxButton.OKCancel);

            if (result != MessageBoxResult.OK)
            {
                return;
            }

            SetLoadingState(true, "удаление пина...");

            var api = new PinryApiService();
            bool success = await api.DeletePinAsync(SelectedPin.Id);

            SetLoadingState(false);

            if (success)
            {
                if (App.ViewModel.RecentPins != null && App.ViewModel.RecentPins.Contains(SelectedPin))
                {
                    App.ViewModel.RecentPins.Remove(SelectedPin);
                }
                if (App.ViewModel.UserPins != null && App.ViewModel.UserPins.Contains(SelectedPin))
                {
                    App.ViewModel.UserPins.Remove(SelectedPin);
                }

                SelectedPin = null;

                if (NavigationService.CanGoBack)
                {
                    NavigationService.GoBack();
                }
            }
            else
            {
                MessageBox.Show("Не удалось удалить пин. Проверьте права доступа и сеть.", "Ошибка", MessageBoxButton.OK);
            }
        }
    }
}