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
        }

        /// <summary>
        /// Коллекция свежих пинов для первой секции панорамы ("recent").
        /// </summary>
        public ObservableCollection<Pin> RecentPins { get; private set; }

        /// <summary>
        /// Коллекция досок для второй секции панорамы ("boards").
        /// </summary>
        public ObservableCollection<Board> Boards { get; private set; }

        private UserProfile _currentUser;
        /// <summary>
        /// Данные текущего профиля для третьей секции ("me").
        /// </summary>
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
        /// <summary>
        /// Флаг выполнения сетевых операций (для индикатора загрузки).
        /// </summary>
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

        public bool IsDataLoaded { get; private set; }

        /// <summary>
        /// Загрузка данных для всех секций панорамы.
        /// </summary>
        public async Task LoadDataAsync()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                // Запускаем запросы параллельно (без await на каждой строке)
                var userTask = _apiService.GetCurrentUserAsync();
                var pinsTask = _apiService.GetRecentPinsAsync(); // Изменили response на pinsTask и убрали await
                var boardsTask = _apiService.GetBoardsAsync();

                await Task.WhenAll(userTask, pinsTask, boardsTask);

                // Заполняем данные пользователя
                if (userTask.Result != null)
                {
                    CurrentUser = userTask.Result;
                }

                // Заполняем свежие пины
                RecentPins.Clear();
                if (pinsTask.Result != null && pinsTask.Result.Results != null)
                {
                    foreach (var pin in pinsTask.Result.Results)
                    {
                        RecentPins.Add(pin);
                    }
                }

                // Заполняем список досок
                Boards.Clear();
                if (boardsTask.Result != null && boardsTask.Result.Results != null)
                {
                    foreach (var board in boardsTask.Result.Results)
                    {
                        Boards.Add(board);
                    }
                }

                IsDataLoaded = true;
            }
            catch (Exception ex)
            {
                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    MessageBox.Show("Не удалось загрузить данные с сервера: " + ex.Message, "Ошибка сети", MessageBoxButton.OK);
                });
            }
            finally
            {
                IsBusy = false;
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