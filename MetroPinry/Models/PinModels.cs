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

    public class Pin : INotifyPropertyChanged
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        private string _description;
        [JsonProperty("description")]
        public string Description
        {
            get { return _description; }
            set
            {
                if (_description != value)
                {
                    _description = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private List<string> _tags;
        [JsonProperty("tags")]
        public List<string> Tags
        {
            get { return _tags; }
            set
            {
                if (_tags != value)
                {
                    _tags = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private List<object> _boards;
        [JsonProperty("boards")]
        public List<object> Boards
        {
            get { return _boards; }
            set
            {
                if (_boards != value)
                {
                    _boards = value;
                    NotifyPropertyChanged();
                }
            }
        }

        [JsonProperty("image")]
        public PinImageData Image { get; set; }

        [JsonProperty("submitter")]
        public UserProfile Submitter { get; set; }

        private bool _isPrivate;
        [JsonProperty("private")]
        public bool IsPrivate
        {
            get { return _isPrivate; }
            set
            {
                if (_isPrivate != value)
                {
                    _isPrivate = value;
                    NotifyPropertyChanged();
                }
            }
        }

        [JsonIgnore]
        public Uri DisplayThumbnailUrl
        {
            get
            {
                if (Image == null) return null;

                string targetUrl = Image.ResolvedThumbnailUrl;
                if (string.IsNullOrEmpty(targetUrl))
                {
                    targetUrl = Image.ResolvedStandardUrl;
                }
                if (string.IsNullOrEmpty(targetUrl))
                {
                    targetUrl = Image.ResolvedOriginalUrl;
                }

                if (string.IsNullOrEmpty(targetUrl)) return null;

                try
                {
                    return new Uri(targetUrl, UriKind.Absolute);
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

                string targetUrl = Image.ResolvedStandardUrl;
                if (string.IsNullOrEmpty(targetUrl))
                {
                    targetUrl = Image.ResolvedOriginalUrl;
                }
                if (string.IsNullOrEmpty(targetUrl))
                {
                    targetUrl = Image.ResolvedThumbnailUrl;
                }

                if (string.IsNullOrEmpty(targetUrl)) return null;

                try
                {
                    return new Uri(targetUrl, UriKind.Absolute);
                }
                catch
                {
                    return null;
                }
            }
        }

        [JsonIgnore]
        public Uri DisplayOriginalUrl
        {
            get
            {
                if (Image == null) return null;

                string targetUrl = Image.ResolvedOriginalUrl;
                if (string.IsNullOrEmpty(targetUrl))
                {
                    targetUrl = Image.ResolvedStandardUrl;
                }

                if (string.IsNullOrEmpty(targetUrl)) return null;

                try
                {
                    return new Uri(targetUrl, UriKind.Absolute);
                }
                catch
                {
                    return DisplayStandardUrl;
                }
            }
        }

        public bool BelongsToBoard(int boardId, string boardName)
        {
            if (Boards == null || Boards.Count == 0) return false;

            string targetIdStr = "\"id\":" + boardId.ToString();
            string targetIdStrSpace = "\"id\": " + boardId.ToString();
            string targetNameStr = "\"name\":\"" + boardName + "\"";

            foreach (var item in Boards)
            {
                if (item == null) continue;

                string val = item.ToString();

                if (val == boardId.ToString()) return true;

                if (val.Contains(targetIdStr) || val.Contains(targetIdStrSpace))
                {
                    return true;
                }

                if (!string.IsNullOrEmpty(boardName) && val.Contains(targetNameStr))
                {
                    return true;
                }
            }
            return false;
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
                return FormatFullUrl(path);
            }
        }

        [JsonIgnore]
        public string ResolvedStandardUrl
        {
            get
            {
                string path = null;
                if (Standard != null && !string.IsNullOrEmpty(Standard.ImageUrl))
                {
                    path = Standard.ImageUrl;
                }
                return FormatFullUrl(path);
            }
        }

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

            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            if (path.StartsWith("/"))
            {
                return baseServer + path;
            }

            return baseServer + "/" + path;
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

    public class Board : INotifyPropertyChanged
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        private string _name;
        [JsonProperty("name")]
        public string Name
        {
            get { return _name; }
            set
            {
                if (_name != value)
                {
                    _name = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private int _pinsCount;
        [JsonProperty("total_pins")]
        public int PinsCount
        {
            get { return _pinsCount; }
            set
            {
                if (_pinsCount != value)
                {
                    _pinsCount = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private Pin _cover;
        [JsonProperty("cover")]
        public Pin Cover
        {
            get { return _cover; }
            set
            {
                if (_cover != value)
                {
                    _cover = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged("CoverImageUrl");
                }
            }
        }

        private bool _isPrivate;
        [JsonProperty("private")]
        public bool IsPrivate
        {
            get { return _isPrivate; }
            set
            {
                if (_isPrivate != value)
                {
                    _isPrivate = value;
                    NotifyPropertyChanged();
                }
            }
        }

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