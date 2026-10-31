using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using MetroPinry.Models;

namespace MetroPinry.Services
{
    public class PinryApiService
    {
        private static readonly JsonSerializerSettings SafeSettings = new JsonSerializerSettings
        {
            ContractResolver = SilverlightContractResolver.Instance,
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

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

        private static string NormalizeBaseUrl(string serverUrl)
        {
            string clean = (serverUrl ?? string.Empty).Trim().TrimEnd('/');
            if (clean.EndsWith("/api/v2", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(0, clean.Length - "/api/v2".Length);
            }
            else if (clean.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(0, clean.Length - "/api".Length);
            }
            return clean.TrimEnd('/');
        }

        public async Task<UserProfile> LoginWithCredentialsAsync(string serverUrl, string username, string password)
        {
            string cleanUrl = NormalizeBaseUrl(serverUrl);
            string loginResponseBody = string.Empty;
            bool isSuccess = false;

            Debug.WriteLine("=================== PINRY CREDENTIALS LOGIN ===================");
            Debug.WriteLine("Target Base URL: " + cleanUrl);

            using (var handler = new HttpClientHandler { UseCookies = true })
            using (var client = new HttpClient(handler))
            {
                client.DefaultRequestHeaders.Add("User-Agent", "MetroPinry/1.0 (Windows Phone 8.1)");

                string probeUrl = cleanUrl + "/api/v2/profile/users/?_t=" + Environment.TickCount;
                try
                {
                    // Убраны ConfigureAwait(false), чтобы оставаться в безопасном контексте UI
                    await client.GetAsync(probeUrl);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Probe warning: " + ex.Message);
                }

                string csrfToken = string.Empty;
                var cookies = handler.CookieContainer.GetCookies(new Uri(cleanUrl));
                foreach (Cookie c in cookies)
                {
                    if (string.Equals(c.Name, "csrftoken", StringComparison.OrdinalIgnoreCase))
                    {
                        csrfToken = c.Value;
                        break;
                    }
                }

                string loginUrl = cleanUrl + "/api/v2/profile/login/";
                if (!string.IsNullOrEmpty(csrfToken))
                {
                    client.DefaultRequestHeaders.Add("X-CSRFToken", csrfToken);
                }
                client.DefaultRequestHeaders.Referrer = new Uri(cleanUrl + "/");

                string safeUser = username.Replace("\\", "\\\\").Replace("\"", "\\\"");
                string safePass = password.Replace("\\", "\\\\").Replace("\"", "\\\"");
                string jsonPayload = string.Format("{{\"username\":\"{0}\",\"password\":\"{1}\"}}", safeUser, safePass);

                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                try
                {
                    var response = await client.PostAsync(loginUrl, content);
                    loginResponseBody = await response.Content.ReadAsStringAsync();
                    isSuccess = response.IsSuccessStatusCode;
                }
                catch (Exception reqEx)
                {
                    throw new InvalidOperationException("Сбой сети: " + reqEx.Message);
                }
            } // Безопасное закрытие HTTP-клиента до выброса логических ошибок

            // Выбрасываем ошибки здесь, когда фоновые потоки уже завершены
            if (!isSuccess)
            {
                if (loginResponseBody.Contains("username and password doesn't match"))
                {
                    throw new InvalidOperationException("Неверный логин или пароль.");
                }
                throw new InvalidOperationException("Ошибка сервера: " + loginResponseBody);
            }

            string token = ExtractJsonValue(loginResponseBody, "token");
            string retUser = ExtractJsonValue(loginResponseBody, "username");
            string retEmail = ExtractJsonValue(loginResponseBody, "email");
            string retLink = ExtractJsonValue(loginResponseBody, "resource_link");

            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException("Не удалось получить API-токен после авторизации.");
            }

            var profile = new UserProfile
            {
                Username = !string.IsNullOrEmpty(retUser) ? retUser : username,
                Email = retEmail,
                Token = token,
                ResourceLink = retLink
            };

            StorageService.ServerUrl = cleanUrl;
            StorageService.AuthToken = profile.Token;
            UpdateClient();

            Debug.WriteLine("=== LOGIN SUCCESSFUL ===");
            return profile;
        }

        public static string ExtractJsonValue(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return string.Empty;

            string pattern = "\"" + key + "\":\"";
            int start = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (start == -1) return string.Empty;
            start += pattern.Length;
            int end = json.IndexOf("\"", start, StringComparison.OrdinalIgnoreCase);
            if (end == -1) return string.Empty;
            return json.Substring(start, end - start);
        }

        public static int ExtractJsonInt(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return 0;

            string pattern = "\"" + key + "\":";
            int idx = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx == -1) return 0;

            int valStart = idx + pattern.Length;
            while (valStart < json.Length && (json[valStart] == ' ' || json[valStart] == ':')) valStart++;

            int valEnd = valStart;
            while (valEnd < json.Length && char.IsDigit(json[valEnd])) valEnd++;

            int result;
            int.TryParse(json.Substring(valStart, valEnd - valStart), out result);
            return result;
        }

        public async Task<UserProfile> VerifyAndLoginWithTokenAsync(string serverUrl, string apiToken)
        {
            string cleanUrl = NormalizeBaseUrl(serverUrl);
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

                var users = JsonConvert.DeserializeObject<List<UserProfile>>(responseBody, SafeSettings);
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

        public async Task<bool> RegisterWithCredentialsAsync(string serverUrl, string username, string email, string password)
        {
            string cleanUrl = NormalizeBaseUrl(serverUrl);

            // В API Pinry создание пользователя обычно идет через этот эндпоинт
            string registerUrl = cleanUrl + "/api/v2/profile/users/";

            string safeUser = username.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string safeEmail = email.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string safePass = password.Replace("\\", "\\\\").Replace("\"", "\\\"");

            string jsonPayload = string.Format("{{\"username\":\"{0}\",\"email\":\"{1}\",\"password\":\"{2}\"}}", safeUser, safeEmail, safePass);
            byte[] payloadBytes = Encoding.UTF8.GetBytes(jsonPayload);

            var request = (HttpWebRequest)WebRequest.Create(new Uri(registerUrl));
            request.Method = "POST";
            request.ContentType = "application/json";
            request.UserAgent = "MetroPinry/1.0 (Windows Phone 8.1)";

            try
            {
                using (var stream = await Task.Factory.FromAsync<Stream>(request.BeginGetRequestStream, request.EndGetRequestStream, null))
                {
                    await stream.WriteAsync(payloadBytes, 0, payloadBytes.Length);
                }

                using (var response = (HttpWebResponse)await Task.Factory.FromAsync<WebResponse>(request.BeginGetResponse, request.EndGetResponse, null))
                {
                    // 201 Created или 200 OK
                    return response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK;
                }
            }
            catch (WebException wex)
            {
                var errResp = wex.Response as HttpWebResponse;
                if (errResp != null)
                {
                    using (var reader = new StreamReader(errResp.GetResponseStream()))
                    {
                        string errBody = reader.ReadToEnd();
                        throw new Exception("Ошибка сервера при регистрации: " + errBody);
                    }
                }
                throw new Exception("Сбой сети: " + wex.Message);
            }
        }

        public async Task<UserProfile> GetCurrentUserAsync()
        {
            // Гостю профиль запрашивать не нужно, возвращаем null сразу
            if (string.IsNullOrEmpty(StorageService.AuthToken)) return null;

            string cleanUrl = NormalizeBaseUrl(StorageService.ServerUrl);
            string url = cleanUrl + "/api/v2/profile/users/?format=json&_t=" + Environment.TickCount;

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Headers["Authorization"] = "Token " + StorageService.AuthToken;
            request.Headers["User-Agent"] = "MetroPinry/1.0 (Windows Phone 8.1)";

            try
            {
                using (var response = (HttpWebResponse)await Task.Factory.FromAsync<WebResponse>(request.BeginGetResponse, request.EndGetResponse, null))
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    string json = await reader.ReadToEndAsync();
                    var users = JsonConvert.DeserializeObject<List<UserProfile>>(json, SafeSettings);
                    return (users != null && users.Count > 0) ? users[0] : null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("USER ERROR: " + ex.ToString());
                return null;
            }
        }

        public async Task<PinResponse> GetRecentPinsAsync()
        {
            string cleanUrl = NormalizeBaseUrl(StorageService.ServerUrl);
            string url = cleanUrl + "/api/v2/pins/?format=json&_t=" + Environment.TickCount;

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Headers["User-Agent"] = "MetroPinry/1.0 (Windows Phone 8.1)";

            // ИСПРАВЛЕНИЕ: Добавляем авторизацию ТОЛЬКО если есть токен (иначе запрос будет анонимным)
            if (!string.IsNullOrEmpty(StorageService.AuthToken))
            {
                request.Headers["Authorization"] = "Token " + StorageService.AuthToken;
            }

            try
            {
                using (var response = (HttpWebResponse)await Task.Factory.FromAsync<WebResponse>(request.BeginGetResponse, request.EndGetResponse, null))
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    string json = await reader.ReadToEndAsync();
                    return JsonConvert.DeserializeObject<PinResponse>(json, SafeSettings);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("PINS ERROR: " + ex.ToString());
                return null;
            }
        }

        public async Task<BoardResponse> GetBoardsAsync()
        {
            string cleanUrl = NormalizeBaseUrl(StorageService.ServerUrl);
            string url = cleanUrl + "/api/v2/boards/?format=json&_t=" + Environment.TickCount;

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Headers["User-Agent"] = "MetroPinry/1.0 (Windows Phone 8.1)";

            // ИСПРАВЛЕНИЕ: Авторизация только для залогиненных пользователей
            if (!string.IsNullOrEmpty(StorageService.AuthToken))
            {
                request.Headers["Authorization"] = "Token " + StorageService.AuthToken;
            }

            try
            {
                using (var response = (HttpWebResponse)await Task.Factory.FromAsync<WebResponse>(request.BeginGetResponse, request.EndGetResponse, null))
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    string json = await reader.ReadToEndAsync();
                    return JsonConvert.DeserializeObject<BoardResponse>(json, SafeSettings);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("BOARDS ERROR: " + ex.ToString());
                return null;
            }
        }

        public async Task<PinResponse> GetBoardPinsAsync(int boardId, string boardName = null)
        {
            string cleanUrl = NormalizeBaseUrl(StorageService.ServerUrl);
            string url = string.Format("{0}/api/v2/pins/?pins__id={1}&format=json&_t={2}",
                cleanUrl, boardId, Environment.TickCount);

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Headers["User-Agent"] = "MetroPinry/1.0 (Windows Phone 8.1)";

            // ИСПРАВЛЕНИЕ: Авторизация только для залогиненных пользователей
            if (!string.IsNullOrEmpty(StorageService.AuthToken))
            {
                request.Headers["Authorization"] = "Token " + StorageService.AuthToken;
            }

            try
            {
                using (var response = (HttpWebResponse)await Task.Factory.FromAsync<WebResponse>(request.BeginGetResponse, request.EndGetResponse, null))
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    string json = await reader.ReadToEndAsync();
                    return JsonConvert.DeserializeObject<PinResponse>(json, SafeSettings);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("BOARD PINS ERROR: " + ex.ToString());
                return null;
            }
        }

        public async Task<bool> DeletePinAsync(int pinId)
        {
            string cleanUrl = NormalizeBaseUrl(StorageService.ServerUrl);
            string url = string.Format("{0}/api/v2/pins/{1}/", cleanUrl, pinId);

            var request = (HttpWebRequest)WebRequest.Create(new Uri(url));
            request.Method = "DELETE";
            request.Headers["User-Agent"] = "MetroPinry/1.0 (Windows Phone 8.1)";

            if (!string.IsNullOrEmpty(StorageService.AuthToken))
            {
                request.Headers["Authorization"] = "Token " + StorageService.AuthToken;
            }

            try
            {
                using (var response = (HttpWebResponse)await Task.Factory.FromAsync<WebResponse>(request.BeginGetResponse, request.EndGetResponse, null))
                {
                    return response.StatusCode == HttpStatusCode.NoContent || response.StatusCode == HttpStatusCode.OK;
                }
            }
            catch (WebException wex)
            {
                return false;
            }
        }

        public async Task<bool> UpdatePinAsync(int pinId, string description, List<string> tags, bool isPrivate, int? targetBoardId = null)
        {
            string cleanUrl = NormalizeBaseUrl(StorageService.ServerUrl);
            string url = string.Format("{0}/api/v2/pins/{1}/", cleanUrl, pinId);

            var request = (HttpWebRequest)WebRequest.Create(new Uri(url));
            request.Method = "PATCH";
            request.ContentType = "application/json; charset=utf-8";
            request.Headers["User-Agent"] = "MetroPinry/1.0 (Windows Phone 8.1)";

            if (!string.IsNullOrEmpty(StorageService.AuthToken))
            {
                request.Headers["Authorization"] = "Token " + StorageService.AuthToken;
            }

            var payloadObj = new
            {
                description = description ?? string.Empty,
                tags = tags ?? new List<string>(),
                @private = isPrivate
            };

            string json = JsonConvert.SerializeObject(payloadObj, SafeSettings);
            byte[] bytes = Encoding.UTF8.GetBytes(json);

            try
            {
                using (var stream = await Task.Factory.FromAsync<Stream>(request.BeginGetRequestStream, request.EndGetRequestStream, null))
                {
                    await stream.WriteAsync(bytes, 0, bytes.Length);
                }

                using (var response = (HttpWebResponse)await Task.Factory.FromAsync<WebResponse>(request.BeginGetResponse, request.EndGetResponse, null))
                {
                    bool success = response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NoContent;

                    if (success && targetBoardId.HasValue && targetBoardId.Value > 0)
                    {
                        await AddPinToBoardAsync(targetBoardId.Value, pinId);
                    }
                    return success;
                }
            }
            catch (WebException wex)
            {
                return false;
            }
        }

        public async Task<bool> UpdateBoardAsync(int boardId, string name, bool isPrivate)
        {
            string cleanUrl = NormalizeBaseUrl(StorageService.ServerUrl);
            string url = string.Format("{0}/api/v2/boards/{1}/", cleanUrl, boardId);

            var request = (HttpWebRequest)WebRequest.Create(new Uri(url));
            request.Method = "PATCH";
            request.ContentType = "application/json; charset=utf-8";
            request.Headers["User-Agent"] = "MetroPinry/1.0 (Windows Phone 8.1)";

            if (!string.IsNullOrEmpty(StorageService.AuthToken))
            {
                request.Headers["Authorization"] = "Token " + StorageService.AuthToken;
            }

            var payloadObj = new
            {
                name = name ?? string.Empty,
                @private = isPrivate
            };

            string json = JsonConvert.SerializeObject(payloadObj, SafeSettings);
            byte[] bytes = Encoding.UTF8.GetBytes(json);

            try
            {
                using (var stream = await Task.Factory.FromAsync<Stream>(request.BeginGetRequestStream, request.EndGetRequestStream, null))
                {
                    await stream.WriteAsync(bytes, 0, bytes.Length);
                }

                using (var response = (HttpWebResponse)await Task.Factory.FromAsync<WebResponse>(request.BeginGetResponse, request.EndGetResponse, null))
                {
                    return response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NoContent;
                }
            }
            catch (WebException wex)
            {
                return false;
            }
        }

        public class NewPinRequest
        {
            [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
            public string Url { get; set; }

            [JsonProperty("image_by_id", NullValueHandling = NullValueHandling.Ignore)]
            public int? ImageById { get; set; }

            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonProperty("referer")]
            public string Referer { get; set; }

            [JsonProperty("boards")]
            public List<string> Boards { get; set; }

            [JsonProperty("tags")]
            public List<string> Tags { get; set; }

            [JsonProperty("private")]
            public bool IsPrivate { get; set; }
        }

        public class NewBoardRequest
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("private")]
            public bool IsPrivate { get; set; }
        }

        public async Task<bool> CreateBoardAsync(string name, bool isPrivate)
        {
            var requestData = new NewBoardRequest
            {
                Name = name ?? string.Empty,
                IsPrivate = isPrivate
            };

            string json = JsonConvert.SerializeObject(requestData, SafeSettings);
            byte[] byteArray = Encoding.UTF8.GetBytes(json);

            string cleanUrl = NormalizeBaseUrl(StorageService.ServerUrl);
            string url = cleanUrl + "/api/v2/boards/";

            var request = (HttpWebRequest)WebRequest.Create(new Uri(url));
            request.Method = "POST";
            request.ContentType = "application/json; charset=utf-8";
            request.Headers["User-Agent"] = "MetroPinry/1.0 (Windows Phone 8.1)";

            if (!string.IsNullOrEmpty(StorageService.AuthToken))
            {
                request.Headers["Authorization"] = "Token " + StorageService.AuthToken;
            }

            using (var reqStream = await Task.Factory.FromAsync<Stream>(request.BeginGetRequestStream, request.EndGetRequestStream, null))
            {
                await reqStream.WriteAsync(byteArray, 0, byteArray.Length);
            }

            try
            {
                using (var response = (HttpWebResponse)await Task.Factory.FromAsync<WebResponse>(request.BeginGetResponse, request.EndGetResponse, null))
                {
                    return response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK;
                }
            }
            catch (WebException wex)
            {
                return false;
            }
        }

        public async Task<bool> CreatePinFromUrlAsync(string imageUrl, string description, List<string> boardNames = null, List<string> tags = null, bool isPrivate = false, int? targetBoardId = null)
        {
            var requestData = new NewPinRequest
            {
                Url = imageUrl,
                Description = description ?? string.Empty,
                Referer = !string.IsNullOrEmpty(imageUrl) ? imageUrl : NormalizeBaseUrl(StorageService.ServerUrl),
                Boards = boardNames ?? new List<string>(),
                Tags = tags ?? new List<string>(),
                IsPrivate = isPrivate
            };

            string json = JsonConvert.SerializeObject(requestData, SafeSettings);
            byte[] byteArray = Encoding.UTF8.GetBytes(json);

            string cleanUrl = NormalizeBaseUrl(StorageService.ServerUrl);
            string url = cleanUrl + "/api/v2/pins/";

            var request = (HttpWebRequest)WebRequest.Create(new Uri(url));
            request.Method = "POST";
            request.ContentType = "application/json; charset=utf-8";
            request.Headers["User-Agent"] = "MetroPinry/1.0 (Windows Phone 8.1)";

            if (!string.IsNullOrEmpty(StorageService.AuthToken))
            {
                request.Headers["Authorization"] = "Token " + StorageService.AuthToken;
            }

            using (var reqStream = await Task.Factory.FromAsync<Stream>(request.BeginGetRequestStream, request.EndGetRequestStream, null))
            {
                await reqStream.WriteAsync(byteArray, 0, byteArray.Length);
            }

            try
            {
                using (var response = (HttpWebResponse)await Task.Factory.FromAsync<WebResponse>(request.BeginGetResponse, request.EndGetResponse, null))
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    string responseBody = await reader.ReadToEndAsync();
                    bool success = response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK;

                    if (success && targetBoardId.HasValue && targetBoardId.Value > 0)
                    {
                        int pinId = ExtractJsonInt(responseBody, "id");
                        if (pinId > 0)
                        {
                            await AddPinToBoardAsync(targetBoardId.Value, pinId);
                        }
                    }
                    return success;
                }
            }
            catch (WebException wex)
            {
                return false;
            }
        }

        public async Task<bool> UploadPinImageAsync(Stream imageStream, string description, List<string> boardNames = null, List<string> tags = null, bool isPrivate = false, int? targetBoardId = null)
        {
            if (imageStream == null) return false;

            string cleanUrl = NormalizeBaseUrl(StorageService.ServerUrl);
            string uploadUrl = cleanUrl + "/api/v2/images/";
            string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");

            var uploadReq = (HttpWebRequest)WebRequest.Create(new Uri(uploadUrl));
            uploadReq.Method = "POST";
            uploadReq.ContentType = "multipart/form-data; boundary=" + boundary;
            uploadReq.Headers["User-Agent"] = "MetroPinry/1.0 (Windows Phone 8.1)";

            if (!string.IsNullOrEmpty(StorageService.AuthToken))
            {
                uploadReq.Headers["Authorization"] = "Token " + StorageService.AuthToken;
            }

            byte[] boundaryBytes = Encoding.UTF8.GetBytes("\r\n--" + boundary + "\r\n");
            byte[] trailerBytes = Encoding.UTF8.GetBytes("\r\n--" + boundary + "--\r\n");

            using (var reqStream = await Task.Factory.FromAsync<Stream>(uploadReq.BeginGetRequestStream, uploadReq.EndGetRequestStream, null))
            {
                await reqStream.WriteAsync(boundaryBytes, 0, boundaryBytes.Length);
                string fileHeader = "Content-Disposition: form-data; name=\"image\"; filename=\"upload.jpg\"\r\nContent-Type: image/jpeg\r\n\r\n";
                byte[] fileHeaderBytes = Encoding.UTF8.GetBytes(fileHeader);
                await reqStream.WriteAsync(fileHeaderBytes, 0, fileHeaderBytes.Length);

                if (imageStream.CanSeek)
                {
                    imageStream.Position = 0;
                }

                byte[] buffer = new byte[4096];
                int bytesRead;
                while ((bytesRead = imageStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    await reqStream.WriteAsync(buffer, 0, bytesRead);
                }

                await reqStream.WriteAsync(trailerBytes, 0, trailerBytes.Length);
            }

            string imageResponseJson = string.Empty;
            try
            {
                using (var res = (HttpWebResponse)await Task.Factory.FromAsync<WebResponse>(uploadReq.BeginGetResponse, uploadReq.EndGetResponse, null))
                using (var reader = new StreamReader(res.GetResponseStream()))
                {
                    imageResponseJson = await reader.ReadToEndAsync();
                }
            }
            catch (WebException wex)
            {
                return false;
            }

            int imageId = ExtractJsonInt(imageResponseJson, "id");
            if (imageId <= 0) return false;

            var requestData = new NewPinRequest
            {
                ImageById = imageId,
                Description = description ?? string.Empty,
                Referer = cleanUrl,
                Boards = boardNames ?? new List<string>(),
                Tags = tags ?? new List<string>(),
                IsPrivate = isPrivate
            };

            string pinJson = JsonConvert.SerializeObject(requestData, SafeSettings);
            byte[] byteArray = Encoding.UTF8.GetBytes(pinJson);

            string pinUrl = cleanUrl + "/api/v2/pins/";
            var pinReq = (HttpWebRequest)WebRequest.Create(new Uri(pinUrl));
            pinReq.Method = "POST";
            pinReq.ContentType = "application/json; charset=utf-8";
            pinReq.Headers["User-Agent"] = "MetroPinry/1.0 (Windows Phone 8.1)";

            if (!string.IsNullOrEmpty(StorageService.AuthToken))
            {
                pinReq.Headers["Authorization"] = "Token " + StorageService.AuthToken;
            }

            using (var reqStream = await Task.Factory.FromAsync<Stream>(pinReq.BeginGetRequestStream, pinReq.EndGetRequestStream, null))
            {
                await reqStream.WriteAsync(byteArray, 0, byteArray.Length);
            }

            try
            {
                using (var response = (HttpWebResponse)await Task.Factory.FromAsync<WebResponse>(pinReq.BeginGetResponse, pinReq.EndGetResponse, null))
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    string pinResponseJson = await reader.ReadToEndAsync();
                    bool success = response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK;

                    if (success && targetBoardId.HasValue && targetBoardId.Value > 0)
                    {
                        int newPinId = ExtractJsonInt(pinResponseJson, "id");
                        if (newPinId > 0)
                        {
                            await AddPinToBoardAsync(targetBoardId.Value, newPinId);
                        }
                    }

                    return success;
                }
            }
            catch (WebException wex)
            {
                return false;
            }
        }

        public async Task<bool> AddPinToBoardAsync(int boardId, int pinId)
        {
            string cleanUrl = NormalizeBaseUrl(StorageService.ServerUrl);
            string url = string.Format("{0}/api/v2/boards/{1}/", cleanUrl, boardId);

            var request = (HttpWebRequest)WebRequest.Create(new Uri(url));
            request.Method = "PATCH";
            request.ContentType = "application/json; charset=utf-8";
            request.Headers["User-Agent"] = "MetroPinry/1.0 (Windows Phone 8.1)";

            if (!string.IsNullOrEmpty(StorageService.AuthToken))
            {
                request.Headers["Authorization"] = "Token " + StorageService.AuthToken;
            }

            string payload = string.Format("{{\"pins_to_add\":[{0}]}}", pinId);
            byte[] bytes = Encoding.UTF8.GetBytes(payload);

            using (var stream = await Task.Factory.FromAsync<Stream>(request.BeginGetRequestStream, request.EndGetRequestStream, null))
            {
                await stream.WriteAsync(bytes, 0, bytes.Length);
            }

            try
            {
                using (var response = (HttpWebResponse)await Task.Factory.FromAsync<WebResponse>(request.BeginGetResponse, request.EndGetResponse, null))
                {
                    return response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NoContent;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<bool> RemovePinFromBoardAsync(int boardId, int pinId)
        {
            string cleanUrl = NormalizeBaseUrl(StorageService.ServerUrl);
            string url = string.Format("{0}/api/v2/boards/{1}/", cleanUrl, boardId);

            var request = (HttpWebRequest)WebRequest.Create(new Uri(url));
            request.Method = "PATCH";
            request.ContentType = "application/json; charset=utf-8";
            request.Headers["User-Agent"] = "MetroPinry/1.0 (Windows Phone 8.1)";

            if (!string.IsNullOrEmpty(StorageService.AuthToken))
            {
                request.Headers["Authorization"] = "Token " + StorageService.AuthToken;
            }

            string payload = string.Format("{{\"pins_to_remove\":[{0}]}}", pinId);
            byte[] bytes = Encoding.UTF8.GetBytes(payload);

            try
            {
                using (var stream = await Task.Factory.FromAsync<Stream>(request.BeginGetRequestStream, request.EndGetRequestStream, null))
                {
                    await stream.WriteAsync(bytes, 0, bytes.Length);
                }

                using (var response = (HttpWebResponse)await Task.Factory.FromAsync<WebResponse>(request.BeginGetResponse, request.EndGetResponse, null))
                {
                    return response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NoContent;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<bool> DeleteBoardAsync(int boardId)
        {
            string cleanUrl = NormalizeBaseUrl(StorageService.ServerUrl);
            string url = string.Format("{0}/api/v2/boards/{1}/", cleanUrl, boardId);

            var request = (HttpWebRequest)WebRequest.Create(new Uri(url));
            request.Method = "DELETE";
            request.Headers["User-Agent"] = "MetroPinry/1.0 (Windows Phone 8.1)";

            if (!string.IsNullOrEmpty(StorageService.AuthToken))
            {
                request.Headers["Authorization"] = "Token " + StorageService.AuthToken;
            }

            try
            {
                using (var response = (HttpWebResponse)await Task.Factory.FromAsync<WebResponse>(request.BeginGetResponse, request.EndGetResponse, null))
                {
                    return response.StatusCode == HttpStatusCode.NoContent || response.StatusCode == HttpStatusCode.OK;
                }
            }
            catch (WebException wex)
            {
                return false;
            }
        }
    }
}