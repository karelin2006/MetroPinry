using System;
using System.Collections.Generic;
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
    public partial class SelectBoardPage : PhoneApplicationPage
    {
        public static Pin TargetPin { get; set; }
        public static bool IsAttachMode { get; set; }
        public static bool IsRepinMode { get; set; }

        public SelectBoardPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (TargetPin == null || App.ViewModel.Boards == null)
            {
                GoBackSafely();
                return;
            }

            var myBoards = App.ViewModel.Boards.Where(b => b.IsMine).ToList();

            if (IsRepinMode)
            {
                PageHeader.Text = AppResources.SavePinTo;
                BoardsList.ItemsSource = myBoards;
                return;
            }

            PageHeader.Text = IsAttachMode ? AppResources.AttachTo : AppResources.DetachFrom;

            if (TargetPin.Boards == null)
            {
                TargetPin.Boards = new List<object>();
            }

            // Если пин открыт из общей ленты и не знает своих досок — синхронизируем их
            if (TargetPin.Boards.Count == 0 && myBoards.Count > 0)
            {
                SetLoading(true, AppResources.AppTitle);
                try
                {
                    var api = new PinryApiService();
                    foreach (var board in myBoards)
                    {
                        var resp = await api.GetBoardPinsAsync(board.Id, board.Name);
                        if (resp != null && resp.Results != null && resp.Results.Any(p => p.Id == TargetPin.Id))
                        {
                            if (!TargetPin.BelongsToBoard(board.Id, board.Name))
                            {
                                TargetPin.Boards.Add(board.Id);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("SYNC PIN BOARDS ERROR: " + ex.Message);
                }
                finally
                {
                    SetLoading(false);
                }
            }

            if (IsAttachMode)
            {
                BoardsList.ItemsSource = myBoards.Where(b => !TargetPin.BelongsToBoard(b.Id, b.Name)).ToList();
            }
            else
            {
                BoardsList.ItemsSource = myBoards.Where(b => TargetPin.BelongsToBoard(b.Id, b.Name)).ToList();
            }
        }

        private async void OnBoardSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedBoard = BoardsList.SelectedItem as Board;
            BoardsList.SelectedItem = null;

            if (selectedBoard == null || TargetPin == null) return;

            string progressText = IsRepinMode
                ? AppResources.Saving
                : (IsAttachMode ? AppResources.Attaching : AppResources.Detaching);

            SetLoading(true, progressText);
            var api = new PinryApiService();

            try
            {
                if (IsRepinMode)
                {
                    Uri imageUri = TargetPin.DisplayOriginalUrl ?? TargetPin.DisplayStandardUrl ?? TargetPin.DisplayThumbnailUrl;
                    if (imageUri == null)
                    {
                        MessageBox.Show(AppResources.DirectImageLinkError, AppResources.Error, MessageBoxButton.OK);
                        return;
                    }

                    string imageUrl = imageUri.AbsoluteUri;
                    if (!imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                        !imageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        string baseServer = (StorageService.ServerUrl ?? string.Empty).TrimEnd('/');
                        imageUrl = baseServer + "/" + imageUrl.TrimStart('/');
                    }

                    bool pinPrivacy = selectedBoard.IsPrivate;
                    string desc = TargetPin.Description ?? string.Empty;
                    var tagsList = TargetPin.Tags != null ? TargetPin.Tags.ToList() : new List<string>();

                    bool success = await api.CreatePinAsync(
                            url: imageUrl,
                            imageId: null,
                            desc: desc,
                            boardsList: null,
                            tagsList: tagsList,
                            isPrivate: pinPrivacy,
                            targetBoardId: selectedBoard.Id);

                    if (success)
                    {
                        await RefreshAllAppDataAsync();
                        string privacyNotice = pinPrivacy ? AppResources.PrivateBoardNotice : string.Empty;
                        MessageBox.Show(string.Format(AppResources.PinSavedSuccessFormat, selectedBoard.Name, privacyNotice), AppResources.Saved, MessageBoxButton.OK);
                        GoBackSafely();
                    }
                    else
                    {
                        MessageBox.Show(AppResources.RepinServerError, AppResources.Error, MessageBoxButton.OK);
                    }
                }
                else if (IsAttachMode)
                {
                    if (selectedBoard.IsPrivate && !TargetPin.IsPrivate)
                    {
                        var tagsList = TargetPin.Tags != null ? TargetPin.Tags.ToList() : new List<string>();
                        bool updateSuccess = await api.UpdatePinAsync(
                            pinId: TargetPin.Id,
                            desc: TargetPin.Description,
                            tags: tagsList,
                            isPrivate: true);

                        if (updateSuccess)
                        {
                            TargetPin.IsPrivate = true;
                        }
                    }

                    bool success = await api.AddPinToBoardAsync(selectedBoard.Id, TargetPin.Id);
                    if (success)
                    {
                        if (TargetPin.Boards == null)
                        {
                            TargetPin.Boards = new List<object>();
                        }
                        if (!TargetPin.BelongsToBoard(selectedBoard.Id, selectedBoard.Name))
                        {
                            TargetPin.Boards.Add(selectedBoard.Id);
                        }

                        selectedBoard.PinsCount++;
                        await RefreshAllAppDataAsync();
                        GoBackSafely();
                    }
                    else
                    {
                        MessageBox.Show(AppResources.AttachPinError, AppResources.Error, MessageBoxButton.OK);
                    }
                }
                else
                {
                    bool success = await api.RemovePinFromBoardAsync(selectedBoard.Id, TargetPin.Id);
                    if (success)
                    {
                        if (TargetPin.Boards != null)
                        {
                            TargetPin.Boards.Remove(selectedBoard.Id);
                            TargetPin.Boards.Remove(selectedBoard.Id.ToString());
                            TargetPin.Boards.RemoveAll(item => item != null && item.ToString().Contains("\"id\":" + selectedBoard.Id));
                        }

                        if (selectedBoard.PinsCount > 0)
                        {
                            selectedBoard.PinsCount--;
                        }

                        await RefreshAllAppDataAsync();
                        GoBackSafely();
                    }
                    else
                    {
                        MessageBox.Show(AppResources.DetachPinError, AppResources.Error, MessageBoxButton.OK);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(AppResources.OperationErrorFormat, ex.Message), AppResources.Error, MessageBoxButton.OK);
            }
            finally
            {
                SetLoading(false);
            }
        }

        // Принудительная перезагрузка всех коллекций ViewModel и актуализация текущих ссылок
        private async System.Threading.Tasks.Task RefreshAllAppDataAsync()
        {
            var currentBoardsBackup = TargetPin != null && TargetPin.Boards != null
                ? new List<object>(TargetPin.Boards)
                : null;

            App.ViewModel.IsDataLoaded = false;
            await App.ViewModel.LoadDataAsync();

            // Восстанавливаем знание пина о своих досках после перезагрузки
            if (TargetPin != null && currentBoardsBackup != null)
            {
                TargetPin.Boards = currentBoardsBackup;
            }

            // Если открыта конкретная доска — обновляем на неё ссылку из свежего списка
            if (BoardDetailsPage.SelectedBoard != null && App.ViewModel.Boards != null)
            {
                var updatedBoard = App.ViewModel.Boards.FirstOrDefault(b => b.Id == BoardDetailsPage.SelectedBoard.Id);
                if (updatedBoard != null)
                {
                    BoardDetailsPage.SelectedBoard = updatedBoard;
                }
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

        private void GoBackSafely()
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
}