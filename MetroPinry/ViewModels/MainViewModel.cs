using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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

        // Внутренний буфер предзагруженных пинов
        private readonly List<Pin> _pinsPool = new List<Pin>();
        private const int InitialUiCount = 12; // Стартовая пачка для стабильного скролла
        private const int ChunkSize = 6;      // Шаг досыпки при прокрутке

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
        public ObservableCollection<Board> UserBoardsWithAll { get; private set; } = new ObservableCollection<Board>();

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

        public string NextPinsUrl { get; set; }
        public bool IsLoadingMore { get; set; }

        public async Task LoadDataAsync()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                var userTask = _apiService.GetCurrentUserAsync();
                var pinsTask = _apiService.GetRecentPinsAsync(); // Запрашивает 30-40 пинов
                var boardsTask = _apiService.GetBoardsAsync();

                await Task.WhenAll(userTask, pinsTask, boardsTask);

                var user = await userTask;
                var pinsResp = await pinsTask;
                var boardsResp = await boardsTask;

                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    if (user != null)
                    {
                        CurrentUser = user;
                    }

                    RecentPins.Clear();
                    UserPins.Clear();
                    _pinsPool.Clear();

                    if (pinsResp != null && pinsResp.Results != null)
                    {
                        NextPinsUrl = pinsResp.Next;

                        // 1. Наполняем внутренний пул сервера
                        _pinsPool.AddRange(pinsResp.Results);

                        // 2. Выкладываем ровно InitialUiCount (12), чтобы активировать виртуализацию и скролл
                        var firstChunk = _pinsPool.Take(InitialUiCount).ToList();
                        foreach (var pin in firstChunk)
                        {
                            RecentPins.Add(pin);
                            CheckAndAddUserPin(pin);
                        }

                        System.Diagnostics.Debug.WriteLine(string.Format("UI INITIAL LOAD: {0} pins. POOL TOTAL: {1}", RecentPins.Count, _pinsPool.Count));
                    }

                    Boards.Clear();
                    if (boardsResp != null && boardsResp.Results != null)
                    {
                        foreach (var board in boardsResp.Results)
                        {
                            Boards.Add(board);
                        }
                    }

                    UpdateUserBoardsWithAll();

                    IsDataLoaded = true;
                    IsBusy = false;
                });
            }
            catch (Exception ex)
            {
                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    IsBusy = false;
                    MessageBox.Show("Не удалось загрузить данные: " + ex.Message, "Ошибка сети", MessageBoxButton.OK);
                });
            }
        }

        public async Task LoadMorePinsAsync()
        {
            if (IsLoadingMore) return;
            IsLoadingMore = true;

            try
            {
                // ЭТАП 1: Досыпка из локального кэша в памяти
                int currentCount = RecentPins.Count;
                if (currentCount < _pinsPool.Count)
                {
                    var nextChunk = _pinsPool.Skip(currentCount).Take(ChunkSize).ToList();

                    Deployment.Current.Dispatcher.BeginInvoke(() =>
                    {
                        foreach (var pin in nextChunk)
                        {
                            if (!RecentPins.Any(p => p.Id == pin.Id))
                            {
                                RecentPins.Add(pin);
                                CheckAndAddUserPin(pin);
                            }
                        }
                        System.Diagnostics.Debug.WriteLine(string.Format("POURED FROM LOCAL POOL: +{0}. UI Visible: {1}/{2}", nextChunk.Count, RecentPins.Count, _pinsPool.Count));
                        IsLoadingMore = false;
                    });

                    return;
                }

                // ЭТАП 2: Локальный пул исчерпан — запрашиваем следующую страницу у Pinry API
                if (!string.IsNullOrEmpty(NextPinsUrl))
                {
                    System.Diagnostics.Debug.WriteLine(">>> REQUESTING SERVER FOR NEXT CHUNK: " + NextPinsUrl);
                    var api = new PinryApiService();
                    var data = await api.GetPinsPageAsync(NextPinsUrl);

                    if (data != null && data.Results != null && data.Results.Count > 0)
                    {
                        NextPinsUrl = data.Next;

                        // Дополняем пул новыми пинами
                        foreach (var pin in data.Results)
                        {
                            if (!_pinsPool.Any(p => p.Id == pin.Id))
                            {
                                _pinsPool.Add(pin);
                            }
                        }

                        var newChunk = data.Results.Take(ChunkSize).ToList();
                        Deployment.Current.Dispatcher.BeginInvoke(() =>
                        {
                            foreach (var pin in newChunk)
                            {
                                if (!RecentPins.Any(p => p.Id == pin.Id))
                                {
                                    RecentPins.Add(pin);
                                    CheckAndAddUserPin(pin);
                                }
                            }
                            System.Diagnostics.Debug.WriteLine(string.Format("SERVER CHUNK LOADED: +{0}. UI Total: {1}", newChunk.Count, RecentPins.Count));
                            IsLoadingMore = false;
                        });
                    }
                    else
                    {
                        NextPinsUrl = null;
                        IsLoadingMore = false;
                    }
                }
                else
                {
                    IsLoadingMore = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("LOAD MORE EXCEPTION: " + ex.Message);
                IsLoadingMore = false;
            }
        }

        private void CheckAndAddUserPin(Pin pin)
        {
            if (CurrentUser != null && pin.Submitter != null &&
                string.Equals(pin.Submitter.Username, CurrentUser.Username, StringComparison.OrdinalIgnoreCase))
            {
                if (!UserPins.Any(p => p.Id == pin.Id))
                {
                    UserPins.Add(pin);
                }
            }
        }

        public void ResetSessionState()
        {
            IsDataLoaded = false;
            CurrentUser = null;
            NextPinsUrl = null;
            IsLoadingMore = false;
            _pinsPool.Clear();

            if (Deployment.Current.Dispatcher.CheckAccess())
            {
                RecentPins.Clear();
                UserPins.Clear();
                Boards.Clear();
            }
            else
            {
                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    RecentPins.Clear();
                    UserPins.Clear();
                    Boards.Clear();
                });
            }
        }

        public void UpdateUserBoardsWithAll()
        {
            UserBoardsWithAll.Clear();

            var latestPin = UserPins?.FirstOrDefault();

            // Виртуальная доска "all"
            var allBoard = new Board
            {
                Id = -1,
                Name = "all",
                PinsCount = UserPins != null ? UserPins.Count : 0,
                IsPrivate = false,
                Cover = latestPin // Передаем пин напрямую в качестве обложки доски
            };

            UserBoardsWithAll.Add(allBoard);

            // Добавляем только свои реальные доски
            if (Boards != null)
            {
                foreach (var b in Boards.Where(b => b.IsMine))
                {
                    UserBoardsWithAll.Add(b);
                }
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