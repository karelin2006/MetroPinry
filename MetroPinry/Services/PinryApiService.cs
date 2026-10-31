using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using MetroPinry.Models;

namespace MetroPinry.Services
{
    public class PinryApiService
    {
        private HttpClient _httpClient;
        private string _baseUrl;

        public PinryApiService()
        {
            UpdateClient();
        }

        public void UpdateClient()
        {
            _baseUrl = StorageService.ServerUrl;
            _httpClient = new HttpClient();

            if (!string.IsNullOrEmpty(StorageService.AuthToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Token", StorageService.AuthToken);
            }
        }

        /// <summary>
        /// Вход по логину и паролю через стандартную сессию Django с получением API-токена.
        /// </summary>
        public async Task<UserProfile> LoginWithCredentialsAsync(string serverUrl, string username, string password)
        {
            string cleanUrl = serverUrl.TrimEnd('/');
            var cookies = new CookieContainer();

            Debug.WriteLine("=================== PINRY CREDENTIALS LOGIN ===================");

            // 1. Получаем CSRF-токен из корня API
            string probeUrl = cleanUrl + "/api/v2/profile/users/?_t=" + Environment.TickCount;
            var getReq = (HttpWebRequest)WebRequest.Create(probeUrl);
            getReq.Method = "GET";
            getReq.CookieContainer = cookies;
            getReq.Headers["User-Agent"] = "MetroPinry/1.0 (Windows Phone 8.1)";

            try
            {
                using (var res = (HttpWebResponse)await Task.Factory.FromAsync<WebResponse>(getReq.BeginGetResponse, getReq.EndGetResponse, null))
                {
                    // Успешный GET заполнит CookieContainer
                }
            }
            catch (WebException wex)
            {
                // 404/401 на этом шаге нормальны для анонимного запроса — куки всё равно прилетают
            }

            string csrfToken = string.Empty;
            var targetUri = new Uri(cleanUrl);
            foreach (Cookie c in cookies.GetCookies(targetUri))
            {
                if (c.Name == "csrftoken")
                {
                    csrfToken = c.Value;
                    break;
                }
            }

            Debug.WriteLine("CSRF Token: " + csrfToken);

            // 2. Отправляем JSON-логин на /api/v2/profile/login/
            string loginUrl = cleanUrl + "/api/v2/profile/login/";
            var postReq = (HttpWebRequest)WebRequest.Create(loginUrl);
            postReq.Method = "POST";
            postReq.ContentType = "application/json; charset=utf-8";
            postReq.CookieContainer = cookies;
            postReq.Headers["Referer"] = cleanUrl + "/";
            postReq.Headers["User-Agent"] = "MetroPinry/1.0 (Windows Phone 8.1)";

            if (!string.IsNullOrEmpty(csrfToken))
            {
                postReq.Headers["X-CSRFToken"] = csrfToken;
            }

            var payload = new
            {
                username = username,
                password = password
            };
            byte[] jsonBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload));

            using (var reqStream = await Task.Factory.FromAsync<System.IO.Stream>(postReq.BeginGetRequestStream, postReq.EndGetRequestStream, null))
            {
                await reqStream.WriteAsync(jsonBytes, 0, jsonBytes.Length);
            }

            try
            {
                using (var postRes = (HttpWebResponse)await Task.Factory.FromAsync<WebResponse>(postReq.BeginGetResponse, postReq.EndGetResponse, null))
                using (var reader = new System.IO.StreamReader(postRes.GetResponseStream()))
                {
                    string body = await reader.ReadToEndAsync();
                    Debug.WriteLine(string.Format("Login Status: {0} - {1}", (int)postRes.StatusCode, body));
                }
            }
            catch (WebException wex)
            {
                var errResp = wex.Response as HttpWebResponse;
                if (errResp != null)
                {
                    using (var reader = new System.IO.StreamReader(errResp.GetResponseStream()))
                    {
                        string errBody = await reader.ReadToEndAsync();
                        Debug.WriteLine(string.Format("Login Error: HTTP {0} - {1}", (int)errResp.StatusCode, errBody));

                        if (errBody.Contains("username and password doesn't match"))
                        {
                            throw new InvalidOperationException("Неверный логин или пароль.");
                        }
                        throw new InvalidOperationException("Ошибка сервера: " + errBody);
                    }
                }
                throw new InvalidOperationException("Сбой сети: " + wex.Message);
            }

            // 3. Сессия активна — запрашиваем профиль и сохраняем выданный токен
            string profileUrl = cleanUrl + "/api/v2/profile/users/?_t=" + Environment.TickCount;
            var profileReq = (HttpWebRequest)WebRequest.Create(profileUrl);
            profileReq.Method = "GET";
            profileReq.CookieContainer = cookies;
            profileReq.Headers["User-Agent"] = "MetroPinry/1.0 (Windows Phone 8.1)";

            string profileJson = string.Empty;
            using (var profileRes = (HttpWebResponse)await Task.Factory.FromAsync<WebResponse>(profileReq.BeginGetResponse, profileReq.EndGetResponse, null))
            using (var reader = new System.IO.StreamReader(profileRes.GetResponseStream()))
            {
                profileJson = await reader.ReadToEndAsync();
            }

            var userList = JsonConvert.DeserializeObject<List<UserProfile>>(profileJson);
            if (userList == null || userList.Count == 0 || string.IsNullOrEmpty(userList[0].Token))
            {
                throw new InvalidOperationException("Не удалось получить API-токен после авторизации.");
            }

            StorageService.ServerUrl = cleanUrl;
            StorageService.AuthToken = userList[0].Token;
            UpdateClient();

            Debug.WriteLine("=== LOGIN SUCCESSFUL: Token " + userList[0].Token + " saved ===");
            return userList[0];
        }

        /// <summary>
        /// Альтернативный вход: прямая проверка и сохранение существующего API-токена.
        /// </summary>
        public async Task<UserProfile> VerifyAndLoginWithTokenAsync(string serverUrl, string apiToken)
        {
            string cleanUrl = serverUrl.TrimEnd('/');
            string endpoint = cleanUrl + "/api/v2/profile/users/";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", apiToken);

                var response = await client.GetAsync(endpoint);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(string.Format("Ошибка авторизации HTTP {0}", (int)response.StatusCode));
                }

                var users = JsonConvert.DeserializeObject<List<UserProfile>>(responseBody);
                if (users == null || users.Count == 0)
                {
                    throw new InvalidOperationException("Сервер вернул пустой профиль пользователя.");
                }

                StorageService.ServerUrl = cleanUrl;
                StorageService.AuthToken = apiToken;
                UpdateClient();

                return users[0];
            }
        }

        public async Task<UserProfile> GetCurrentUserAsync()
        {
            string url = StorageService.ServerUrl.TrimEnd('/') + "/api/v2/profile/users/?format=json&_t=" + Environment.TickCount;
            System.Diagnostics.Debug.WriteLine("=== FETCHING USER PROFILE ===");
            System.Diagnostics.Debug.WriteLine("URL: " + url);

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Headers["Authorization"] = "Token " + StorageService.AuthToken;
            request.Headers["User-Agent"] = "MetroPinry/1.0 (Windows Phone 8.1)";

            try
            {
                using (var response = (HttpWebResponse)await Task.Factory.FromAsync<WebResponse>(request.BeginGetResponse, request.EndGetResponse, null))
                using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
                {
                    string json = await reader.ReadToEndAsync();

                    // Сервер возвращает массив: [{ "username": "goose", ... }]
                    var users = JsonConvert.DeserializeObject<List<UserProfile>>(json);
                    return (users != null && users.Count > 0) ? users[0] : null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("USER ERROR: " + ex.Message);
                return null;
            }
        }

        public async Task<PinResponse> GetRecentPinsAsync()
        {
            // Добавляем ?_t=... чтобы Windows Phone не брал пустой ответ из кэша
            string url = StorageService.ServerUrl.TrimEnd('/') + "/api/v2/pins/?format=json&_t=" + Environment.TickCount;
            System.Diagnostics.Debug.WriteLine("=== FETCHING PINS ===");
            System.Diagnostics.Debug.WriteLine("URL: " + url);

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Headers["Authorization"] = "Token " + StorageService.AuthToken;
            request.Headers["User-Agent"] = "MetroPinry/1.0 (Windows Phone 8.1)";

            try
            {
                using (var response = (HttpWebResponse)await Task.Factory.FromAsync<WebResponse>(request.BeginGetResponse, request.EndGetResponse, null))
                using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
                {
                    string json = await reader.ReadToEndAsync();
                    System.Diagnostics.Debug.WriteLine("PINS STATUS: " + (int)response.StatusCode);
                    System.Diagnostics.Debug.WriteLine("PINS JSON (первые 300 симв): " + (json.Length > 300 ? json.Substring(0, 300) + "..." : json));

                    return JsonConvert.DeserializeObject<PinResponse>(json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("PINS ERROR: " + ex.Message);
                return null;
            }
        }

        public async Task<BoardResponse> GetBoardsAsync()
        {
            string url = StorageService.ServerUrl.TrimEnd('/') + "/api/v2/boards/?format=json&_t=" + Environment.TickCount;
            System.Diagnostics.Debug.WriteLine("=== FETCHING BOARDS ===");
            System.Diagnostics.Debug.WriteLine("URL: " + url);

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Headers["Authorization"] = "Token " + StorageService.AuthToken;
            request.Headers["User-Agent"] = "MetroPinry/1.0 (Windows Phone 8.1)";

            try
            {
                using (var response = (HttpWebResponse)await Task.Factory.FromAsync<WebResponse>(request.BeginGetResponse, request.EndGetResponse, null))
                using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
                {
                    string json = await reader.ReadToEndAsync();
                    System.Diagnostics.Debug.WriteLine("BOARDS STATUS: " + (int)response.StatusCode);
                    System.Diagnostics.Debug.WriteLine("BOARDS JSON (первые 300 симв): " + (json.Length > 300 ? json.Substring(0, 300) + "..." : json));

                    return JsonConvert.DeserializeObject<BoardResponse>(json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("BOARDS ERROR: " + ex.Message);
                return null;
            }
        }

        public async Task<PinResponse> GetBoardPinsAsync(int boardId)
        {
            // Pinry API умеет фильтровать пины по параметру ?boards=ID
            string url = string.Format("{0}/api/v2/pins/?boards={1}&format=json&_t={2}",
                StorageService.ServerUrl.TrimEnd('/'), boardId, Environment.TickCount);

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Headers["Authorization"] = "Token " + StorageService.AuthToken;
            request.Headers["User-Agent"] = "MetroPinry/1.0 (Windows Phone 8.1)";

            try
            {
                using (var response = (HttpWebResponse)await Task.Factory.FromAsync<WebResponse>(request.BeginGetResponse, request.EndGetResponse, null))
                using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
                {
                    string json = await reader.ReadToEndAsync();
                    return JsonConvert.DeserializeObject<PinResponse>(json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("BOARD PINS ERROR: " + ex.Message);
                return null;
            }
        }
    }
}