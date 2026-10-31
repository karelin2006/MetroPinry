using System.IO.IsolatedStorage;

namespace MetroPinry.Services
{
    public static class StorageService
    {
        private const string ServerUrlKey = "ServerUrl";
        private const string AuthTokenKey = "AuthToken";

        private static readonly IsolatedStorageSettings Settings = IsolatedStorageSettings.ApplicationSettings;

        public static string ServerUrl
        {
            get
            {
                string value;
                return Settings.TryGetValue(ServerUrlKey, out value) ? value : string.Empty;
            }
            set
            {
                Settings[ServerUrlKey] = value != null ? value.TrimEnd('/') : string.Empty;
                Settings.Save();
            }
        }

        public static string AuthToken
        {
            get
            {
                string value;
                return Settings.TryGetValue(AuthTokenKey, out value) ? value : string.Empty;
            }
            set
            {
                Settings[AuthTokenKey] = value;
                Settings.Save();
            }
        }

        public static bool IsAuthenticated
        {
            get { return !string.IsNullOrEmpty(ServerUrl) && !string.IsNullOrEmpty(AuthToken); }
        }

        public static void ClearSession()
        {
            Settings.Remove(AuthTokenKey);
            Settings.Save();
        }
    }
}