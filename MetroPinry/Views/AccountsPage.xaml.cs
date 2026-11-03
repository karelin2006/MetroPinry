using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using MetroPinry.Models;
using MetroPinry.Services;

namespace MetroPinry.Views
{
    public partial class AccountsPage : PhoneApplicationPage
    {
        public AccountsPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // 1. Отображаем текущий сохраненный список
            AccountsList.ItemsSource = StorageService.Accounts;

            // 2. Если у активного аккаунта нет хеша или аватарки — подтягиваем профиль
            if (StorageService.IsAuthenticated)
            {
                var api = new PinryApiService();
                var profile = await api.GetCurrentUserAsync();
                if (profile != null && !string.IsNullOrEmpty(profile.Username))
                {
                    StorageService.SaveOrUpdateAccount(
                        StorageService.ServerUrl,
                        profile.Username,
                        StorageService.AuthToken,
                        profile.GravatarHash);

                    AccountsList.ItemsSource = null;
                    AccountsList.ItemsSource = StorageService.Accounts;
                }
            }
        }

        private void AccountsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AccountsList.SelectedItem == null) return;

            var targetAccount = AccountsList.SelectedItem as PinryAccount;
            AccountsList.SelectedItem = null; // Снимаем выделение сразу

            if (targetAccount == null) return;

            // Если выбран тот же самый активный аккаунт, просто возвращаемся назад
            if (targetAccount.ServerUrl == StorageService.ServerUrl &&
                targetAccount.Token == StorageService.AuthToken)
            {
                NavigationService.GoBack();
                return;
            }

            // Переключение активного аккаунта
            App.ViewModel.ResetSessionState();
            StorageService.SwitchActiveAccount(targetAccount);
            NavigationService.Navigate(new Uri("/MainPage.xaml?refresh=true", UriKind.Relative));

            while (NavigationService.CanGoBack)
            {
                NavigationService.RemoveBackEntry();
            }
        }

        // Обработчик удержания пальца: блокирует скролл списка и надежно открывает ContextMenu
        private void OnAccountItemHold(object sender, System.Windows.Input.GestureEventArgs e)
        {
            e.Handled = true;

            var element = sender as FrameworkElement;
            if (element == null) return;

            var contextMenu = ContextMenuService.GetContextMenu(element);
            if (contextMenu != null && !contextMenu.IsOpen)
            {
                contextMenu.IsOpen = true;
            }
        }

        // Обработчик удаления аккаунта из контекстного меню
        private void OnDeleteAccountContextClick(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;

            var acc = menuItem.DataContext as PinryAccount;
            if (acc == null) return;

            var result = MessageBox.Show(
                string.Format("Удалить аккаунт {0} ({1}) из сохраненных?", acc.Username, acc.ServerUrl),
                "Удаление аккаунта",
                MessageBoxButton.OKCancel);

            if (result == MessageBoxResult.OK)
            {
                var accs = StorageService.Accounts;
                var toRemove = accs.FirstOrDefault(a => a.ServerUrl == acc.ServerUrl && a.Token == acc.Token);
                if (toRemove != null)
                {
                    accs.Remove(toRemove);
                    StorageService.Accounts = accs;
                }

                // Проверяем, был ли удален активный в данный момент аккаунт
                bool wasActive = (acc.ServerUrl == StorageService.ServerUrl && acc.Token == StorageService.AuthToken);

                if (wasActive)
                {
                    App.ViewModel.ResetSessionState();

                    // Если остались другие аккаунты — переключаемся на первый из списка
                    if (accs.Count > 0)
                    {
                        var nextAcc = accs[0];
                        StorageService.SwitchActiveAccount(nextAcc);
                        NavigationService.Navigate(new Uri("/MainPage.xaml?refresh=true", UriKind.Relative));

                        while (NavigationService.CanGoBack)
                        {
                            NavigationService.RemoveBackEntry();
                        }
                    }
                    else
                    {
                        // Если это был единственный аккаунт — очищаем реквизиты и отправляем на вход
                        StorageService.AuthToken = null;
                        StorageService.ServerUrl = null;
                        NavigationService.Navigate(new Uri("/Views/LoginPage.xaml", UriKind.Relative));

                        while (NavigationService.CanGoBack)
                        {
                            NavigationService.RemoveBackEntry();
                        }
                    }
                }
                else
                {
                    // Был удален неактивный аккаунт — просто обновляем отображаемый список
                    AccountsList.ItemsSource = null;
                    AccountsList.ItemsSource = StorageService.Accounts;
                }
            }
        }

        private void OnAddAccountClick(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/Views/LoginPage.xaml?mode=add", UriKind.Relative));
        }
    }
}