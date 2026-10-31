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
                await LoadPinsAsync();
            }
            else
            {
                if (NavigationService.CanGoBack)
                {
                    NavigationService.GoBack();
                }
            }
        }

        private void UpdateBoardHeader()
        {
            if (SelectedBoard == null) return;

            BoardTitle.Text = SelectedBoard.Name;

            if (SelectedBoard.IsPrivate)
            {
                BoardVisibilityIcon.Text = "\uE1F6"; // Замочек
            }
            else
            {
                BoardVisibilityIcon.Text = "\uE12B"; // Глобус
            }
        }

        private async System.Threading.Tasks.Task LoadPinsAsync()
        {
            if (SelectedBoard == null) return;

            BoardProgress.IsVisible = true;
            var api = new PinryApiService();
            var resp = await api.GetBoardPinsAsync(SelectedBoard.Id, SelectedBoard.Name);
            BoardProgress.IsVisible = false;

            System.Windows.Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                _boardPins.Clear();
                if (resp != null && resp.Results != null)
                {
                    Debug.WriteLine("ADDING PINS TO BOARD VIEW: " + resp.Results.Count);
                    foreach (var pin in resp.Results)
                    {
                        // Наполняем свойство Boards вручную, так как API Pinry не отдает его в объекте Pin
                        if (pin.Boards == null)
                        {
                            pin.Boards = new List<object>();
                        }
                        if (!pin.BelongsToBoard(SelectedBoard.Id, SelectedBoard.Name))
                        {
                            pin.Boards.Add(SelectedBoard.Id);
                        }

                        _boardPins.Add(pin);
                    }
                }
            });
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

        // Добавление нового пина строго на текущую доску
        private void OnAddPinToBoardAppBarClick(object sender, EventArgs e)
        {
            if (SelectedBoard == null) return;

            NewPinPage.PinToEdit = null;
            NewPinPage.PreselectedBoard = SelectedBoard;

            NavigationService.Navigate(new Uri("/Views/NewPinPage.xaml", UriKind.Relative));
        }

        // Редактирование текущей доски
        private void OnEditBoardAppBarClick(object sender, EventArgs e)
        {
            if (SelectedBoard == null) return;

            NewBoardPage.BoardToEdit = SelectedBoard;
            NavigationService.Navigate(new Uri("/Views/NewBoardPage.xaml", UriKind.Relative));
        }

        // Контекстное меню пина: Редактировать
        private void OnEditPinContextClick(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;

            var pin = menuItem.DataContext as Pin;
            if (pin == null) return;

            NewPinPage.PinToEdit = pin;
            NavigationService.Navigate(new Uri("/Views/NewPinPage.xaml", UriKind.Relative));
        }

        // Контекстное меню пина: Удалить
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
                    _boardPins.Remove(pin);
                    if (App.ViewModel.RecentPins != null && App.ViewModel.RecentPins.Contains(pin))
                    {
                        App.ViewModel.RecentPins.Remove(pin);
                    }
                    if (App.ViewModel.UserPins != null && App.ViewModel.UserPins.Contains(pin))
                    {
                        App.ViewModel.UserPins.Remove(pin);
                    }
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
            if (SelectedBoard == null) return;

            var result = MessageBox.Show(
                string.Format("Вы действительно хотите удалить доску \"{0}\"? Пины останутся в вашей ленте, но доска будет удалена.", SelectedBoard.Name),
                "Удаление доски",
                MessageBoxButton.OKCancel);

            if (result != MessageBoxResult.OK) return;

            BoardProgress.IsVisible = true;

            var api = new PinryApiService();
            bool success = await api.DeleteBoardAsync(SelectedBoard.Id);

            BoardProgress.IsVisible = false;

            if (success)
            {
                if (App.ViewModel.Boards != null)
                {
                    var boardInVm = App.ViewModel.Boards.FirstOrDefault(b => b.Id == SelectedBoard.Id);
                    if (boardInVm != null)
                    {
                        App.ViewModel.Boards.Remove(boardInVm);
                    }
                }

                SelectedBoard = null;

                if (NavigationService.CanGoBack)
                {
                    NavigationService.GoBack();
                }
            }
            else
            {
                MessageBox.Show("Не удалось удалить доску. Проверьте права доступа и сеть.", "Ошибка", MessageBoxButton.OK);
            }
        }
    }
}