using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using MetroPinry.Models;
using MetroPinry.Services;

namespace MetroPinry.Views
{
    public partial class BoardDetailsPage : PhoneApplicationPage
    {
        public static Board SelectedBoard { get; set; }
        private readonly PinryApiService _apiService;
        public ObservableCollection<Pin> BoardPins { get; set; }

        public BoardDetailsPage()
        {
            InitializeComponent();
            _apiService = new PinryApiService();
            BoardPins = new ObservableCollection<Pin>();
            BoardPinsList.ItemsSource = BoardPins;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (SelectedBoard != null)
            {
                BoardTitle.Text = SelectedBoard.Name.ToLower();

                var response = await _apiService.GetBoardPinsAsync(SelectedBoard.Id);
                if (response != null && response.Results != null)
                {
                    BoardPins.Clear();
                    foreach (var pin in response.Results)
                    {
                        BoardPins.Add(pin);
                    }
                }
            }
        }

        private void OnPinSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selector = sender as LongListSelector;
            if (selector == null || selector.SelectedItem == null) return;

            PinDetailPage.SelectedPin = selector.SelectedItem as Pin;
            selector.SelectedItem = null; // сбрасываем подсветку элемента

            NavigationService.Navigate(new Uri("/Views/PinDetailPage.xaml", UriKind.Relative));
        }
    }
}