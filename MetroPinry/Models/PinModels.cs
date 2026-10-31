using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using MetroPinry.Services;

namespace MetroPinry.Models
{
    public class UserProfile : INotifyPropertyChanged
    {
        private string _username;
        [JsonProperty("username")]
        public string Username
        {
            get { return _username; }
            set
            {
                if (_username != value)
                {
                    _username = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private string _email;
        [JsonProperty("email")]
        public string Email
        {
            get { return _email; }
            set
            {
                if (_email != value)
                {
                    _email = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private string _token;
        [JsonProperty("token")]
        public string Token
        {
            get { return _token; }
            set
            {
                if (_token != value)
                {
                    _token = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private string _gravatarHash;
        [JsonProperty("gravatar")]
        public string GravatarHash
        {
            get { return _gravatarHash; }
            set
            {
                if (_gravatarHash != value)
                {
                    _gravatarHash = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged("AvatarUrl");
                }
            }
        }

        [JsonProperty("resource_link")]
        public string ResourceLink { get; set; }

        [JsonIgnore]
        public Uri AvatarUrl
        {
            get
            {
                string hash = !string.IsNullOrEmpty(GravatarHash)
                    ? GravatarHash
                    : "00000000000000000000000000000000";

                string url = string.Format("http://www.gravatar.com/avatar/{0}?s=144&d=identicon", hash);
                return new Uri(url, UriKind.Absolute);
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

    public class PinResponse
    {
        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("next")]
        public string Next { get; set; }

        [JsonProperty("results")]
        public List<Pin> Results { get; set; }
    }

    public class Pin
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("image")]
        public PinImageData Image { get; set; }

        [JsonProperty("submitter")]
        public UserProfile Submitter { get; set; }

        [JsonIgnore]
        public Uri DisplayThumbnailUrl
        {
            get
            {
                if (Image == null || string.IsNullOrEmpty(Image.ResolvedThumbnailUrl))
                    return null;

                try
                {
                    return new Uri(Image.ResolvedThumbnailUrl, UriKind.Absolute);
                }
                catch
                {
                    return null;
                }
            }
        }

        [JsonIgnore]
        public Uri DisplayStandardUrl
        {
            get
            {
                if (Image == null) return null;

                string path = null;
                if (Image.Standard != null && !string.IsNullOrEmpty(Image.Standard.ImageUrl))
                {
                    path = Image.Standard.ImageUrl;
                }
                else
                {
                    path = Image.RawImageUrl;
                }

                string full = PinImageData.FormatFullUrl(path);
                if (string.IsNullOrEmpty(full)) return DisplayThumbnailUrl;

                try
                {
                    return new Uri(full, UriKind.Absolute);
                }
                catch
                {
                    return DisplayThumbnailUrl;
                }
            }
        }

        /// <summary>
        /// Абсолютный адрес оригинального полноразмерного файла для скачивания в память
        /// </summary>
        [JsonIgnore]
        public Uri DisplayOriginalUrl
        {
            get
            {
                if (Image == null || string.IsNullOrEmpty(Image.ResolvedOriginalUrl))
                    return null;

                try
                {
                    return new Uri(Image.ResolvedOriginalUrl, UriKind.Absolute);
                }
                catch
                {
                    return DisplayStandardUrl;
                }
            }
        }
    }

    public class PinImageData
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("image")]
        public string RawImageUrl { get; set; }

        [JsonProperty("thumbnail")]
        public PinImageDetail Thumbnail { get; set; }

        [JsonProperty("standard")]
        public PinImageDetail Standard { get; set; }

        [JsonIgnore]
        public string ResolvedThumbnailUrl
        {
            get
            {
                string path = null;
                if (Thumbnail != null && !string.IsNullOrEmpty(Thumbnail.ImageUrl))
                {
                    path = Thumbnail.ImageUrl;
                }
                else if (Standard != null && !string.IsNullOrEmpty(Standard.ImageUrl))
                {
                    path = Standard.ImageUrl;
                }
                else
                {
                    path = RawImageUrl;
                }

                return FormatFullUrl(path);
            }
        }

        /// <summary>
        /// Возвращает полный URL оригинального загруженного файла
        /// </summary>
        [JsonIgnore]
        public string ResolvedOriginalUrl
        {
            get
            {
                return FormatFullUrl(RawImageUrl);
            }
        }

        public static string FormatFullUrl(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            string baseServer = (StorageService.ServerUrl ?? "http://karelin-art.ddns.net").TrimEnd('/');

            if (path.StartsWith("/"))
            {
                return baseServer + path;
            }

            if (path.StartsWith("https://karelin-art.ddns.net", StringComparison.OrdinalIgnoreCase))
            {
                return "http://" + path.Substring("https://".Length);
            }

            return path;
        }
    }

    public class PinImageDetail
    {
        [JsonProperty("image")]
        public string ImageUrl { get; set; }

        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }
    }

    public class BoardResponse
    {
        [JsonProperty("results")]
        public List<Board> Results { get; set; }
    }

    public class Board
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("total_pins")]
        public int PinsCount { get; set; }

        [JsonProperty("cover")]
        public Pin Cover { get; set; }

        [JsonIgnore]
        public Uri CoverImageUrl
        {
            get
            {
                if (Cover != null)
                {
                    return Cover.DisplayThumbnailUrl;
                }
                return null;
            }
        }
    }
}