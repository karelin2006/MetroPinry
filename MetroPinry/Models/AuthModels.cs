using System;
using System.IO;
using System.IO.IsolatedStorage;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;

namespace MetroPinry.Models
{
    public class AuthTokenResponse
    {
        [JsonProperty("token")]
        public string Token { get; set; }
    }

    public class PinryAccount
    {
        // Убираем жесткую привязку к snake_case, чтобы поддерживать оба формата сериализации
        public string ServerUrl { get; set; }

        private string _username;
        public string Username
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_username) || _username.Equals("name", StringComparison.OrdinalIgnoreCase))
                {
                    return "user";
                }
                return _username;
            }
            set { _username = value; }
        }

        public string Token { get; set; }

        public string GravatarHash { get; set; }

        // Дополнительные алиасы для десериализации, если в JSON ключи пришли из бэкенда
        [JsonProperty("server_url")]
        private string ServerUrlAlias { set { if (string.IsNullOrEmpty(ServerUrl)) ServerUrl = value; } }

        [JsonProperty("gravatar")]
        private string GravatarHashAlias { set { if (string.IsNullOrEmpty(GravatarHash)) GravatarHash = value; } }

        [JsonIgnore]
        public Uri AvatarUrl
        {
            get
            {
                string hash = !string.IsNullOrWhiteSpace(GravatarHash)
                    ? GravatarHash.Trim().ToLowerInvariant()
                    : "00000000000000000000000000000000";

                // Для Windows Phone 8.1 HTTP работает надежнее из-за устаревших SSL-сертификатов Gravatar
                string url = string.Format("http://www.gravatar.com/avatar/{0}?s=144&d=identicon", hash);
                return new Uri(url, UriKind.Absolute);
            }
        }

        [JsonIgnore]
        public BitmapImage LocalAvatar
        {
            get
            {
                var bmp = new BitmapImage();
                bmp.CreateOptions = BitmapCreateOptions.BackgroundCreation;

                try
                {
                    using (var isoStore = IsolatedStorageFile.GetUserStoreForApplication())
                    {
                        string safeName = string.IsNullOrWhiteSpace(Username) ? "user" : Username;
                        string fileName = string.Format("avatar_{0}.jpg", safeName);

                        if (isoStore.FileExists(fileName))
                        {
                            using (var fileStream = isoStore.OpenFile(fileName, FileMode.Open, FileAccess.Read))
                            {
                                var ms = new MemoryStream();
                                fileStream.CopyTo(ms);
                                ms.Position = 0;
                                bmp.SetSource(ms);
                                return bmp;
                            }
                        }
                    }
                }
                catch { }

                // Fallback на сетевой Gravatar, если локального кэша нет
                bmp.UriSource = AvatarUrl;
                return bmp;
            }
        }
    }
}