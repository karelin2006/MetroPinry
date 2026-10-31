using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Tasks;
using MetroPinry.Models;
using MetroPinry.Services;

namespace MetroPinry.Views
{
    public partial class NewPinPage : PhoneApplicationPage
    {
        public static Pin PinToEdit { get; set; }
        public static Board PreselectedBoard { get; set; }

        private readonly PhotoChooserTask _photoTask;
        private Stream _selectedImageStream;
        private List<string> _allAvailableTags = new List<string>();
        private bool _isUpdatingTagsText = false;

        // Фиктивный элемент для выбора "без доски"
        private static readonly Board NoneBoardOption = new Board { Id = 0, Name = "(none)" };

        public NewPinPage()
        {
            InitializeComponent();

            _photoTask = new PhotoChooserTask();
            _photoTask.ShowCamera = true;
            _photoTask.Completed += OnPhotoTaskCompleted;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Если страница возобновляется после выбора фото из системной галереи — пропускаем
            if (e.NavigationMode == NavigationMode.Refresh)
            {
                return;
            }

            // 1. Инициализация списка досок
            if (PreselectedBoard != null)
            {
                // Принудительная привязка к конкретной доске при создании с экрана доски
                BoardPicker.ItemsSource = new List<Board> { PreselectedBoard };
                BoardPicker.SelectedItem = PreselectedBoard;
                BoardPicker.IsEnabled = false;
            }
            else
            {
                // Собираем список: первым идет пункт "(none)", затем остальные доски
                var availableBoards = new List<Board> { NoneBoardOption };
                if (App.ViewModel.Boards != null && App.ViewModel.Boards.Count > 0)
                {
                    availableBoards.AddRange(App.ViewModel.Boards);
                }

                BoardPicker.ItemsSource = availableBoards;
                BoardPicker.IsEnabled = true;

                if (!string.IsNullOrEmpty(NewBoardPage.NewlyCreatedBoardName))
                {
                    var targetBoard = availableBoards.FirstOrDefault(b => b.Name == NewBoardPage.NewlyCreatedBoardName);
                    BoardPicker.SelectedItem = targetBoard ?? NoneBoardOption;
                    NewBoardPage.NewlyCreatedBoardName = null;
                }
                else if (BoardPicker.SelectedItem == null)
                {
                    BoardPicker.SelectedIndex = 0; // По умолчанию выбран (none)
                }
            }

            // 2. Настройка UI под создание или редактирование
            if (PinToEdit != null)
            {
                PageTitle.Text = "edit pin";

                TxtDescription.Text = PinToEdit.Description ?? string.Empty;
                ChkIsPrivate.IsChecked = PinToEdit.IsPrivate;

                if (PinToEdit.Tags != null && PinToEdit.Tags.Count > 0)
                {
                    TxtTags.Text = string.Join(", ", PinToEdit.Tags);
                }

                if (PinToEdit.DisplayThumbnailUrl != null)
                {
                    ImagePreview.Source = new BitmapImage(PinToEdit.DisplayThumbnailUrl);
                    ImagePreview.Visibility = Visibility.Visible;
                    PlaceholderText.Visibility = Visibility.Collapsed;
                }

                TxtImageUrl.IsEnabled = false;
                BtnPickImage.IsEnabled = false;
                BtnClearImage.Visibility = Visibility.Collapsed;

                // Проверяем принадлежность доске среди существующих
                bool foundBoard = false;
                if (App.ViewModel.Boards != null)
                {
                    foreach (var b in App.ViewModel.Boards)
                    {
                        if (PinToEdit.BelongsToBoard(b.Id, b.Name))
                        {
                            BoardPicker.SelectedItem = b;
                            foundBoard = true;
                            break;
                        }
                    }
                }

                if (!foundBoard && PreselectedBoard == null)
                {
                    BoardPicker.SelectedItem = NoneBoardOption;
                }
            }
            else
            {
                PageTitle.Text = "add pin";
                BtnPickImage.IsEnabled = true;
                UpdateImageSourceControls();
            }

            // 3. Загрузка базы подсказок тегов
            LoadAvailableTags();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            if (e.NavigationMode == NavigationMode.Back)
            {
                PinToEdit = null;
                PreselectedBoard = null;
            }
        }

        private async void LoadAvailableTags()
        {
            try
            {
                var api = new PinryApiService();
                var resp = await api.GetRecentPinsAsync();

                if (resp != null && resp.Results != null && resp.Results.Count > 0)
                {
                    _allAvailableTags = resp.Results
                        .Where(p => p.Tags != null)
                        .SelectMany(p => p.Tags)
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .Select(t => t.Trim().TrimStart('#'))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(t => t)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("LOAD TAGS ERROR: " + ex.Message);
            }
        }

        private void OnTagsTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingTagsText) return;

            string text = TxtTags.Text ?? string.Empty;

            string currentWord;
            int lastComma = text.LastIndexOf(',');
            if (lastComma >= 0)
            {
                currentWord = text.Substring(lastComma + 1).TrimStart();
            }
            else
            {
                currentWord = text.TrimStart('#');
            }

            if (!string.IsNullOrWhiteSpace(currentWord) && currentWord.Length >= 2)
            {
                var matches = _allAvailableTags
                    .Where(t => t.IndexOf(currentWord, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Take(8)
                    .ToList();

                if (matches.Count > 0)
                {
                    SuggestedTagsList.ItemsSource = matches;
                    TagsPopupBorder.Visibility = Visibility.Visible;
                    return;
                }
            }

            TagsPopupBorder.Visibility = Visibility.Collapsed;
            SuggestedTagsList.ItemsSource = null;
        }

        private void OnSuggestedTagTap(object sender, SelectionChangedEventArgs e)
        {
            if (SuggestedTagsList.SelectedItem == null) return;

            string selectedTag = SuggestedTagsList.SelectedItem.ToString();
            string text = TxtTags.Text ?? string.Empty;

            _isUpdatingTagsText = true;

            int lastComma = text.LastIndexOf(',');
            if (lastComma >= 0)
            {
                string prefix = text.Substring(0, lastComma + 1).TrimEnd();
                TxtTags.Text = prefix + " " + selectedTag + ", ";
            }
            else
            {
                TxtTags.Text = selectedTag + ", ";
            }

            TxtTags.SelectionStart = TxtTags.Text.Length;

            _isUpdatingTagsText = false;

            TagsPopupBorder.Visibility = Visibility.Collapsed;
            SuggestedTagsList.SelectedItem = null;
        }

        private void OnPickImageClick(object sender, RoutedEventArgs e)
        {
            _photoTask.Show();
        }

        private void OnPhotoTaskCompleted(object sender, PhotoResult e)
        {
            if (e.TaskResult == TaskResult.OK && e.ChosenPhoto != null)
            {
                try
                {
                    if (_selectedImageStream != null)
                    {
                        _selectedImageStream.Dispose();
                        _selectedImageStream = null;
                    }

                    var memoryStream = new MemoryStream();
                    e.ChosenPhoto.CopyTo(memoryStream);
                    memoryStream.Position = 0;

                    _selectedImageStream = memoryStream;

                    var bmp = new BitmapImage();
                    bmp.SetSource(_selectedImageStream);

                    ImagePreview.Source = bmp;
                    ImagePreview.Visibility = Visibility.Visible;
                    PlaceholderText.Visibility = Visibility.Collapsed;

                    TxtImageUrl.Text = string.Empty;
                    UpdateImageSourceControls();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("PHOTO CHOOSER ERROR: " + ex.Message);
                }
            }
        }

        private void OnClearImageClick(object sender, RoutedEventArgs e)
        {
            if (_selectedImageStream != null)
            {
                _selectedImageStream.Dispose();
                _selectedImageStream = null;
            }

            ImagePreview.Source = null;
            ImagePreview.Visibility = Visibility.Collapsed;
            PlaceholderText.Visibility = Visibility.Visible;

            UpdateImageSourceControls();
        }

        private void OnImageUrlChanged(object sender, TextChangedEventArgs e)
        {
            if (PinToEdit != null) return;

            string url = TxtImageUrl.Text.Trim();
            if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                if (_selectedImageStream != null)
                {
                    _selectedImageStream.Dispose();
                    _selectedImageStream = null;
                }
                ImagePreview.Source = new BitmapImage(new Uri(url, UriKind.Absolute));
                ImagePreview.Visibility = Visibility.Visible;
                PlaceholderText.Visibility = Visibility.Collapsed;
            }
            else if (string.IsNullOrEmpty(url) && _selectedImageStream == null)
            {
                ImagePreview.Source = null;
                ImagePreview.Visibility = Visibility.Collapsed;
                PlaceholderText.Visibility = Visibility.Visible;
            }

            UpdateImageSourceControls();
        }

        private void UpdateImageSourceControls()
        {
            if (PinToEdit != null) return;

            bool hasLocalFile = _selectedImageStream != null;

            TxtImageUrl.IsEnabled = !hasLocalFile;
            BtnClearImage.Visibility = hasLocalFile ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void OnSaveAppBarClick(object sender, EventArgs e)
        {
            string url = TxtImageUrl.Text.Trim();
            string desc = TxtDescription.Text.Trim();
            string rawTags = TxtTags.Text != null ? TxtTags.Text.Trim() : string.Empty;
            bool isPrivate = ChkIsPrivate.IsChecked ?? false;

            if (PinToEdit == null && string.IsNullOrEmpty(url) && _selectedImageStream == null)
            {
                MessageBox.Show("Укажите ссылку на изображение или выберите фото из галереи.", "Предупреждение", MessageBoxButton.OK);
                return;
            }

            // Определение выбранной доски (может быть (none) или null)
            var selectedBoard = PreselectedBoard ?? (BoardPicker.SelectedItem as Board);

            var boardsList = new List<string>();
            int? targetBoardId = null;

            if (selectedBoard != null && selectedBoard.Id > 0 && !string.IsNullOrEmpty(selectedBoard.Name))
            {
                boardsList.Add(selectedBoard.Name);
                targetBoardId = selectedBoard.Id;
            }

            var tagsList = new List<string>();
            if (!string.IsNullOrEmpty(rawTags))
            {
                if (rawTags.Contains(",") || rawTags.Contains(";"))
                {
                    var split = rawTags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var tag in split)
                    {
                        string cleaned = tag.Trim().TrimStart('#').Trim();
                        if (!string.IsNullOrEmpty(cleaned) && !tagsList.Contains(cleaned))
                        {
                            tagsList.Add(cleaned);
                        }
                    }
                }
                else if (rawTags.Contains("#"))
                {
                    var split = rawTags.Split(new[] { '#' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var tag in split)
                    {
                        string cleaned = tag.Trim();
                        if (!string.IsNullOrEmpty(cleaned) && !tagsList.Contains(cleaned))
                        {
                            tagsList.Add(cleaned);
                        }
                    }
                }
                else
                {
                    string cleaned = rawTags.Trim();
                    if (!string.IsNullOrEmpty(cleaned))
                    {
                        tagsList.Add(cleaned);
                    }
                }
            }

            SetLoadingState(true);

            bool success = false;
            var apiService = new PinryApiService();

            // 1. Редактирование существующего пина
            if (PinToEdit != null)
            {
                success = await apiService.UpdatePinAsync(
                    PinToEdit.Id,
                    desc,
                    tagsList,
                    isPrivate,
                    targetBoardId: targetBoardId);

                if (success)
                {
                    PinToEdit.Description = desc;
                    PinToEdit.Tags = tagsList;
                    PinToEdit.IsPrivate = isPrivate;

                    if (targetBoardId.HasValue && targetBoardId.Value > 0)
                    {
                        if (PinToEdit.Boards == null)
                        {
                            PinToEdit.Boards = new List<object>();
                        }
                        if (!PinToEdit.Boards.Contains(targetBoardId.Value))
                        {
                            PinToEdit.Boards.Add(targetBoardId.Value);
                        }
                    }
                }
            }
            // 2. Создание нового пина
            else
            {
                if (!string.IsNullOrEmpty(url))
                {
                    success = await apiService.CreatePinFromUrlAsync(
                        url,
                        desc,
                        boardsList,
                        tagsList,
                        isPrivate,
                        targetBoardId: targetBoardId);
                }
                else if (_selectedImageStream != null)
                {
                    if (_selectedImageStream.CanSeek)
                    {
                        _selectedImageStream.Position = 0;
                    }
                    success = await apiService.UploadPinImageAsync(
                        _selectedImageStream,
                        desc,
                        boardsList,
                        tagsList,
                        isPrivate,
                        targetBoardId: targetBoardId);
                }
            }

            SetLoadingState(false);

            if (success)
            {
                PinToEdit = null;
                PreselectedBoard = null;
                await App.ViewModel.LoadDataAsync();
                NavigationService.GoBack();
            }
            else
            {
                MessageBox.Show("Не удалось сохранить пин. Проверьте подключение к серверу и права доступа.", "Ошибка", MessageBoxButton.OK);
            }
        }

        private void SetLoadingState(bool isLoading)
        {
            var progress = Resources.Contains("ProgressIndicator") ? (ProgressIndicator)Resources["ProgressIndicator"] : null;
            if (progress != null)
            {
                SystemTray.SetProgressIndicator(this, progress);
                progress.IsVisible = isLoading;
            }

            if (ApplicationBar != null)
            {
                foreach (var button in ApplicationBar.Buttons.OfType<ApplicationBarIconButton>())
                {
                    button.IsEnabled = !isLoading;
                }
            }

            TxtImageUrl.IsEnabled = !isLoading && PinToEdit == null && _selectedImageStream == null;
            TxtDescription.IsEnabled = !isLoading;
            TxtTags.IsEnabled = !isLoading;
            BoardPicker.IsEnabled = !isLoading && PreselectedBoard == null;
            BtnPickImage.IsEnabled = !isLoading && PinToEdit == null;
            BtnClearImage.IsEnabled = !isLoading;
            ChkIsPrivate.IsEnabled = !isLoading;
        }

        private void OnCancelAppBarClick(object sender, EventArgs e)
        {
            PinToEdit = null;
            PreselectedBoard = null;
            NavigationService.GoBack();
        }

        private void OnCreateNewBoardClick(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new Uri("/Views/NewBoardPage.xaml", UriKind.Relative));
        }
    }
}