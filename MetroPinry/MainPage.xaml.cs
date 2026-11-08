using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using MetroPinry.Models;
using MetroPinry.Resources;
using MetroPinry.Services;
using MetroPinry.ViewModels;
using MetroPinry.Views;

namespace MetroPinry
{
    public partial class MainPage : PhoneApplicationPage, System.ComponentModel.INotifyPropertyChanged
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

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
            }
        }

        private bool _hasReachedEnd;
        public bool HasReachedEnd
        {
            get { return _hasReachedEnd; }
            set
            {
                if (_hasReachedEnd != value)
                {
                    _hasReachedEnd = value;
                    NotifyPropertyChanged("HasReachedEnd");
                }
            }
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
                    UpdateAppBarLocalization();
                    UpdateAppBarButtons();
                }
            };
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (ApplicationBar != null)
            {
                ApplicationBar.IsVisible = true;
            }

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
                HasReachedEnd = false;
                await App.ViewModel.LoadDataAsync();
                ResetFeedScrollToTop();
            }

            if (SearchBox != null && !string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                PerformSearch(SearchBox.Text.Trim());
            }

            UpdateAppBarLocalization();
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

        private void UpdateAppBarLocalization()
        {
            if (ApplicationBar == null) return;

            if (ApplicationBar.Buttons.Count >= 2)
            {
                var refreshBtn = ApplicationBar.Buttons[1] as ApplicationBarIconButton;
                if (refreshBtn != null)
                {
                    refreshBtn.Text = AppResources.Refresh;
                }
            }

            if (ApplicationBar.MenuItems.Count >= 4)
            {
                ((ApplicationBarMenuItem)ApplicationBar.MenuItems[0]).Text = AppResources.ManageAccounts;
                ((ApplicationBarMenuItem)ApplicationBar.MenuItems[1]).Text = AppResources.AddAccount;
                ((ApplicationBarMenuItem)ApplicationBar.MenuItems[2]).Text = AppResources.ChooseLanguage;
                ((ApplicationBarMenuItem)ApplicationBar.MenuItems[3]).Text = AppResources.SignOut;
            }
        }

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

            var pin = dataContext as Pin;
            if (pin != null)
            {
                return !pin.IsMine;
            }

            var board = dataContext as Board;
            if (board != null)
            {
                if (board.Id == -1 || !board.IsMine)
                {
                    return true;
                }
            }

            return false;
        }

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
                string.Format(AppResources.DeleteBoardConfirmFormat, board.Name),
                AppResources.Delete,
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
                    MessageBox.Show("Error deleting board.", AppResources.AppTitle, MessageBoxButton.OK);
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

            var result = MessageBox.Show(AppResources.DeletePinConfirm, AppResources.Delete, MessageBoxButton.OKCancel);
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
                    MessageBox.Show("Error deleting pin.", AppResources.AppTitle, MessageBoxButton.OK);
                }
            }
        }

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
                SearchEmptyState.Text = AppResources.SearchEmptyState;
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
                SearchEmptyState.Text = AppResources.NoResultsFound;
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
            HasReachedEnd = false;
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
                    case 1:
                        actionBtn.IconUri = new Uri("/Assets/AppBar/layout.png", UriKind.Relative);
                        actionBtn.Text = AppResources.AddBoard;
                        actionBtn.IsEnabled = true;
                        break;
                    case 3:
                        actionBtn.IconUri = new Uri("/Assets/AppBar/smiley.kiki.png", UriKind.Relative);
                        actionBtn.Text = AppResources.Accounts;
                        actionBtn.IsEnabled = true;
                        break;
                    default:
                        actionBtn.IconUri = new Uri("/Assets/AppBar/pin.png", UriKind.Relative);
                        actionBtn.Text = AppResources.AddPin;
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
                MessageBox.Show(AppResources.TokenCopied, AppResources.AppTitle, MessageBoxButton.OK);
            }
        }

        private void OnSignOutMenuClick(object sender, EventArgs e)
        {
            var settings = System.IO.IsolatedStorage.IsolatedStorageSettings.ApplicationSettings;
            bool isGuest = settings.Contains("IsGuest") && (bool)settings["IsGuest"];

            string message = isGuest ? AppResources.GuestSignOutConfirm : AppResources.SignOutConfirm;

            var result = MessageBox.Show(message, AppResources.SignOut, MessageBoxButton.OKCancel);
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
            if (App.ViewModel.IsLoadingMore || HasReachedEnd) return;

            var pin = e.Container.Content as Pin;
            if (pin != null && App.ViewModel.RecentPins != null && App.ViewModel.RecentPins.Count > 0)
            {
                int total = App.ViewModel.RecentPins.Count;
                int currentIdx = App.ViewModel.RecentPins.IndexOf(pin);

                if (currentIdx >= total - 3)
                {
                    int beforeCount = App.ViewModel.RecentPins.Count;
                    await App.ViewModel.LoadMorePinsAsync();
                    int afterCount = App.ViewModel.RecentPins.Count;

                    if (beforeCount == afterCount)
                    {
                        HasReachedEnd = true;
                    }
                }
            }
        }

        private void OnSwitchLanguageMenuClick(object sender, EventArgs e)
        {
            var currentCulture = System.Threading.Thread.CurrentThread.CurrentUICulture.Name;

            var availableLanguages = new List<LanguageItem>
            {
                new LanguageItem { Code = "en-US", NativeName = "English" },
                new LanguageItem { Code = "ru", NativeName = "Русский" }
            };

            var stack = new StackPanel { Margin = new Thickness(12, 12, 12, 0) };

            var messageBox = new CustomMessageBox
            {
                Title = AppResources.AppTitle,
                Message = AppResources.ChooseLanguage,
                Content = stack,
                LeftButtonContent = AppResources.Cancel,
                IsFullScreen = false
            };

            string selectedLanguageCode = null;

            foreach (var lang in availableLanguages)
            {
                bool isCurrent = currentCulture.StartsWith(lang.Code.Substring(0, 2), StringComparison.OrdinalIgnoreCase);

                var btn = new Button
                {
                    Content = lang.NativeName,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(-12, 0, -12, 8),
                    BorderThickness = new Thickness(0),
                    FontSize = 26,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe WP SemiLight")
                };

                if (isCurrent)
                {
                    btn.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["PhoneAccentBrush"];
                }

                string code = lang.Code;
                btn.Click += (s, args) =>
                {
                    selectedLanguageCode = code;
                    messageBox.Dismiss();
                };

                stack.Children.Add(btn);
            }

            messageBox.Dismissed += (s, args) =>
            {
                if (!string.IsNullOrEmpty(selectedLanguageCode))
                {
                    ApplyNewLanguage(selectedLanguageCode);
                }
                else if (ApplicationBar != null)
                {
                    ApplicationBar.IsVisible = true;
                }
            };

            messageBox.Show();
        }

        private void ApplyNewLanguage(string targetLang)
        {
            var currentCulture = System.Threading.Thread.CurrentThread.CurrentUICulture.Name;
            if (currentCulture.Equals(targetLang, StringComparison.OrdinalIgnoreCase)) return;

            var settings = System.IO.IsolatedStorage.IsolatedStorageSettings.ApplicationSettings;
            settings["AppLanguage"] = targetLang;
            settings.Save();

            var culture = new System.Globalization.CultureInfo(targetLang);
            System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = culture;

            var frame = Application.Current.RootVisual as PhoneApplicationFrame;
            if (frame != null)
            {
                frame.Language = XmlLanguage.GetLanguage(culture.Name);
            }

            LocalizedStrings.UpdateLanguage();

            var currentVm = this.DataContext;
            this.DataContext = null;
            this.DataContext = currentVm;

            Dispatcher.BeginInvoke(() =>
            {
                if (ApplicationBar != null)
                {
                    ApplicationBar.IsVisible = true;
                    UpdateAppBarLocalization();
                    UpdateAppBarButtons();
                }
            });
        }

        private void HideScrollBar(DependencyObject parent)
        {
            if (parent == null) return;

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var scrollBar = child as ScrollBar;

                // Полный путь до Orientation снимает конфликт с PageOrientation
                if (scrollBar != null && scrollBar.Orientation == System.Windows.Controls.Orientation.Vertical)
                {
                    scrollBar.Visibility = Visibility.Collapsed;
                    scrollBar.Width = 0;
                    scrollBar.Opacity = 0;
                    scrollBar.IsHitTestVisible = false;
                }

                HideScrollBar(child);
            }
        }

        private void MainPanorama_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (FeedList != null)
                {
                    HideScrollBar(FeedList);
                }
            });
        }
    }
}