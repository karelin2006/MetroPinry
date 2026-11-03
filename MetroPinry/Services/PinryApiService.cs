using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using MetroPinry.Models;
using Newtonsoft.Json;

namespace MetroPinry.Services
{
    public partial class PinryApiService
    {
        private string GetBaseUrl()
        {
            string baseServer = (StorageService.ServerUrl ?? string.Empty).TrimEnd('/');
            if (baseServer.EndsWith("/api/v2", StringComparison.OrdinalIgnoreCase))
            {
                baseServer = baseServer.Substring(0, baseServer.Length - 7);
            }
            return baseServer;
        }

        private void AttachAuthToken(HttpClient client)
        {
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            if (!string.IsNullOrEmpty(StorageService.AuthToken))
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Token", StorageService.AuthToken);
            }
        }

        // ==========================================
        // ПРОФИЛЬ ТЕКУЩЕГО ПОЛЬЗОВАТЕЛЯ
        // ==========================================

        public async Task<UserProfile> GetCurrentUserAsync()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    AttachAuthToken(client);
                    string url = string.Format("{0}/api/v2/profile/users/?format=json&_nocache={1}", GetBaseUrl(), DateTime.Now.Ticks);

                    string json = await client.GetStringAsync(url);
                    if (string.IsNullOrWhiteSpace(json)) return null;

                    string trimmed = json.Trim();

                    if (trimmed.StartsWith("["))
                    {
                        var usersList = await Task.Run(() => JsonConvert.DeserializeObject<List<UserProfile>>(trimmed));
                        if (usersList != null && usersList.Count > 0)
                        {
                            return usersList[0];
                        }
                    }
                    else
                    {
                        return await Task.Run(() => JsonConvert.DeserializeObject<UserProfile>(trimmed));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("USER PROFILE ERROR: " + ex.Message);
            }
            return null;
        }

        // ==========================================
        // ВХОД
        // ==========================================

        public async Task<UserProfile> LoginWithCredentialsAsync(string serverUrl, string username, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(serverUrl)) return null;

                string safeServer = serverUrl.Trim().TrimEnd('/');
                if (!safeServer.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !safeServer.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    safeServer = "https://" + safeServer;
                }

                if (safeServer.EndsWith("/api/v2", StringComparison.OrdinalIgnoreCase))
                {
                    safeServer = safeServer.Substring(0, safeServer.Length - 7);
                }

                string tokenUrl = safeServer + "/api/v2/profile/login/";
                var serverUri = new Uri(safeServer);

                var cookieContainer = new System.Net.CookieContainer();
                var handler = new HttpClientHandler
                {
                    AllowAutoRedirect = true,
                    CookieContainer = cookieContainer,
                    UseCookies = true
                };

                using (var client = new HttpClient(handler))
                {
                    client.Timeout = TimeSpan.FromSeconds(20);
                    client.DefaultRequestHeaders.Add("Referer", safeServer + "/");
                    client.DefaultRequestHeaders.Add("Origin", safeServer);
                    client.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");

                    string csrfToken = null;
                    string[] warmupPaths = new string[] { "/admin/login/", "/api-auth/login/", "/api/v2/profile/login/", "/api/v2/profile/", "/" };

                    foreach (var path in warmupPaths)
                    {
                        try
                        {
                            var warmupResp = await client.GetAsync(safeServer + path);
                            var cookies = cookieContainer.GetCookies(serverUri);
                            foreach (System.Net.Cookie c in cookies)
                            {
                                if (string.Equals(c.Name, "csrftoken", StringComparison.OrdinalIgnoreCase))
                                {
                                    csrfToken = c.Value;
                                    break;
                                }
                            }
                            if (!string.IsNullOrEmpty(csrfToken)) break;

                            IEnumerable<string> setCookies;
                            if (warmupResp.Headers.TryGetValues("Set-Cookie", out setCookies))
                            {
                                foreach (var header in setCookies)
                                {
                                    var match = System.Text.RegularExpressions.Regex.Match(header, @"csrftoken=([a-zA-Z0-9]+)");
                                    if (match.Success)
                                    {
                                        csrfToken = match.Groups[1].Value;
                                        cookieContainer.Add(serverUri, new System.Net.Cookie("csrftoken", csrfToken, "/", serverUri.Host));
                                        break;
                                    }
                                }
                            }
                            if (!string.IsNullOrEmpty(csrfToken)) break;
                        }
                        catch { }
                    }

                    if (string.IsNullOrEmpty(csrfToken))
                    {
                        csrfToken = Guid.NewGuid().ToString("N");
                        cookieContainer.Add(serverUri, new System.Net.Cookie("csrftoken", csrfToken, "/", serverUri.Host));
                    }

                    client.DefaultRequestHeaders.Add("X-CSRFToken", csrfToken);

                    var payload = new { username = username, password = password };
                    string jsonBody = JsonConvert.SerializeObject(payload);
                    var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(tokenUrl, content);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var tokenData = await Task.Run(() => JsonConvert.DeserializeObject<TokenResponse>(responseBody));
                        string token = tokenData != null ? tokenData.GetToken() : null;

                        if (!string.IsNullOrEmpty(token))
                        {
                            StorageService.AuthToken = token;
                            StorageService.ServerUrl = safeServer;

                            var profile = await GetCurrentUserAsync();
                            if (profile != null)
                            {
                                profile.Token = token;
                                return profile;
                            }

                            return new UserProfile
                            {
                                Username = username,
                                Token = token
                            };
                        }
                    }
                    else
                    {
                        string userMessage = "Неверное имя пользователя или пароль.";
                        try
                        {
                            var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseBody);
                            if (dict != null)
                            {
                                if (dict.ContainsKey("password")) userMessage = dict["password"].ToString();
                                else if (dict.ContainsKey("detail")) userMessage = dict["detail"].ToString();
                                else if (dict.ContainsKey("non_field_errors")) userMessage = dict["non_field_errors"].ToString();
                            }
                        }
                        catch
                        {
                            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                            {
                                userMessage = "Ошибка 403: сервер отклонил CSRF токен.";
                            }
                        }

                        throw new Exception(userMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("LOGIN EXCEPTION: " + ex.Message);
                throw;
            }

            return null;
        }

        // ==========================================
        // РЕГИСТРАЦИЯ С CSRF-ЗАЩИТОЙ
        // ==========================================

        public async Task<bool> RegisterWithCredentialsAsync(string serverUrl, string username, string email, string password, string passwordRepeat)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(serverUrl)) return false;

                string safeServer = serverUrl.Trim().TrimEnd('/');
                if (!safeServer.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !safeServer.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    safeServer = "https://" + safeServer;
                }

                if (safeServer.EndsWith("/api/v2", StringComparison.OrdinalIgnoreCase))
                {
                    safeServer = safeServer.Substring(0, safeServer.Length - 7);
                }

                string regUrl = safeServer + "/api/v2/profile/users/";
                var serverUri = new Uri(safeServer);

                var cookieContainer = new System.Net.CookieContainer();
                var handler = new HttpClientHandler
                {
                    AllowAutoRedirect = true,
                    CookieContainer = cookieContainer,
                    UseCookies = true
                };

                using (var client = new HttpClient(handler))
                {
                    client.Timeout = TimeSpan.FromSeconds(20);
                    client.DefaultRequestHeaders.Add("Referer", safeServer + "/");
                    client.DefaultRequestHeaders.Add("Origin", safeServer);
                    client.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");

                    string csrfToken = null;
                    string[] warmupPaths = new string[] { "/admin/login/", "/api-auth/login/", "/api/v2/profile/login/", "/api/v2/profile/", "/" };

                    foreach (var path in warmupPaths)
                    {
                        try
                        {
                            var warmupResp = await client.GetAsync(safeServer + path);
                            var cookies = cookieContainer.GetCookies(serverUri);
                            foreach (System.Net.Cookie c in cookies)
                            {
                                if (string.Equals(c.Name, "csrftoken", StringComparison.OrdinalIgnoreCase))
                                {
                                    csrfToken = c.Value;
                                    break;
                                }
                            }
                            if (!string.IsNullOrEmpty(csrfToken)) break;
                        }
                        catch { }
                    }

                    if (string.IsNullOrEmpty(csrfToken))
                    {
                        csrfToken = Guid.NewGuid().ToString("N");
                        cookieContainer.Add(serverUri, new System.Net.Cookie("csrftoken", csrfToken, "/", serverUri.Host));
                    }

                    client.DefaultRequestHeaders.Add("X-CSRFToken", csrfToken);

                    var payload = new
                    {
                        username = username,
                        email = email,
                        password = password,
                        password_repeat = passwordRepeat
                    };

                    string jsonBody = JsonConvert.SerializeObject(payload);
                    var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(regUrl, content);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }
                    else
                    {
                        string msg = "Ошибка регистрации на сервере.";
                        try
                        {
                            var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseBody);
                            if (dict != null)
                            {
                                var sb = new StringBuilder();
                                foreach (var kvp in dict)
                                {
                                    sb.AppendLine(string.Format("{0}: {1}", kvp.Key, kvp.Value));
                                }
                                if (sb.Length > 0) msg = sb.ToString().Trim();
                            }
                        }
                        catch { }
                        throw new Exception(msg);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("REGISTRATION EXCEPTION: " + ex.Message);
                throw;
            }
        }

        private class TokenResponse
        {
            [JsonProperty("token")]
            public string Token { get; set; }

            [JsonProperty("key")]
            public string Key { get; set; }

            [JsonProperty("auth_token")]
            public string AuthToken { get; set; }

            public string GetToken()
            {
                return Token ?? Key ?? AuthToken;
            }
        }

        public class ImageUploadResponse
        {
            [JsonProperty("url")]
            public string Url { get; set; }
        }
    }
}