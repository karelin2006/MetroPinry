using System;
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
        public static Board SourceBoard { get; set; }

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
                UpdateActionButtons();
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

            if (FullImage.Source == null)
            {
                await LoadPinImageAsync();
            }
        }

        // ==========================================
        // БАЗОВЫЕ ДЕЙСТВИЯ (IMAGE, EDIT/REPIN, DOWNLOAD, DELETE)
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
            Dispatcher.BeginInvoke(() =>
            {
                if (AppProgressIndicator != null)
                {
                    AppProgressIndicator.Text = statusText;
                    AppProgressIndicator.IsVisible = isLoading;
                }

                if (ApplicationBar != null)
                {
                    if (isLoading)
                    {
                        foreach (var button in ApplicationBar.Buttons.OfType<ApplicationBarIconButton>())
                        {
                            button.IsEnabled = false;
                        }
                        foreach (var menuItem in ApplicationBar.MenuItems.OfType<ApplicationBarMenuItem>())
                        {
                            menuItem.IsEnabled = false;
                        }
                    }
                    else
                    {
                        UpdateActionButtons();
                    }
                }
            });
        }

        private void OnPrimaryActionAppBarClick(object sender, EventArgs e)
        {
            if (SelectedPin == null) return;

            var settings = System.IO.IsolatedStorage.IsolatedStorageSettings.ApplicationSettings;
            bool isGuest = settings.Contains("IsGuest") && (bool)settings["IsGuest"];

            if (isGuest)
            {
                MessageBox.Show("Для этого действия необходимо войти в аккаунт.");
                return;
            }

            if (SelectedPin.IsMine)
            {
                NewPinPage.PinToEdit = SelectedPin;
                NavigationService.Navigate(new Uri("/Views/NewPinPage.xaml", UriKind.Relative));
            }
            else
            {
                SelectBoardPage.TargetPin = SelectedPin;
                SelectBoardPage.IsRepinMode = true;
                SelectBoardPage.IsAttachMode = false;
                NavigationService.Navigate(new Uri("/Views/SelectBoardPage.xaml", UriKind.Relative));
            }
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

            bool success = false;
            try
            {
                var api = new PinryApiService();
                success = await api.DeletePinAsync(SelectedPin.Id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DELETE PIN ERROR: " + ex.Message);
            }
            finally
            {
                SetLoadingState(false);
            }

            if (success)
            {
                var pinToRemove = SelectedPin;
                SelectedPin = null;

                Dispatcher.BeginInvoke(() =>
                {
                    if (App.ViewModel.RecentPins != null && App.ViewModel.RecentPins.Contains(pinToRemove))
                    {
                        App.ViewModel.RecentPins.Remove(pinToRemove);
                    }
                    if (App.ViewModel.UserPins != null && App.ViewModel.UserPins.Contains(pinToRemove))
                    {
                        App.ViewModel.UserPins.Remove(pinToRemove);
                    }

                    if (NavigationService.CanGoBack)
                    {
                        NavigationService.GoBack();
                    }
                });
            }
            else
            {
                MessageBox.Show("Не удалось удалить пин. Проверьте права доступа и сеть.", "Ошибка", MessageBoxButton.OK);
            }
        }

        private void OnAttachBoardMenuClick(object sender, EventArgs e)
        {
            if (App.ViewModel.Boards == null || App.ViewModel.Boards.Count == 0)
            {
                MessageBox.Show("У вас пока нет ни одной доски.", "Информация", MessageBoxButton.OK);
                return;
            }

            SelectBoardPage.TargetPin = SelectedPin;
            SelectBoardPage.IsAttachMode = true;
            SelectBoardPage.IsRepinMode = false;
            NavigationService.Navigate(new Uri("/Views/SelectBoardPage.xaml", UriKind.Relative));
        }

        private void OnDetachBoardMenuClick(object sender, EventArgs e)
        {
            if (App.ViewModel.Boards == null || App.ViewModel.Boards.Count == 0)
            {
                MessageBox.Show("У вас пока нет досок.", "Информация", MessageBoxButton.OK);
                return;
            }

            SelectBoardPage.TargetPin = SelectedPin;
            SelectBoardPage.IsAttachMode = false;
            SelectBoardPage.IsRepinMode = false;
            NavigationService.Navigate(new Uri("/Views/SelectBoardPage.xaml", UriKind.Relative));
        }

        private void UpdateActionButtons()
        {
            if (SelectedPin == null || ApplicationBar == null) return;

            var settings = System.IO.IsolatedStorage.IsolatedStorageSettings.ApplicationSettings;
            bool isGuest = settings.Contains("IsGuest") && (bool)settings["IsGuest"];
            bool isOwner = !isGuest && SelectedPin.IsMine;

            if (ApplicationBar.Buttons.Count >= 3)
            {
                var actionBtn = ApplicationBar.Buttons[0] as ApplicationBarIconButton;
                var downloadBtn = ApplicationBar.Buttons[1] as ApplicationBarIconButton;
                var deleteBtn = ApplicationBar.Buttons[2] as ApplicationBarIconButton;

                if (actionBtn != null)
                {
                    if (isOwner)
                    {
                        actionBtn.IconUri = new Uri("/Assets/AppBar/edit.png", UriKind.Relative);
                        actionBtn.Text = "edit";
                        actionBtn.IsEnabled = true;
                    }
                    else if (!isGuest)
                    {
                        actionBtn.IconUri = new Uri("/Assets/AppBar/heart.png", UriKind.Relative);
                        actionBtn.Text = "save pin";
                        actionBtn.IsEnabled = true;
                    }
                    else
                    {
                        actionBtn.IsEnabled = false;
                    }
                }

                if (downloadBtn != null)
                {
                    downloadBtn.IsEnabled = true;
                }

                if (deleteBtn != null)
                {
                    deleteBtn.IsEnabled = isOwner;
                }
            }

            foreach (var menuItem in ApplicationBar.MenuItems.OfType<ApplicationBarMenuItem>())
            {
                string txt = (menuItem.Text ?? string.Empty).ToLowerInvariant();
                if (txt.Contains("доск") || txt.Contains("board"))
                {
                    menuItem.IsEnabled = isOwner;
                }
            }
        }
    }
}