using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using MetroPinry.Models;
using MetroPinry.Services;
using MetroPinry.ViewModels;
using MetroPinry.Views;

namespace MetroPinry
{
    public partial class MainPage : PhoneApplicationPage
    {
        private readonly ObservableCollection<Board> _filteredBoards = new ObservableCollection<Board>();
        private readonly ObservableCollection<Pin> _filteredPins = new ObservableCollection<Pin>();
        private bool _isPageLoaded = false;
        private int _lastPanoramaIndex = -1;

        public static Pin LastViewedPin = null;

        public MainViewModel ViewModel
        {
            get { return App.ViewModel; }
        }

        public MainPage()
        {
            InitializeComponent();
            DataContext = App.ViewModel;

            FoundBoardsList.ItemsSource = _filteredBoards;
            FoundPinsList.ItemsSource = _filteredPins;

            this.Loaded += (s, e) =>
            {
                if (!_isPageLoaded)
                {
                    _isPageLoaded = true;
                    UpdateAppBarButtons();
                }
            };
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.NavigationMode == NavigationMode.Back)
            {
                if (LastViewedPin != null && FeedList != null)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            FeedList.ScrollTo(LastViewedPin);
                        }
                        catch { }
                        LastViewedPin = null;
                    });
                }
                return;
            }

            bool forceRefresh = NavigationContext.QueryString.ContainsKey("refresh");
            if (forceRefresh)
            {
                NavigationContext.QueryString.Remove("refresh");
            }

            bool isAddMode = NavigationContext.QueryString.ContainsKey("mode") && NavigationContext.QueryString["mode"] == "add";
            if (e.NavigationMode == NavigationMode.New && !isAddMode)
            {
                while (NavigationService.CanGoBack)
                {
                    NavigationService.RemoveBackEntry();
                }
            }

            bool isGuest = false;
            var settings = System.IO.IsolatedStorage.IsolatedStorageSettings.ApplicationSettings;
            if (settings.Contains("IsGuest"))
            {
                isGuest = (bool)settings["IsGuest"];
            }

            if (!StorageService.IsAuthenticated && !isGuest)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    NavigationService.Navigate(new Uri("/Views/LoginPage.xaml", UriKind.Relative));
                });
                return;
            }

            if (!App.ViewModel.IsDataLoaded || App.ViewModel.RecentPins.Count == 0 || forceRefresh)
            {
                await App.ViewModel.LoadDataAsync();
                ResetFeedScrollToTop();
            }

            if (SearchBox != null && !string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                PerformSearch(SearchBox.Text.Trim());
            }

            UpdateAppBarButtons();
        }

        private void ResetFeedScrollToTop()
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (FeedList != null && App.ViewModel.RecentPins != null && App.ViewModel.RecentPins.Count > 0)
                {
                    FeedList.SelectedItem = null;
                    var firstItem = App.ViewModel.RecentPins.FirstOrDefault();
                    if (firstItem != null)
                    {
                        try
                        {
                            FeedList.ScrollTo(firstItem);
                        }
                        catch { }
                    }
                }
            });
        }

        // ==========================================
        // БЛОКИРОВКА СЛУЧАЙНЫХ ДЕЙСТВИЙ (ЧУЖИЕ ПИНЫ, ЧУЖИЕ ДОСКИ, ДОСКА ALL)
        // ==========================================

        private void OnItemHold(object sender, System.Windows.Input.GestureEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;

            if (ShouldSuppressContextMenu(element.DataContext))
            {
                e.Handled = true;
                var menu = ContextMenuService.GetContextMenu(element);
                if (menu != null)
                {
                    menu.IsOpen = false;
                }
            }
        }

        private void OnContextMenuOpened(object sender, RoutedEventArgs e)
        {
            var menu = sender as ContextMenu;
            if (menu == null) return;

            if (ShouldSuppressContextMenu(menu.DataContext))
            {
                menu.IsOpen = false;
            }
        }

        private bool ShouldSuppressContextMenu(object dataContext)
        {
            var settings = System.IO.IsolatedStorage.IsolatedStorageSettings.ApplicationSettings;
            bool isGuest = settings.Contains("IsGuest") && (bool)settings["IsGuest"];
            if (isGuest) return true;

            // 1. Проверка для пинов
            var pin = dataContext as Pin;
            if (pin != null)
            {
                return !pin.IsMine;
            }

            // 2. Проверка для досок
            var board = dataContext as Board;
            if (board != null)
            {
                // Запрещено для виртуальной доски "all" (Id == -1) и чужих досок
                if (board.Id == -1 || !board.IsMine)
                {
                    return true;
                }
            }

            return false;
        }

        // ==========================================
        // ДЕЙСТВИЯ МЕНЮ
        // ==========================================

        private void OnEditBoardContextClick(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;

            var board = menuItem.DataContext as Board;
            if (board == null || board.Id == -1 || !board.IsMine) return;

            NewBoardPage.BoardToEdit = board;
            NavigationService.Navigate(new Uri("/Views/NewBoardPage.xaml", UriKind.Relative));
        }

        private async void OnDeleteBoardContextClick(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;

            var board = menuItem.DataContext as Board;
            if (board == null || board.Id == -1 || !board.IsMine) return;

            var result = MessageBox.Show(
                string.Format("Удалить доску \"{0}\"? Пины останутся в вашей ленте.", board.Name),
                "Удаление доски",
                MessageBoxButton.OKCancel);

            if (result == MessageBoxResult.OK)
            {
                var api = new PinryApiService();
                bool success = await api.DeleteBoardAsync(board.Id);

                if (success)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        App.ViewModel.Boards.Remove(board);
                        App.ViewModel.UserBoardsWithAll.Remove(board);
                        _filteredBoards.Remove(board);
                    });
                }
                else
                {
                    MessageBox.Show("Не удалось удалить доску. Проверьте права доступа и сеть.", "Ошибка", MessageBoxButton.OK);
                }
            }
        }

        private void OnEditPinContextClick(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;

            var pin = menuItem.DataContext as Pin;
            if (pin == null || !pin.IsMine) return;

            NewPinPage.PinToEdit = pin;
            NewPinPage.PreselectedBoard = null;
            NavigationService.Navigate(new Uri("/Views/NewPinPage.xaml", UriKind.Relative));
        }

        private async void OnDeletePinContextClick(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;

            var pin = menuItem.DataContext as Pin;
            if (pin == null || !pin.IsMine) return;

            var result = MessageBox.Show("Удалить этот пин навсегда?", "Удаление пина", MessageBoxButton.OKCancel);
            if (result == MessageBoxResult.OK)
            {
                var api = new PinryApiService();
                bool success = await api.DeletePinAsync(pin.Id);

                if (success)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        if (App.ViewModel.RecentPins != null && App.ViewModel.RecentPins.Contains(pin))
                        {
                            App.ViewModel.RecentPins.Remove(pin);
                        }
                        if (App.ViewModel.UserPins != null && App.ViewModel.UserPins.Contains(pin))
                        {
                            App.ViewModel.UserPins.Remove(pin);
                        }
                        _filteredPins.Remove(pin);

                        var allBoard = App.ViewModel.UserBoardsWithAll.FirstOrDefault(b => b.Id == -1);
                        if (allBoard != null)
                        {
                            allBoard.PinsCount = App.ViewModel.UserPins != null ? App.ViewModel.UserPins.Count : 0;
                        }
                    });
                }
                else
                {
                    MessageBox.Show("Не удалось удалить пин.", "Ошибка", MessageBoxButton.OK);
                }
            }
        }

        // ==========================================
        // ПОИСК И НАВИГАЦИЯ
        // ==========================================

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchBox.Text != null ? SearchBox.Text.Trim() : string.Empty;
            PerformSearch(query);
        }

        private void PerformSearch(string query)
        {
            _filteredBoards.Clear();
            _filteredPins.Clear();

            if (string.IsNullOrWhiteSpace(query))
            {
                SearchEmptyState.Text = "enter keywords or tags";
                SearchEmptyState.Visibility = Visibility.Visible;
                FoundBoardsHeaderPanel.Visibility = Visibility.Collapsed;
                FoundPinsHeaderPanel.Visibility = Visibility.Collapsed;
                return;
            }

            string cleanQuery = query.TrimStart('#');

            if (App.ViewModel.Boards != null)
            {
                var matchedBoards = App.ViewModel.Boards
                    .Where(b => !string.IsNullOrEmpty(b.Name) &&
                                b.Name.IndexOf(cleanQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

                foreach (var b in matchedBoards)
                {
                    _filteredBoards.Add(b);
                }
            }

            var allPins = new List<Pin>();
            if (App.ViewModel.RecentPins != null) allPins.AddRange(App.ViewModel.RecentPins);
            if (App.ViewModel.UserPins != null)
            {
                foreach (var up in App.ViewModel.UserPins)
                {
                    if (!allPins.Any(p => p.Id == up.Id))
                    {
                        allPins.Add(up);
                    }
                }
            }

            var matchedPins = allPins.Where(p =>
                (!string.IsNullOrEmpty(p.Description) && p.Description.IndexOf(cleanQuery, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (p.Tags != null && p.Tags.Any(t => t != null && t.TrimStart('#').IndexOf(cleanQuery, StringComparison.OrdinalIgnoreCase) >= 0))
            ).ToList();

            foreach (var p in matchedPins)
            {
                _filteredPins.Add(p);
            }

            bool hasBoards = _filteredBoards.Count > 0;
            bool hasPins = _filteredPins.Count > 0;

            FoundBoardsHeaderPanel.Visibility = hasBoards ? Visibility.Visible : Visibility.Collapsed;
            FoundPinsHeaderPanel.Visibility = hasPins ? Visibility.Visible : Visibility.Collapsed;

            if (!hasBoards && !hasPins)
            {
                SearchEmptyState.Text = "no results found";
                SearchEmptyState.Visibility = Visibility.Visible;
            }
            else
            {
                SearchEmptyState.Visibility = Visibility.Collapsed;
            }
        }

        private void OnFoundBoardTap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;

            var board = element.DataContext as Board;
            if (board == null) return;

            BoardDetailsPage.SelectedBoard = board;
            NavigationService.Navigate(new Uri("/Views/BoardDetailsPage.xaml", UriKind.Relative));
        }

        private void OnFoundPinTap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;

            var pin = element.DataContext as Pin;
            if (pin == null) return;

            LastViewedPin = pin;
            BoardDetailsPage.SelectedBoard = null;
            PinDetailPage.SelectedPin = pin;
            NavigationService.Navigate(new Uri("/Views/PinDetailPage.xaml", UriKind.Relative));
        }

        private void OnPinSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selector = sender as LongListSelector;
            if (selector == null || selector.SelectedItem == null) return;

            var selectedPin = selector.SelectedItem as Pin;
            selector.SelectedItem = null;

            LastViewedPin = selectedPin;
            BoardDetailsPage.SelectedBoard = null;
            PinDetailPage.SelectedPin = selectedPin;
            NavigationService.Navigate(new Uri("/Views/PinDetailPage.xaml", UriKind.Relative));
        }

        private void OnBoardSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selector = sender as LongListSelector;
            if (selector == null || selector.SelectedItem == null) return;

            BoardDetailsPage.SelectedBoard = selector.SelectedItem as Board;
            selector.SelectedItem = null;

            NavigationService.Navigate(new Uri("/Views/BoardDetailsPage.xaml", UriKind.Relative));
        }

        private void OnMyBoardSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selector = sender as LongListSelector;
            if (selector == null || selector.SelectedItem == null) return;

            var board = selector.SelectedItem as Board;
            selector.SelectedItem = null;

            if (board == null) return;

            BoardDetailsPage.SelectedBoard = board;
            NavigationService.Navigate(new Uri("/Views/BoardDetailsPage.xaml", UriKind.Relative));
        }

        private async void OnRefreshAppBarClick(object sender, EventArgs e)
        {
            await App.ViewModel.LoadDataAsync();
            if (SearchBox != null && !string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                PerformSearch(SearchBox.Text.Trim());
            }
            ResetFeedScrollToTop();
        }

        private void MainPanorama_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isPageLoaded || ApplicationBar == null) return;
            if (MainPanorama.SelectedIndex == _lastPanoramaIndex) return;
            _lastPanoramaIndex = MainPanorama.SelectedIndex;

            UpdateAppBarButtons();
        }

        private void UpdateAppBarButtons()
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (ApplicationBar == null || ApplicationBar.Buttons.Count < 2) return;

                var actionBtn = ApplicationBar.Buttons[0] as ApplicationBarIconButton;
                if (actionBtn == null) return;

                switch (MainPanorama.SelectedIndex)
                {
                    case 1: // boards
                        actionBtn.IconUri = new Uri("/Assets/AppBar/layot.png", UriKind.Relative);
                        actionBtn.Text = "add board";
                        actionBtn.IsEnabled = true;
                        break;

                    case 3: // me
                        actionBtn.IconUri = new Uri("/Assets/AppBar/smiley.kiki.png", UriKind.Relative);
                        actionBtn.Text = "accounts";
                        actionBtn.IsEnabled = true;
                        break;

                    default: // feed & search
                        actionBtn.IconUri = new Uri("/Assets/AppBar/pin.png", UriKind.Relative);
                        actionBtn.Text = "add pin";
                        actionBtn.IsEnabled = true;
                        break;
                }

                var settings = System.IO.IsolatedStorage.IsolatedStorageSettings.ApplicationSettings;
                if (settings.Contains("IsGuest") && (bool)settings["IsGuest"])
                {
                    if (MainPanorama.SelectedIndex != 3)
                    {
                        actionBtn.IsEnabled = false;
                    }
                }
            });
        }

        private void OnAccountsAppBarClick(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/Views/AccountsPage.xaml", UriKind.Relative));
        }

        private void OnAddAccountMenuClick(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/Views/LoginPage.xaml?mode=add", UriKind.Relative));
        }

        private void OnAddAppBarClick(object sender, EventArgs e)
        {
            if (MainPanorama.SelectedIndex == 1)
            {
                NewBoardPage.BoardToEdit = null;
                NavigationService.Navigate(new Uri("/Views/NewBoardPage.xaml", UriKind.Relative));
            }
            else if (MainPanorama.SelectedIndex == 3)
            {
                NavigationService.Navigate(new Uri("/Views/AccountsPage.xaml", UriKind.Relative));
            }
            else
            {
                NewPinPage.PinToEdit = null;
                NewPinPage.PreselectedBoard = null;
                NavigationService.Navigate(new Uri("/Views/NewPinPage.xaml", UriKind.Relative));
            }
        }

        private void OnTokenTap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            string token = App.ViewModel.CurrentUser?.Token ?? StorageService.AuthToken;
            if (!string.IsNullOrEmpty(token))
            {
                Clipboard.SetText(token);
                MessageBox.Show("API токен скопирован в буфер обмена.", "Готово", MessageBoxButton.OK);
            }
        }

        private void OnSignOutMenuClick(object sender, EventArgs e)
        {
            var settings = System.IO.IsolatedStorage.IsolatedStorageSettings.ApplicationSettings;
            bool isGuest = settings.Contains("IsGuest") && (bool)settings["IsGuest"];

            string message = isGuest ? "Покинуть гостевой режим и войти в аккаунт?" : "Выйти из текущего аккаунта?";

            var result = MessageBox.Show(message, "Выход", MessageBoxButton.OKCancel);
            if (result == MessageBoxResult.OK)
            {
                StorageService.AuthToken = null;
                StorageService.ServerUrl = null;

                if (settings.Contains("IsGuest"))
                {
                    settings.Remove("IsGuest");
                    settings.Save();
                }

                App.ViewModel.RecentPins.Clear();
                App.ViewModel.Boards.Clear();
                App.ViewModel.UserPins.Clear();
                App.ViewModel.UserBoardsWithAll.Clear();
                App.ViewModel.CurrentUser = null;

                NavigationService.Navigate(new Uri("/Views/LoginPage.xaml", UriKind.Relative));
            }
        }

        private async void FeedList_ItemRealized(object sender, ItemRealizationEventArgs e)
        {
            if (App.ViewModel.IsLoadingMore) return;

            var pin = e.Container.Content as Pin;
            if (pin != null && App.ViewModel.RecentPins != null && App.ViewModel.RecentPins.Count > 0)
            {
                int total = App.ViewModel.RecentPins.Count;
                int currentIdx = App.ViewModel.RecentPins.IndexOf(pin);

                if (currentIdx >= total - 3)
                {
                    await App.ViewModel.LoadMorePinsAsync();
                }
            }
        }
    }
}