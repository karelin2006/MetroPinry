using System;
using System.Windows;
using Microsoft.Phone.Controls;
using MetroPinry.Services;

namespace MetroPinry.Views
{
    public partial class LoginPage : PhoneApplicationPage
    {
        private readonly PinryApiService _apiService;
        private bool _isTokenMode = false;

        public LoginPage()
        {
            InitializeComponent();
            _apiService = new PinryApiService();

            if (!string.IsNullOrEmpty(StorageService.ServerUrl))
            {
                ServerUrlBox.Text = StorageService.ServerUrl;
            }
            if (!string.IsNullOrEmpty(StorageService.AuthToken))
            {
                TokenBox.Text = StorageService.AuthToken;
            }
        }

        private void OnSwitchAuthModeClick(object sender, RoutedEventArgs e)
        {
            _isTokenMode = !_isTokenMode;

            if (_isTokenMode)
            {
                CredentialsPanel.Visibility = Visibility.Collapsed;
                TokenPanel.Visibility = Visibility.Visible;
                SignInButton.Content = "connect via token";
                SwitchAuthModeButton.Content = "or use username & password";
            }
            else
            {
                CredentialsPanel.Visibility = Visibility.Visible;
                TokenPanel.Visibility = Visibility.Collapsed;
                SignInButton.Content = "sign in";
                SwitchAuthModeButton.Content = "or use api token instead";
            }
        }

        private async void OnSignInClick(object sender, RoutedEventArgs e)
        {
            string url = ServerUrlBox.Text.Trim();
            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show("Введите адрес сервера.");
                return;
            }

            SetLoading(true);

            try
            {
                if (_isTokenMode)
                {
                    string token = TokenBox.Text.Trim();
                    if (string.IsNullOrEmpty(token))
                    {
                        MessageBox.Show("Введите API токен.");
                        return;
                    }

                    await _apiService.VerifyAndLoginWithTokenAsync(url, token);
                }
                else
                {
                    string user = UsernameBox.Text.Trim();
                    string pass = PasswordBox.Password;

                    if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
                    {
                        MessageBox.Show("Введите имя пользователя и пароль.");
                        return;
                    }

                    await _apiService.LoginWithCredentialsAsync(url, user, pass);
                }

                NavigationService.Navigate(new Uri("/MainPage.xaml", UriKind.Relative));
                NavigationService.RemoveBackEntry();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка входа", MessageBoxButton.OK);
            }
            finally
            {
                SetLoading(false);
            }
        }

        private void SetLoading(bool isLoading)
        {
            LoginProgressBar.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            SignInButton.IsEnabled = !isLoading;
            SwitchAuthModeButton.IsEnabled = !isLoading;
            ServerUrlBox.IsEnabled = !isLoading;
            UsernameBox.IsEnabled = !isLoading;
            PasswordBox.IsEnabled = !isLoading;
            TokenBox.IsEnabled = !isLoading;
        }
    }
}