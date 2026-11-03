using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
    public partial class BoardDetailsPage : PhoneApplicationPage
    {
        public static Board SelectedBoard { get; set; }
        private readonly ObservableCollection<Pin> _boardPins = new ObservableCollection<Pin>();

        public BoardDetailsPage()
        {
            InitializeComponent();
            BoardPinsList.ItemsSource = _boardPins;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (SelectedBoard != null)
            {
                UpdateBoardHeader();
                ConfigureAppBar();

                // Загружаем данные только при первичном входе на страницу, не перезагружая при возврате назад
                if (e.NavigationMode != NavigationMode.Back)
                {
                    await LoadPinsAsync();
                }
            }
            else
            {
                if (NavigationService.CanGoBack)
                {
                    NavigationService.GoBack();
                }
            }
        }

        private async System.Threading.Tasks.Task LoadPinsAsync()
        {
            if (SelectedBoard == null) return;

            SetProgress(true);

            // 1. Виртуальная доска "all" (все пины пользователя)
            if (SelectedBoard.Id == -1)
            {
                _boardPins.Clear();
                if (App.ViewModel.UserPins != null)
                {
                    foreach (var pin in App.ViewModel.UserPins)
                    {
                        _boardPins.Add(pin);
                    }
                }
                SetProgress(false);
                return;
            }

            // 2. Реальная доска Pinry
            try
            {
                var api = new PinryApiService();
                var resp = await api.GetBoardPinsAsync(SelectedBoard.Id, SelectedBoard.Name);

                Dispatcher.BeginInvoke(() =>
                {
                    _boardPins.Clear();
                    if (resp != null && resp.Results != null)
                    {
                        Debug.WriteLine("ADDING PINS TO BOARD VIEW: " + resp.Results.Count);
                        foreach (var pin in resp.Results)
                        {
                            _boardPins.Add(pin);
                        }
                    }
                    SetProgress(false);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("LOAD BOARD PINS ERROR: " + ex.Message);
                Dispatcher.BeginInvoke(() => SetProgress(false));
            }
        }

        private void SetProgress(bool isVisible)
        {
            if (BoardProgress != null)
            {
                BoardProgress.IsVisible = isVisible;
            }
        }

        private void UpdateBoardHeader()
        {
            if (SelectedBoard == null) return;

            BoardTitle.Text = SelectedBoard.Name;

            // \uE1F6 — замок, \uE12B — глобус
            BoardVisibilityIcon.Text = SelectedBoard.IsPrivate ? "\uE1F6" : "\uE12B";
        }

        private void ConfigureAppBar()
        {
            if (ApplicationBar == null || SelectedBoard == null) return;

            bool isVirtual = (SelectedBoard.Id == -1);

            foreach (var button in ApplicationBar.Buttons.OfType<ApplicationBarIconButton>())
            {
                string txt = (button.Text ?? string.Empty).ToLowerInvariant();
                if (txt.Contains("edit") || txt.Contains("delete"))
                {
                    button.IsEnabled = !isVirtual && SelectedBoard.IsMine;
                }
            }

            foreach (var item in ApplicationBar.MenuItems.OfType<ApplicationBarMenuItem>())
            {
                string txt = (item.Text ?? string.Empty).ToLowerInvariant();
                if (txt.Contains("edit") || txt.Contains("delete"))
                {
                    item.IsEnabled = !isVirtual && SelectedBoard.IsMine;
                }
            }
        }

        private void OnPinSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selector = sender as LongListSelector;
            if (selector == null || selector.SelectedItem == null) return;

            var pin = selector.SelectedItem as Pin;
            selector.SelectedItem = null;

            PinDetailPage.SelectedPin = pin;
            NavigationService.Navigate(new Uri("/Views/PinDetailPage.xaml", UriKind.Relative));
        }

        private void OnAddPinToBoardAppBarClick(object sender, EventArgs e)
        {
            if (SelectedBoard == null) return;

            NewPinPage.PinToEdit = null;
            // Если доска "all", передаем null, чтобы пользователь выбрал конкретную доску вручную
            NewPinPage.PreselectedBoard = (SelectedBoard.Id == -1) ? null : SelectedBoard;

            NavigationService.Navigate(new Uri("/Views/NewPinPage.xaml", UriKind.Relative));
        }

        private void OnEditBoardAppBarClick(object sender, EventArgs e)
        {
            if (SelectedBoard == null || SelectedBoard.Id == -1) return;

            NewBoardPage.BoardToEdit = SelectedBoard;
            NavigationService.Navigate(new Uri("/Views/NewBoardPage.xaml", UriKind.Relative));
        }

        private void OnEditPinContextClick(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;

            var pin = menuItem.DataContext as Pin;
            if (pin == null) return;

            NewPinPage.PinToEdit = pin;
            NavigationService.Navigate(new Uri("/Views/NewPinPage.xaml", UriKind.Relative));
        }

        private async void OnDeletePinContextClick(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;

            var pin = menuItem.DataContext as Pin;
            if (pin == null) return;

            string title = string.IsNullOrEmpty(pin.Description) ? "этот пин" : "\"" + pin.Description + "\"";
            var result = MessageBox.Show(string.Format("Удалить {0}? Действие нельзя отменить.", title), "Удаление", MessageBoxButton.OKCancel);

            if (result == MessageBoxResult.OK)
            {
                var api = new PinryApiService();
                bool success = await api.DeletePinAsync(pin.Id);

                if (success)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        _boardPins.Remove(pin);

                        if (App.ViewModel.RecentPins != null && App.ViewModel.RecentPins.Contains(pin))
                        {
                            App.ViewModel.RecentPins.Remove(pin);
                        }
                        if (App.ViewModel.UserPins != null && App.ViewModel.UserPins.Contains(pin))
                        {
                            App.ViewModel.UserPins.Remove(pin);
                        }

                        if (SelectedBoard.PinsCount > 0)
                        {
                            SelectedBoard.PinsCount--;
                        }
                    });
                }
                else
                {
                    MessageBox.Show("Не удалось удалить пин. Проверьте права доступа.", "Ошибка", MessageBoxButton.OK);
                }
            }
        }

        private async void OnRefreshAppBarClick(object sender, EventArgs e)
        {
            await LoadPinsAsync();
        }

        private async void OnDeleteBoardAppBarClick(object sender, EventArgs e)
        {
            if (SelectedBoard == null || SelectedBoard.Id == -1) return;

            var result = MessageBox.Show(
                string.Format("Вы действительно хотите удалить доску \"{0}\"? Пины останутся в вашей ленте, но доска будет удалена.", SelectedBoard.Name),
                "Удаление доски",
                MessageBoxButton.OKCancel);

            if (result != MessageBoxResult.OK) return;

            SetProgress(true);

            var api = new PinryApiService();
            bool success = await api.DeleteBoardAsync(SelectedBoard.Id);

            SetProgress(false);

            if (success)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (App.ViewModel.Boards != null)
                    {
                        var boardInVm = App.ViewModel.Boards.FirstOrDefault(b => b.Id == SelectedBoard.Id);
                        if (boardInVm != null)
                        {
                            App.ViewModel.Boards.Remove(boardInVm);
                        }
                    }

                    if (App.ViewModel.UserBoardsWithAll != null)
                    {
                        var boardInUserList = App.ViewModel.UserBoardsWithAll.FirstOrDefault(b => b.Id == SelectedBoard.Id);
                        if (boardInUserList != null)
                        {
                            App.ViewModel.UserBoardsWithAll.Remove(boardInUserList);
                        }
                    }

                    SelectedBoard = null;

                    if (NavigationService.CanGoBack)
                    {
                        NavigationService.GoBack();
                    }
                });
            }
            else
            {
                MessageBox.Show("Не удалось удалить доску. Проверьте права доступа и сеть.", "Ошибка", MessageBoxButton.OK);
            }
        }
    }
}