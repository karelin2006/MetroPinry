using System.Collections.Generic;
using System.IO.IsolatedStorage;
using System.Linq;
using Newtonsoft.Json;
using MetroPinry.Models;
using System;

namespace MetroPinry.Services
{
    public static class StorageService
    {
        private static IsolatedStorageSettings Settings = IsolatedStorageSettings.ApplicationSettings;

        // 1. АКТИВНАЯ СЕССИЯ (Используется в API)
        public static string ServerUrl
        {
            get { return Settings.Contains("ActiveServerUrl") ? (string)Settings["ActiveServerUrl"] : null; }
            set { Settings["ActiveServerUrl"] = value; Settings.Save(); }
        }

        public static string AuthToken
        {
            get { return Settings.Contains("ActiveAuthToken") ? (string)Settings["ActiveAuthToken"] : null; }
            set { Settings["ActiveAuthToken"] = value; Settings.Save(); }
        }

        public static bool IsAuthenticated
        {
            get { return !string.IsNullOrEmpty(AuthToken); }
        }

        // 2. СПИСОК АККАУНТОВ
        public static List<PinryAccount> Accounts
        {
            get
            {
                List<PinryAccount> list = new List<PinryAccount>();
                if (Settings.Contains("SavedAccountsList"))
                {
                    string json = (string)Settings["SavedAccountsList"];
                    list = JsonConvert.DeserializeObject<List<PinryAccount>>(json) ?? new List<PinryAccount>();
                }

                if (!Settings.Contains("MigratedToMulti"))
                {
                    string oldUrl = Settings.Contains("ServerUrl") ? (string)Settings["ServerUrl"] : null;
                    string oldToken = Settings.Contains("AuthToken") ? (string)Settings["AuthToken"] : null;

                    if (!string.IsNullOrEmpty(oldUrl) && !string.IsNullOrEmpty(oldToken))
                    {
                        list.Add(new PinryAccount
                        {
                            ServerUrl = oldUrl,
                            Username = "migrated_account",
                            Token = oldToken,
                            GravatarHash = ""
                        });
                        Settings["SavedAccountsList"] = JsonConvert.SerializeObject(list);
                        Settings["ActiveServerUrl"] = oldUrl;
                        Settings["ActiveAuthToken"] = oldToken;
                    }
                    Settings["MigratedToMulti"] = true;
                    Settings.Save();
                }
                list.RemoveAll(a => a.Username == "old_account" || a.Username == "migrated_account" || string.IsNullOrEmpty(a.ServerUrl));
                return list;
            }
            set
            {
                Settings["SavedAccountsList"] = JsonConvert.SerializeObject(value);
                Settings.Save();
            }
        }

        // 3. СОХРАНЕНИЕ АККАУНТА (Вызывается из LoginPage)
        public static void SaveOrUpdateAccount(string serverUrl, string username, string token, string gravatarHash)
        {
            var accs = Accounts;
            string safeUsername = string.IsNullOrWhiteSpace(username) ? "user" : username.Trim();
            string safeHash = !string.IsNullOrWhiteSpace(gravatarHash) ? gravatarHash.Trim() : "";

            var existing = accs.FirstOrDefault(a => a.ServerUrl == serverUrl && a.Username.Equals(safeUsername, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Token = token;
                if (!string.IsNullOrEmpty(safeHash)) existing.GravatarHash = safeHash;
            }
            else
            {
                accs.Add(new PinryAccount
                {
                    ServerUrl = serverUrl,
                    Username = safeUsername,
                    Token = token,
                    GravatarHash = safeHash
                });
            }

            Accounts = accs;
            ServerUrl = serverUrl;
            AuthToken = token;
        }

        public static void SwitchActiveAccount(PinryAccount targetAccount)
        {
            if (targetAccount == null) return;

            // 1. Сначала отключаем старую сессию
            AuthToken = null;

            // 2. Устанавливаем новые активные параметры
            ServerUrl = targetAccount.ServerUrl;
            AuthToken = targetAccount.Token;
        }

        public static async System.Threading.Tasks.Task DownloadAndSaveAvatarAsync(string username, string gravatarHash)
        {
            try
            {
                string hash = !string.IsNullOrEmpty(gravatarHash) ? gravatarHash : "00000000000000000000000000000000";
                string url = string.Format("http://www.gravatar.com/avatar/{0}?s=144&d=identicon", hash);

                using (var client = new System.Net.Http.HttpClient())
                {
                    byte[] bytes = await client.GetByteArrayAsync(url);
                    using (var isoStore = IsolatedStorageFile.GetUserStoreForApplication())
                    {
                        string fileName = string.Format("avatar_{0}.jpg", username);
                        using (var stream = isoStore.OpenFile(fileName, System.IO.FileMode.Create))
                        {
                            stream.Write(bytes, 0, bytes.Length);
                        }
                    }
                }
            }
            catch { }
        }
    }
}