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
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Очищаем стек навигации при новом переходе, чтобы кнопка "Назад" закрывала приложение
            if (e.NavigationMode == System.Windows.Navigation.NavigationMode.New)
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

            // Если не авторизован и НЕ гость — редирект на окно входа
            if (!StorageService.IsAuthenticated && !isGuest)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    NavigationService.Navigate(new Uri("/Views/LoginPage.xaml", UriKind.Relative));
                });
                return;
            }

            // Прячем только кнопку "Добавить", оставляя меню (Sign Out) доступным
            if (isGuest && ApplicationBar != null)
            {
                if (ApplicationBar.Buttons.Count > 0)
                {
                    // Предполагается, что кнопка Add - первая (индекс 0)
                    var addButton = ApplicationBar.Buttons[0] as ApplicationBarIconButton;
                    if (addButton != null)
                    {
                        addButton.IsEnabled = false; // Отключаем её
                    }
                }
            }

            // Загружаем данные...
            if (!App.ViewModel.IsDataLoaded || App.ViewModel.RecentPins.Count == 0)
            {
                await App.ViewModel.LoadDataAsync();
            }

            if (!App.ViewModel.IsDataLoaded || App.ViewModel.RecentPins.Count == 0)
            {
                await App.ViewModel.LoadDataAsync();
            }

            if (SearchBox != null && !string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                PerformSearch(SearchBox.Text.Trim());
            }

            ResetFeedScrollToTop();
        }

        private void ResetFeedScrollToTop()
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (FeedList != null)
                {
                    FeedList.SelectedItem = null;

                    var firstItem = App.ViewModel.RecentPins?.FirstOrDefault();
                    if (firstItem != null)
                    {
                        FeedList.ScrollTo(firstItem);
                    }
                }
            });
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchBox.Text != null ? SearchBox.Text.Trim() : string.Empty;
            PerformSearch(query);
        }

        private void OnSearchClearTapped(object sender, EventArgs e)
        {
            SearchBox.Text = string.Empty;
            PerformSearch(string.Empty);
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
            if (ApplicationBar != null && ApplicationBar.Buttons.Count > 0)
            {
                var addButton = ApplicationBar.Buttons[0] as ApplicationBarIconButton;
                if (addButton != null)
                {
                    if (MainPanorama.SelectedIndex == 1)
                    {
                        addButton.Text = "add board";
                    }
                    else
                    {
                        addButton.Text = "add pin";
                    }
                }
            }
        }

        private void OnAddAppBarClick(object sender, EventArgs e)
        {
            if (MainPanorama.SelectedIndex == 1)
            {
                NewBoardPage.BoardToEdit = null;
                NavigationService.Navigate(new Uri("/Views/NewBoardPage.xaml", UriKind.Relative));
            }
            else
            {
                NewPinPage.PinToEdit = null;
                NewPinPage.PreselectedBoard = null;
                NavigationService.Navigate(new Uri("/Views/NewPinPage.xaml", UriKind.Relative));
            }
        }

        private void OnEditBoardContextClick(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;

            var board = menuItem.DataContext as Board;
            if (board == null) return;

            NewBoardPage.BoardToEdit = board;
            NavigationService.Navigate(new Uri("/Views/NewBoardPage.xaml", UriKind.Relative));
        }

        private async void OnDeleteBoardContextClick(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;

            var board = menuItem.DataContext as Board;
            if (board == null) return;

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
                    App.ViewModel.Boards.Remove(board);
                    _filteredBoards.Remove(board);
                }
                else
                {
                    MessageBox.Show("Не удалось удалить доску. Проверьте права доступа и сеть.", "Ошибка", MessageBoxButton.OK);
                }
            }
        }

        private void OnEditPinContextClick(object sender, RoutedEventArgs e)
        {
            var settings = System.IO.IsolatedStorage.IsolatedStorageSettings.ApplicationSettings;
            if (settings.Contains("IsGuest") && (bool)settings["IsGuest"])
            {
                MessageBox.Show("Для этого действия необходимо войти в аккаунт.");
                return;
            }
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;

            var pin = menuItem.DataContext as Pin;
            if (pin == null) return;

            NewPinPage.PinToEdit = pin;
            NewPinPage.PreselectedBoard = null;
            NavigationService.Navigate(new Uri("/Views/NewPinPage.xaml", UriKind.Relative));
        }

        private async void OnDeletePinContextClick(object sender, RoutedEventArgs e)
        {
            var settings = System.IO.IsolatedStorage.IsolatedStorageSettings.ApplicationSettings;
            if (settings.Contains("IsGuest") && (bool)settings["IsGuest"])
            {
                MessageBox.Show("Для этого действия необходимо войти в аккаунт.");
                return;
            }
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;

            var pin = menuItem.DataContext as Pin;
            if (pin == null) return;

            var result = MessageBox.Show("Удалить этот пин навсегда?", "Удаление пина", MessageBoxButton.OKCancel);
            if (result == MessageBoxResult.OK)
            {
                var api = new PinryApiService();
                bool success = await api.DeletePinAsync(pin.Id);

                if (success)
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
                }
                else
                {
                    MessageBox.Show("Не удалось удалить пин.", "Ошибка", MessageBoxButton.OK);
                }
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

                // Удаляем статус гостя
                if (settings.Contains("IsGuest"))
                {
                    settings.Remove("IsGuest");
                    settings.Save();
                }

                App.ViewModel.RecentPins.Clear();
                App.ViewModel.Boards.Clear();
                App.ViewModel.UserPins.Clear();
                App.ViewModel.CurrentUser = null;

                // Убедитесь, что путь к LoginPage указан верно (в папке /Views/ или в корне)
                NavigationService.Navigate(new Uri("/Views/LoginPage.xaml", UriKind.Relative));
            }
        }
    }
}