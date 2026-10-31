using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using MetroPinry.Models;
using MetroPinry.Services;
using MetroPinry.ViewModels;
using MetroPinry.Views;

namespace MetroPinry
{
    public partial class MainPage : PhoneApplicationPage
    {
        public MainViewModel ViewModel
        {
            get { return App.ViewModel; }
        }

        public MainPage()
        {
            InitializeComponent();
            DataContext = App.ViewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Проверка авторизации
            if (!StorageService.IsAuthenticated)
            {
                NavigationService.Navigate(new Uri("/Views/LoginPage.xaml", UriKind.Relative));
                return;
            }

            // Загрузка данных при первом запуске
            if (!App.ViewModel.IsDataLoaded)
            {
                await App.ViewModel.LoadDataAsync();
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

        // Клик по пину в секции feed -> переход к просмотру пина
        private void OnPinSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selector = sender as LongListSelector;
            if (selector == null || selector.SelectedItem == null) return;

            var selectedPin = selector.SelectedItem as Pin;

            // Сбрасываем выбор сразу, чтобы список не удерживал фокус
            selector.SelectedItem = null;

            PinDetailPage.SelectedPin = selectedPin;
            NavigationService.Navigate(new Uri("/Views/PinDetailPage.xaml", UriKind.Relative));
        }

        // Клик по плитке доски в секции boards -> переход к содержимому доски
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
            ResetFeedScrollToTop();
        }

        private void OnAddPinAppBarClick(object sender, EventArgs e)
        {
            // NavigationService.Navigate(new Uri("/Views/NewPinPage.xaml", UriKind.Relative));
        }

        private void OnMyPinsClick(object sender, RoutedEventArgs e) { }
        private void OnCreateBoardClick(object sender, RoutedEventArgs e) { }
        private void OnSettingsClick(object sender, RoutedEventArgs e) { }
    }
}