using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using MetroPinry.Models;
using MetroPinry.Services;

namespace MetroPinry.Views
{
    public partial class SelectBoardPage : PhoneApplicationPage
    {
        // Входные параметры
        public static Pin TargetPin { get; set; }
        public static bool IsAttachMode { get; set; } // true = прикрепить, false = открепить

        public SelectBoardPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (TargetPin == null || App.ViewModel.Boards == null)
            {
                NavigationService.GoBack();
                return;
            }

            PageHeader.Text = IsAttachMode ? "attach to" : "detach from";

            List<Board> boardsToShow;

            if (IsAttachMode)
            {
                // Доски, к которым пин еще НЕ привязан
                boardsToShow = App.ViewModel.Boards
                    .Where(b => !TargetPin.BelongsToBoard(b.Id, b.Name))
                    .ToList();
            }
            else
            {
                // Доски, к которым пин УЖЕ привязан
                boardsToShow = App.ViewModel.Boards
                    .Where(b => TargetPin.BelongsToBoard(b.Id, b.Name))
                    .ToList();
            }

            BoardsList.ItemsSource = boardsToShow;
        }

        private async void OnBoardSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedBoard = BoardsList.SelectedItem as Board;
            if (selectedBoard == null || TargetPin == null) return;

            BoardsList.SelectedItem = null;
            BoardsList.IsEnabled = false;
            PageProgress.Text = IsAttachMode ? "прикрепление..." : "открепление...";
            PageProgress.IsVisible = true;

            var api = new PinryApiService();
            bool success = false;

            if (IsAttachMode)
            {
                success = await api.AddPinToBoardAsync(selectedBoard.Id, TargetPin.Id);
                if (success)
                {
                    if (TargetPin.Boards == null)
                    {
                        TargetPin.Boards = new List<object>();
                    }
                    if (!TargetPin.Boards.Contains(selectedBoard.Id))
                    {
                        TargetPin.Boards.Add(selectedBoard.Id);
                    }
                }
            }
            else
            {
                success = await api.RemovePinFromBoardAsync(selectedBoard.Id, TargetPin.Id);
                if (success)
                {
                    if (TargetPin.Boards != null)
                    {
                        TargetPin.Boards.Remove(selectedBoard.Id);
                        TargetPin.Boards.Remove(selectedBoard.Id.ToString());
                        TargetPin.Boards.RemoveAll(item => item != null && item.ToString().Contains("\"id\":" + selectedBoard.Id));
                    }
                }
            }

            PageProgress.IsVisible = false;

            if (success)
            {
                await App.ViewModel.LoadDataAsync();
                NavigationService.GoBack();
            }
            else
            {
                BoardsList.IsEnabled = true;
                System.Windows.MessageBox.Show("Не удалось выполнить операцию. Проверьте права и сеть.", "Ошибка", System.Windows.MessageBoxButton.OK);
            }
        }
    }
}