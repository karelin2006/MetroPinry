using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using MetroPinry.Models;
using MetroPinry.Services;

namespace MetroPinry.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly PinryApiService _apiService;

        public MainViewModel(PinryApiService apiService)
        {
            _apiService = apiService;
            RecentPins = new ObservableCollection<Pin>();
            Boards = new ObservableCollection<Board>();
            UserPins = new ObservableCollection<Pin>();
        }

        public ObservableCollection<Pin> RecentPins { get; private set; }
        public ObservableCollection<Board> Boards { get; private set; }
        public ObservableCollection<Pin> UserPins { get; private set; }

        private UserProfile _currentUser;
        public UserProfile CurrentUser
        {
            get { return _currentUser; }
            set
            {
                if (_currentUser != value)
                {
                    _currentUser = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get { return _isBusy; }
            set
            {
                if (_isBusy != value)
                {
                    _isBusy = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private bool _isDataLoaded;
        public bool IsDataLoaded
        {
            get { return _isDataLoaded; }
            set
            {
                if (_isDataLoaded != value)
                {
                    _isDataLoaded = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public async Task LoadDataAsync()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                // 1. Сетевые запросы выполняются параллельно в фоне
                var userTask = _apiService.GetCurrentUserAsync();
                var pinsTask = _apiService.GetRecentPinsAsync();
                var boardsTask = _apiService.GetBoardsAsync();

                await Task.WhenAll(userTask, pinsTask, boardsTask);

                var user = await userTask;
                var pinsResp = await pinsTask;
                var boardsResp = await boardsTask;

                // 2. ВСЕ изменения ObservableCollection и UI-свойств — строго в UI-потоке через Dispatcher!
                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    if (user != null)
                    {
                        CurrentUser = user;
                    }

                    RecentPins.Clear();
                    UserPins.Clear();

                    if (pinsResp != null && pinsResp.Results != null)
                    {
                        foreach (var pin in pinsResp.Results)
                        {
                            RecentPins.Add(pin);

                            if (CurrentUser != null &&
                                pin.Submitter != null &&
                                string.Equals(pin.Submitter.Username, CurrentUser.Username, StringComparison.OrdinalIgnoreCase))
                            {
                                UserPins.Add(pin);
                            }
                        }
                    }

                    Boards.Clear();
                    if (boardsResp != null && boardsResp.Results != null)
                    {
                        foreach (var board in boardsResp.Results)
                        {
                            Boards.Add(board);
                        }
                    }

                    IsDataLoaded = true;
                    IsBusy = false;
                });
            }
            catch (Exception ex)
            {
                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    IsBusy = false;
                    MessageBox.Show("Не удалось загрузить данные с сервера: " + ex.Message, "Ошибка сети", MessageBoxButton.OK);
                });
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}