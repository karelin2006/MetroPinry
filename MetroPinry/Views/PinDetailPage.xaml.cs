using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Xna.Framework.Media;
using MetroPinry.Models;

namespace MetroPinry.Views
{
    public partial class PinDetailPage : PhoneApplicationPage
    {
        public static Pin SelectedPin { get; set; }

        public PinDetailPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (SelectedPin == null) return;

            PinDescription.Text = !string.IsNullOrWhiteSpace(SelectedPin.Description)
                ? SelectedPin.Description
                : "без описания";

            TxtPinId.Text = "#" + SelectedPin.Id;
            TxtAuthor.Text = !string.IsNullOrEmpty(SelectedPin.Submitter?.Username)
                ? SelectedPin.Submitter.Username
                : "goose";

            TxtSourceUrl.Text = SelectedPin.DisplayStandardUrl != null
                ? SelectedPin.DisplayStandardUrl.ToString()
                : "локальный файл";

            // Для отображения на экране используем оптимизированный standard JPEG
            if (SelectedPin.DisplayStandardUrl != null)
            {
                FullImage.Source = new BitmapImage(SelectedPin.DisplayStandardUrl)
                {
                    CreateOptions = BitmapCreateOptions.BackgroundCreation
                };
            }
        }

        private async void OnDownloadAppBarClick(object sender, EventArgs e)
        {
            // Берем оригинальный URL, если нет — резервно standard
            Uri targetUri = SelectedPin.DisplayOriginalUrl ?? SelectedPin.DisplayStandardUrl;

            if (targetUri == null)
            {
                MessageBox.Show("Ссылка на изображение недоступна.", "Ошибка", MessageBoxButton.OK);
                return;
            }

            var progressIndicator = new ProgressIndicator
            {
                IsVisible = true,
                IsIndeterminate = true,
                Text = "Скачивание оригинала..."
            };
            SystemTray.SetProgressIndicator(this, progressIndicator);

            try
            {
                var request = (HttpWebRequest)WebRequest.Create(targetUri);
                request.Method = "GET";
                request.Headers["User-Agent"] = "MetroPinry/1.0 (Windows Phone 8.1)";

                using (var response = (HttpWebResponse)await Task.Factory.FromAsync<WebResponse>(request.BeginGetResponse, request.EndGetResponse, null))
                using (var stream = response.GetResponseStream())
                using (var ms = new MemoryStream())
                {
                    await stream.CopyToAsync(ms);
                    ms.Position = 0;

                    // Имя файла для медиатеки
                    string fileName = string.Format("pin_{0}_{1}.png", SelectedPin.Id, DateTime.Now.Ticks);

                    // Сохраняем в системный альбом «Saved Pictures»
                    var library = new MediaLibrary();
                    library.SavePicture(fileName, ms);

                    MessageBox.Show("Оригинальное изображение сохранено в галерею (Saved Pictures).", "Успешно", MessageBoxButton.OK);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось сохранить изображение: " + ex.Message, "Ошибка", MessageBoxButton.OK);
            }
            finally
            {
                progressIndicator.IsVisible = false;
                SystemTray.SetProgressIndicator(this, null);
            }
        }
    }
}