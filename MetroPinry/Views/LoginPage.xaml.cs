using System;
using System.Windows;
using Microsoft.Phone.Controls;
using System.Windows.Navigation;
using System.Windows.Controls;

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

        private async void OnActionBtnClick(object sender, RoutedEventArgs e)
        {
            string serverUrl = ServerUrlBox.Text;
            string username = UsernameBox.Text;
            string password = PasswordBox.Password;
            string email = EmailBox.Text;
            string token = TokenBox.Text;

            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                MessageBox.Show("Введите адрес сервера.");
                return;
            }

            // Проверяем, все ли заполнено для текущего режима
            if (_isTokenMode)
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    MessageBox.Show("Введите API токен.");
                    return;
                }
            }
            else if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || (_isRegisterMode && string.IsNullOrWhiteSpace(email)))
            {
                MessageBox.Show("Пожалуйста, заполните все поля.");
                return;
            }

            LoginProgressBar.Visibility = Visibility.Visible;
            ActionBtn.IsEnabled = false;

            bool isSuccess = false;

            try
            {
                var apiService = new Services.PinryApiService();

                if (_isTokenMode)
                {
                    // ВХОД ПО ТОКЕНУ
                    Services.StorageService.ServerUrl = serverUrl;
                    Services.StorageService.AuthToken = token;

                    // Запрашиваем профиль, чтобы проверить, рабочий ли токен
                    var profile = await apiService.GetCurrentUserAsync();
                    if (profile != null)
                    {
                        isSuccess = true;
                    }
                    else
                    {
                        Services.StorageService.AuthToken = null; // Откатываем неверный токен
                        throw new Exception("Неверный токен или сервер недоступен.");
                    }
                }
                else if (_isRegisterMode)
                {
                    // РЕГИСТРАЦИЯ
                    bool registered = await apiService.RegisterWithCredentialsAsync(serverUrl, username, email, password);
                    if (registered)
                    {
                        var profile = await apiService.LoginWithCredentialsAsync(serverUrl, username, password);
                        isSuccess = (profile != null);
                    }
                }
                else
                {
                    // ОБЫЧНЫЙ ЛОГИН
                    var profile = await apiService.LoginWithCredentialsAsync(serverUrl, username, password);
                    isSuccess = (profile != null);
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
                if (NavigationService.CanGoBack)
                {
                    NavigationService.GoBack();
                }
                else
                {
                    NavigationService.Navigate(new Uri("/MainPage.xaml", UriKind.Relative));
                }
            }
        }

        private void OnToggleModeClick(object sender, EventArgs e)
        {
            _isRegisterMode = !_isRegisterMode;

            // Динамически меняем текст в меню
            var toggleMenu = (Microsoft.Phone.Shell.ApplicationBarMenuItem)ApplicationBar.MenuItems[0];

            if (_isRegisterMode)
            {
                PageTitle.Text = "sign up";
                ActionBtn.Content = "register";
                toggleMenu.Text = "switch to sign in";
                EmailPanel.Visibility = Visibility.Visible;
            }
            else
            {
                PageTitle.Text = "sign in";
                ActionBtn.Content = "sign in";
                toggleMenu.Text = "create account";
                EmailPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void OnSwitchAuthModeClick(object sender, EventArgs e)
        {
            _isTokenMode = !_isTokenMode;

            // Находим кнопку токена в меню (она идет второй, поэтому индекс [1])
            var tokenMenu = (Microsoft.Phone.Shell.ApplicationBarMenuItem)ApplicationBar.MenuItems[1];

            if (_isTokenMode)
            {
                CredentialsPanel.Visibility = Visibility.Collapsed;
                TokenPanel.Visibility = Visibility.Visible;
                tokenMenu.Text = "use password"; // Меняем текст для возврата

                // Скрываем поле Email, если параллельно был открыт режим регистрации
                EmailPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                CredentialsPanel.Visibility = Visibility.Visible;
                TokenPanel.Visibility = Visibility.Collapsed;
                tokenMenu.Text = "use api token"; // Возвращаем исходный текст

                // Если мы до этого включили режим регистрации - возвращаем поле Email на место
                if (_isRegisterMode)
                {
                    EmailPanel.Visibility = Visibility.Visible;
                }
            }
        }

        private void OnGuestModeClick(object sender, EventArgs e)
        {
            string serverUrl = ServerUrlBox.Text;
            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                MessageBox.Show("Укажите адрес сервера для просмотра.");
                return;
            }

            var settings = System.IO.IsolatedStorage.IsolatedStorageSettings.ApplicationSettings;
            settings["IsGuest"] = true;
            settings.Save();

            Services.StorageService.AuthToken = null;
            Services.StorageService.ServerUrl = serverUrl;

            Dispatcher.BeginInvoke(() =>
            {
                NavigationService.Navigate(new Uri("/MainPage.xaml", UriKind.Relative));
            });
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Очищаем историю, чтобы нельзя было вернуться "назад" в приложение без логина
            if (e.NavigationMode == System.Windows.Navigation.NavigationMode.New)
            {
                while (NavigationService.CanGoBack)
                {
                    NavigationService.RemoveBackEntry();
                }
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // Связываем каждое поле с его подсказкой
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

    }
}