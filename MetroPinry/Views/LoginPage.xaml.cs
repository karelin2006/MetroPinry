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
    public partial class LoginPage : PhoneApplicationPage
    {
        private bool _isRegisterMode = false;
        private bool _isTokenMode = false;

        public LoginPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            bool isAddMode = NavigationContext.QueryString.ContainsKey("mode") && NavigationContext.QueryString["mode"] == "add";

            if (e.NavigationMode == NavigationMode.New)
            {
                if (!isAddMode)
                {
                    while (NavigationService.CanGoBack)
                    {
                        NavigationService.RemoveBackEntry();
                    }
                }
                else
                {
                    ServerUrlBox.Text = "https://";
                    UsernameBox.Text = string.Empty;
                    PasswordBox.Password = string.Empty;
                    PasswordRepeatBox.Password = string.Empty;
                    EmailBox.Text = string.Empty;
                    TokenBox.Text = string.Empty;
                }
            }

            RefreshSavedAccountsList(isAddMode);
        }

        private void RefreshSavedAccountsList(bool isAddMode)
        {
            var accounts = StorageService.Accounts;
            if (accounts != null && accounts.Count > 0 && !isAddMode)
            {
                SavedAccountsList.ItemsSource = accounts;
                SavedAccountsSection.Visibility = Visibility.Visible;
            }
            else
            {
                SavedAccountsList.ItemsSource = null;
                SavedAccountsSection.Visibility = Visibility.Collapsed;
            }
        }

        // БЫСТРЫЙ ВХОД ПО КЛИКУ НА СОХРАНЕННЫЙ АККАУНТ
        private async void OnSavedAccountTap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;

            // Используем существующий PinryAccount
            var account = element.DataContext as PinryAccount;
            if (account == null) return;

            LoginProgressBar.Visibility = Visibility.Visible;
            ActionBtn.IsEnabled = false;

            try
            {
                StorageService.ServerUrl = account.ServerUrl;
                StorageService.AuthToken = account.Token;

                var settings = System.IO.IsolatedStorage.IsolatedStorageSettings.ApplicationSettings;
                if (settings.Contains("IsGuest"))
                {
                    settings.Remove("IsGuest");
                    settings.Save();
                }

                var apiService = new PinryApiService();
                var profile = await apiService.GetCurrentUserAsync();

                if (profile != null)
                {
                    StorageService.SaveOrUpdateAccount(account.ServerUrl, profile.Username, account.Token, profile.GravatarHash);
                    await StorageService.DownloadAndSaveAvatarAsync(profile.Username, profile.GravatarHash);

                    NavigateToMainAfterAuth();
                }
                else
                {
                    StorageService.AuthToken = null;
                    MessageBox.Show("Не удалось войти в этот аккаунт. Возможно, токен устарел или сервер временно недоступен.", "Ошибка входа", MessageBoxButton.OK);
                }
            }
            catch (Exception ex)
            {
                StorageService.AuthToken = null;
                MessageBox.Show("Ошибка входа: " + ex.Message, "Ошибка", MessageBoxButton.OK);
            }
            finally
            {
                LoginProgressBar.Visibility = Visibility.Collapsed;
                ActionBtn.IsEnabled = true;
            }
        }

        private async void OnActionBtnClick(object sender, RoutedEventArgs e)
        {
            this.Focus();

            string serverUrl = ServerUrlBox.Text != null ? ServerUrlBox.Text.Trim() : string.Empty;

            if (string.IsNullOrWhiteSpace(serverUrl) || serverUrl == "https://" || serverUrl == "http://")
            {
                MessageBox.Show("Введите адрес сервера.");
                return;
            }

            if (!serverUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !serverUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                serverUrl = "https://" + serverUrl;
                ServerUrlBox.Text = serverUrl;
            }

            string username = UsernameBox.Text != null ? UsernameBox.Text.Trim() : string.Empty;
            string password = PasswordBox.Password;
            string passwordRepeat = PasswordRepeatBox.Password;
            string email = EmailBox.Text != null ? EmailBox.Text.Trim() : string.Empty;
            string token = TokenBox.Text != null ? TokenBox.Text.Trim() : string.Empty;

            if (_isTokenMode)
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    MessageBox.Show("Введите API токен.");
                    return;
                }
            }
            else if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) ||
                    (_isRegisterMode && (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(passwordRepeat))))
            {
                MessageBox.Show("Пожалуйста, заполните все поля.");
                return;
            }

            if (_isRegisterMode && password != passwordRepeat)
            {
                MessageBox.Show("Пароли не совпадают.");
                return;
            }

            LoginProgressBar.Visibility = Visibility.Visible;
            ActionBtn.IsEnabled = false;

            bool isSuccess = false;

            try
            {
                var apiService = new PinryApiService();

                var settings = System.IO.IsolatedStorage.IsolatedStorageSettings.ApplicationSettings;
                if (settings.Contains("IsGuest"))
                {
                    settings.Remove("IsGuest");
                    settings.Save();
                }

                if (_isTokenMode)
                {
                    StorageService.ServerUrl = serverUrl;
                    StorageService.AuthToken = token;

                    var profile = await apiService.GetCurrentUserAsync();
                    if (profile != null)
                    {
                        isSuccess = true;
                        StorageService.SaveOrUpdateAccount(serverUrl, profile.Username, token, profile.GravatarHash);
                        await StorageService.DownloadAndSaveAvatarAsync(profile.Username, profile.GravatarHash);
                    }
                    else
                    {
                        StorageService.AuthToken = null;
                        throw new Exception("Неверный токен или сервер недоступен.");
                    }
                }
                else if (_isRegisterMode)
                {
                    bool registered = await apiService.RegisterWithCredentialsAsync(serverUrl, username, email, password, passwordRepeat);
                    if (registered)
                    {
                        var profile = await apiService.LoginWithCredentialsAsync(serverUrl, username, password);
                        if (profile != null)
                        {
                            isSuccess = true;
                            StorageService.SaveOrUpdateAccount(serverUrl, profile.Username, profile.Token, profile.GravatarHash);
                            await StorageService.DownloadAndSaveAvatarAsync(profile.Username, profile.GravatarHash);
                        }
                    }
                }
                else
                {
                    var profile = await apiService.LoginWithCredentialsAsync(serverUrl, username, password);
                    if (profile != null)
                    {
                        isSuccess = true;
                        StorageService.SaveOrUpdateAccount(serverUrl, profile.Username, profile.Token, profile.GravatarHash);
                        await StorageService.DownloadAndSaveAvatarAsync(profile.Username, profile.GravatarHash);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK);
            }
            finally
            {
                LoginProgressBar.Visibility = Visibility.Collapsed;
                ActionBtn.IsEnabled = true;
            }

            if (isSuccess)
            {
                NavigateToMainAfterAuth();
            }
        }

        private void NavigateToMainAfterAuth()
        {
            App.ViewModel.UserPins.Clear();
            App.ViewModel.RecentPins.Clear();
            App.ViewModel.Boards.Clear();
            App.ViewModel.UserBoardsWithAll.Clear();
            App.ViewModel.CurrentUser = null;
            App.ViewModel.IsDataLoaded = false;

            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
            else
            {
                NavigationService.Navigate(new Uri("/MainPage.xaml", UriKind.Relative));
            }
        }

        private void OnToggleModeClick(object sender, EventArgs e)
        {
            _isRegisterMode = !_isRegisterMode;
            var toggleMenu = (ApplicationBarMenuItem)ApplicationBar.MenuItems[0];

            if (_isRegisterMode)
            {
                PageTitle.Text = "sign up";
                ActionBtn.Content = "register";
                toggleMenu.Text = "switch to sign in";
                EmailPanel.Visibility = Visibility.Visible;
                PasswordRepeatPanel.Visibility = Visibility.Visible;
            }
            else
            {
                PageTitle.Text = "sign in";
                ActionBtn.Content = "sign in";
                toggleMenu.Text = "create account";
                EmailPanel.Visibility = Visibility.Collapsed;
                PasswordRepeatPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void OnSwitchAuthModeClick(object sender, EventArgs e)
        {
            _isTokenMode = !_isTokenMode;
            var tokenMenu = (ApplicationBarMenuItem)ApplicationBar.MenuItems[1];

            if (_isTokenMode)
            {
                CredentialsPanel.Visibility = Visibility.Collapsed;
                TokenPanel.Visibility = Visibility.Visible;
                tokenMenu.Text = "use password";
                EmailPanel.Visibility = Visibility.Collapsed;
                PasswordRepeatPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                CredentialsPanel.Visibility = Visibility.Visible;
                TokenPanel.Visibility = Visibility.Collapsed;
                tokenMenu.Text = "use api token";

                if (_isRegisterMode)
                {
                    EmailPanel.Visibility = Visibility.Visible;
                    PasswordRepeatPanel.Visibility = Visibility.Visible;
                }
            }
        }

        private void OnGuestModeClick(object sender, EventArgs e)
        {
            string serverUrl = ServerUrlBox.Text != null ? ServerUrlBox.Text.Trim() : string.Empty;
            if (!string.IsNullOrWhiteSpace(serverUrl) && !serverUrl.StartsWith("http://") && !serverUrl.StartsWith("https://"))
            {
                serverUrl = "https://" + serverUrl;
                ServerUrlBox.Text = serverUrl;
            }
            if (string.IsNullOrWhiteSpace(serverUrl) || serverUrl == "https://" || serverUrl == "http://")
            {
                MessageBox.Show("Укажите адрес сервера для просмотра.");
                return;
            }

            var settings = System.IO.IsolatedStorage.IsolatedStorageSettings.ApplicationSettings;
            settings["IsGuest"] = true;
            settings.Save();

            StorageService.AuthToken = null;
            StorageService.ServerUrl = serverUrl;

            App.ViewModel.UserPins.Clear();
            App.ViewModel.RecentPins.Clear();
            App.ViewModel.Boards.Clear();
            App.ViewModel.UserBoardsWithAll.Clear();
            App.ViewModel.CurrentUser = null;
            App.ViewModel.IsDataLoaded = false;

            Dispatcher.BeginInvoke(() =>
            {
                NavigationService.Navigate(new Uri("/MainPage.xaml", UriKind.Relative));
            });
        }

        private void OnManageAccountsClick(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/Views/AccountsPage.xaml", UriKind.Relative));
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            if (textBox.Name == "ServerUrlBox" && ServerUrlHint != null)
                ServerUrlHint.Visibility = string.IsNullOrEmpty(textBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            else if (textBox.Name == "UsernameBox" && UsernameHint != null)
                UsernameHint.Visibility = string.IsNullOrEmpty(textBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            else if (textBox.Name == "EmailBox" && EmailHint != null)
                EmailHint.Visibility = string.IsNullOrEmpty(textBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            else if (textBox.Name == "TokenBox" && TokenHint != null)
                TokenHint.Visibility = string.IsNullOrEmpty(textBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            var passBox = sender as PasswordBox;
            if (passBox != null && PasswordHint != null)
            {
                PasswordHint.Visibility = string.IsNullOrEmpty(passBox.Password) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void PasswordRepeatBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            var passBox = sender as PasswordBox;
            if (passBox != null && PasswordRepeatHint != null)
            {
                PasswordRepeatHint.Visibility = string.IsNullOrEmpty(passBox.Password) ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}