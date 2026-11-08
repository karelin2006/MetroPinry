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
using MetroPinry.Resources;
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

        private static Board _noneBoardOption;
        private static Board NoneBoardOption
        {
            get
            {
                if (_noneBoardOption == null)
                {
                    _noneBoardOption = new Board { Id = 0, Name = AppResources.NoneOption };
                }
                else
                {
                    _noneBoardOption.Name = AppResources.NoneOption;
                }
                return _noneBoardOption;
            }
        }

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

            UpdateAppBarLocalization();

            if (e.NavigationMode == NavigationMode.Refresh)
            {
                return;
            }

            // Инициализация списка досок
            if (PreselectedBoard != null)
            {
                BoardPicker.ItemsSource = new List<Board> { PreselectedBoard };
                BoardPicker.SelectedItem = PreselectedBoard;
                BoardPicker.IsEnabled = false;

                if (PinToEdit == null && PreselectedBoard.IsPrivate)
                {
                    ChkIsPrivate.IsChecked = true;
                }
            }
            else
            {
                var availableBoards = new List<Board> { NoneBoardOption };
                if (App.ViewModel.Boards != null)
                {
                    availableBoards.AddRange(App.ViewModel.Boards.Where(b => b.IsMine));
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
                    BoardPicker.SelectedIndex = 0;
                }
            }

            // Настройка UI под создание или редактирование
            if (PinToEdit != null)
            {
                PageTitle.Text = AppResources.EditPin;
                TxtDescription.Text = PinToEdit.Description ?? string.Empty;
                ChkIsPrivate.IsChecked = PinToEdit.IsPrivate;

                if (PinToEdit.Tags != null && PinToEdit.Tags.Count > 0)
                {
                    TxtTags.Text = string.Join(", ", PinToEdit.Tags);
                }

                Uri thumbUri = PinToEdit.DisplayThumbnailUrl ?? PinToEdit.DisplayStandardUrl;
                if (thumbUri != null)
                {
                    ImagePreview.Source = new BitmapImage(thumbUri);
                    ImagePreview.Visibility = Visibility.Visible;
                    PlaceholderText.Visibility = Visibility.Collapsed;
                }

                TxtImageUrl.IsEnabled = false;
                BtnPickImage.IsEnabled = false;
                BtnClearImage.Visibility = Visibility.Collapsed;

                // Скрываем выбор доски при редактировании
                BoardSelectionPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                PageTitle.Text = AppResources.AddPin;
                BtnPickImage.IsEnabled = true;
                UpdateImageSourceControls();
                BoardSelectionPanel.Visibility = Visibility.Visible;
            }

            BoardPicker.SelectionChanged += OnBoardPickerSelectionChanged;
            LoadAvailableTags();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            BoardPicker.SelectionChanged -= OnBoardPickerSelectionChanged;

            if (e.NavigationMode == NavigationMode.Back)
            {
                DisposeImageStream();
                PinToEdit = null;
                PreselectedBoard = null;
            }
        }

        private void UpdateAppBarLocalization()
        {
            if (ApplicationBar == null || ApplicationBar.Buttons.Count < 2) return;
            var saveBtn = ApplicationBar.Buttons[0] as ApplicationBarIconButton;
            var cancelBtn = ApplicationBar.Buttons[1] as ApplicationBarIconButton;
            if (saveBtn != null) saveBtn.Text = AppResources.Save;
            if (cancelBtn != null) cancelBtn.Text = AppResources.Cancel;
        }

        private void OnBoardPickerSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PinToEdit != null) return;

            var chosen = BoardPicker.SelectedItem as Board;
            if (chosen != null && chosen.Id > 0 && chosen.IsPrivate)
            {
                ChkIsPrivate.IsChecked = true;
            }
        }

        private void DisposeImageStream()
        {
            if (_selectedImageStream != null)
            {
                _selectedImageStream.Dispose();
                _selectedImageStream = null;
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

        private async void OnPhotoTaskCompleted(object sender, PhotoResult e)
        {
            if (e.TaskResult == TaskResult.OK && e.ChosenPhoto != null)
            {
                try
                {
                    DisposeImageStream();
                    var memoryStream = new MemoryStream();

                    // Защита от зависания "Возобновление"
                    await e.ChosenPhoto.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;
                    _selectedImageStream = memoryStream;

                    var bmp = new BitmapImage();
                    bmp.DecodePixelWidth = 400; // Облегчаем UI
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
            DisposeImageStream();
            ImagePreview.Source = null;
            ImagePreview.Visibility = Visibility.Collapsed;
            PlaceholderText.Visibility = Visibility.Visible;
            UpdateImageSourceControls();
        }

        private void OnImageUrlChanged(object sender, TextChangedEventArgs e)
        {
            if (PinToEdit != null) return;

            string url = TxtImageUrl.Text != null ? TxtImageUrl.Text.Trim() : string.Empty;
            if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                DisposeImageStream();
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
            string url = TxtImageUrl.Text != null ? TxtImageUrl.Text.Trim() : string.Empty;
            string desc = TxtDescription.Text != null ? TxtDescription.Text.Trim() : string.Empty;
            string rawTags = TxtTags.Text != null ? TxtTags.Text.Trim() : string.Empty;
            bool isPrivate = ChkIsPrivate.IsChecked ?? false;

            if (PinToEdit == null && string.IsNullOrEmpty(url) && _selectedImageStream == null)
            {
                MessageBox.Show(AppResources.SpecifyImagePrompt, AppResources.Warning, MessageBoxButton.OK);
                return;
            }

            var selectedBoard = PreselectedBoard ?? (BoardPicker.SelectedItem as Board);
            int? targetBoardId = (selectedBoard != null && selectedBoard.Id > 0) ? selectedBoard.Id : (int?)null;

            // Если выбрана приватная доска, пин тоже делаем приватным
            if (selectedBoard != null && selectedBoard.Id > 0 && selectedBoard.IsPrivate)
            {
                isPrivate = true;
            }

            var tagsList = new List<string>();
            if (!string.IsNullOrEmpty(rawTags))
            {
                char[] separators = new char[] { ',', ';', ' ' };
                var split = rawTags.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                foreach (var tag in split)
                {
                    string cleaned = tag.Trim().TrimStart('#').Trim();
                    if (!string.IsNullOrEmpty(cleaned) && !tagsList.Contains(cleaned))
                    {
                        tagsList.Add(cleaned);
                    }
                }
            }

            SetLoadingState(true);

            bool success = false;
            var apiService = new PinryApiService();

            if (PinToEdit != null)
            {
                success = await apiService.UpdatePinAsync(
                    pinId: PinToEdit.Id,
                    desc: desc,
                    tags: tagsList,
                    isPrivate: isPrivate);

                if (success)
                {
                    PinToEdit.Description = desc;
                    PinToEdit.Tags = tagsList;
                    PinToEdit.IsPrivate = isPrivate;
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(url))
                {
                    success = await apiService.CreatePinAsync(
                        url: url,
                        imageId: null,
                        desc: desc,
                        boardsList: null,
                        tagsList: tagsList,
                        isPrivate: isPrivate,
                        targetBoardId: targetBoardId);
                }
                else if (_selectedImageStream != null)
                {
                    if (_selectedImageStream.CanSeek)
                    {
                        _selectedImageStream.Position = 0;
                    }
                    success = await apiService.UploadPinImageAsync(
                        imageStream: _selectedImageStream,
                        desc: desc,
                        boardsList: null,
                        tagsList: tagsList,
                        isPrivate: isPrivate,
                        targetBoardId: targetBoardId);
                }
            }

            SetLoadingState(false);

            if (success)
            {
                DisposeImageStream();
                PinToEdit = null;
                PreselectedBoard = null;
                App.ViewModel.IsDataLoaded = false;
                await App.ViewModel.LoadDataAsync();

                Dispatcher.BeginInvoke(() =>
                {
                    if (NavigationService.CanGoBack)
                    {
                        NavigationService.GoBack();
                    }
                });
            }
            else
            {
                MessageBox.Show(AppResources.PinSaveError, AppResources.Error, MessageBoxButton.OK);
            }
        }

        private void SetLoadingState(bool isLoading)
        {
            var indicator = SystemTray.ProgressIndicator;
            if (indicator != null)
            {
                indicator.IsVisible = isLoading;
                indicator.Text = isLoading ? AppResources.Saving : AppResources.AppTitle;
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
            BtnCreateBoard.IsEnabled = !isLoading;
        }

        private void OnCancelAppBarClick(object sender, EventArgs e)
        {
            DisposeImageStream();
            PinToEdit = null;
            PreselectedBoard = null;
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }

        private void OnCreateNewBoardClick(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new Uri("/Views/NewBoardPage.xaml", UriKind.Relative));
        }
    }
}