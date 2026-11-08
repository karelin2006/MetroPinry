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
using MetroPinry.Resources;
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
                // Если зашли из конкретной доски — гарантируем, что пин знает о ней
                if (SourceBoard != null)
                {
                    if (SelectedPin.Boards == null)
                    {
                        SelectedPin.Boards = new List<object>();
                    }
                    if (!SelectedPin.BelongsToBoard(SourceBoard.Id, SourceBoard.Name))
                    {
                        SelectedPin.Boards.Add(SourceBoard.Id);
                    }
                }

                BindPinData();
                UpdateActionButtons();
            }
            else
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (NavigationService.CanGoBack)
                    {
                        NavigationService.GoBack();
                    }
                });
            }
        }

        private async void BindPinData()
        {
            PinDescription.Text = !string.IsNullOrEmpty(SelectedPin.Description)
                ? SelectedPin.Description
                : AppResources.NoDescription;

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
                TxtVisibilityIcon.Text = "\uE1F6";
                TxtVisibility.Text = AppResources.Private;
            }
            else
            {
                TxtVisibilityIcon.Text = "\uE12B";
                TxtVisibility.Text = AppResources.Public;
            }

            if (FullImage.Source == null)
            {
                await LoadPinImageAsync();
            }
        }

        private async Task LoadPinImageAsync()
        {
            if (SelectedPin == null) return;

            Uri imageUri = SelectedPin.DisplayOriginalUrl ?? SelectedPin.DisplayStandardUrl ?? SelectedPin.DisplayThumbnailUrl;
            if (imageUri == null) return;

            SetLoadingState(true, AppResources.LoadingImage);

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

        private void SetLoadingState(bool isLoading, string statusText = null)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (AppProgressIndicator != null)
                {
                    AppProgressIndicator.Text = !string.IsNullOrEmpty(statusText) ? statusText : AppResources.AppTitle;
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
                MessageBox.Show(AppResources.GuestRestrictedAction, AppResources.AppTitle, MessageBoxButton.OK);
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
                MessageBox.Show("Image link is unavailable.", AppResources.AppTitle, MessageBoxButton.OK);
                return;
            }

            SetLoadingState(true, AppResources.Downloading);

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
                        MessageBox.Show(AppResources.PictureSaved, AppResources.AppTitle, MessageBoxButton.OK);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, AppResources.AppTitle, MessageBoxButton.OK);
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
                AppResources.DeletePinConfirm,
                AppResources.Delete,
                MessageBoxButton.OKCancel);

            if (result != MessageBoxResult.OK)
            {
                return;
            }

            SetLoadingState(true, AppResources.DeletingPin);

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
                MessageBox.Show("Error deleting pin.", AppResources.AppTitle, MessageBoxButton.OK);
            }
        }

        private void OnAttachBoardMenuClick(object sender, EventArgs e)
        {
            if (App.ViewModel.Boards == null || App.ViewModel.Boards.Count == 0)
            {
                MessageBox.Show("No boards available.", AppResources.AppTitle, MessageBoxButton.OK);
                return;
            }
            SelectBoardPage.TargetPin = SelectedPin;
            SelectBoardPage.IsAttachMode = true;
            SelectBoardPage.IsRepinMode = false;
            NavigationService.Navigate(new Uri("/Views/SelectBoardPage.xaml", UriKind.Relative));
        }

        private async void OnDetachBoardMenuClick(object sender, EventArgs e)
        {
            if (App.ViewModel.Boards == null || App.ViewModel.Boards.Count == 0)
            {
                MessageBox.Show("No boards available.", AppResources.AppTitle, MessageBoxButton.OK);
                return;
            }

            // Если зашли в пин из конкретной доски — сразу открепляем от неё и обновляем данные
            if (SourceBoard != null)
            {
                SetLoadingState(true, AppResources.Detaching);
                try
                {
                    var api = new PinryApiService();
                    bool success = await api.RemovePinFromBoardAsync(SourceBoard.Id, SelectedPin.Id);
                    if (success)
                    {
                        if (SelectedPin.Boards != null)
                        {
                            SelectedPin.Boards.Remove(SourceBoard.Id);
                            SelectedPin.Boards.Remove(SourceBoard.Id.ToString());
                            SelectedPin.Boards.RemoveAll(item => item != null && item.ToString().Contains("\"id\":" + SourceBoard.Id));
                        }

                        if (SourceBoard.PinsCount > 0)
                        {
                            SourceBoard.PinsCount--;
                        }

                        // Принудительно обновляем общие данные приложения
                        App.ViewModel.IsDataLoaded = false;
                        await App.ViewModel.LoadDataAsync();

                        if (BoardDetailsPage.SelectedBoard != null && App.ViewModel.Boards != null)
                        {
                            var updated = App.ViewModel.Boards.FirstOrDefault(b => b.Id == BoardDetailsPage.SelectedBoard.Id);
                            if (updated != null)
                            {
                                BoardDetailsPage.SelectedBoard = updated;
                            }
                        }

                        // Сразу возвращаемся назад на доску, где OnNavigatedTo обновит список пинов
                        if (NavigationService.CanGoBack)
                        {
                            NavigationService.GoBack();
                        }
                    }
                    else
                    {
                        MessageBox.Show(AppResources.DetachPinError, AppResources.Error, MessageBoxButton.OK);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(AppResources.OperationErrorFormat, ex.Message), AppResources.Error, MessageBoxButton.OK);
                }
                finally
                {
                    SetLoadingState(false);
                }
                return;
            }

            // Если зашли из общей ленты — открываем страницу выбора доски
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
                        actionBtn.Text = AppResources.Edit;
                        actionBtn.IsEnabled = true;
                    }
                    else if (!isGuest)
                    {
                        actionBtn.IconUri = new Uri("/Assets/AppBar/heart.png", UriKind.Relative);
                        actionBtn.Text = AppResources.SavePin;
                        actionBtn.IsEnabled = true;
                    }
                    else
                    {
                        actionBtn.IsEnabled = false;
                    }
                }

                if (downloadBtn != null)
                {
                    downloadBtn.Text = AppResources.Download;
                    downloadBtn.IsEnabled = true;
                }

                if (deleteBtn != null)
                {
                    deleteBtn.Text = AppResources.Delete;
                    deleteBtn.IsEnabled = isOwner;
                }
            }

            if (ApplicationBar.MenuItems.Count >= 2)
            {
                var attachMenu = ApplicationBar.MenuItems[0] as ApplicationBarMenuItem;
                var detachMenu = ApplicationBar.MenuItems[1] as ApplicationBarMenuItem;

                if (attachMenu != null)
                {
                    attachMenu.Text = AppResources.AttachToBoard;
                    attachMenu.IsEnabled = isOwner;
                }

                if (detachMenu != null)
                {
                    detachMenu.Text = AppResources.DetachFromBoard;
                    detachMenu.IsEnabled = isOwner;
                }
            }
        }
    }
}