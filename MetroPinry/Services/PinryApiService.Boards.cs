using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using MetroPinry.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace MetroPinry.Services
{
    public partial class PinryApiService
    {
        // ==========================================
        // ДОСКИ: ВЫБОРКА ПИНОВ КОНКРЕТНОЙ ДОСКИ
        // ==========================================

        public async Task<PinResponse> GetBoardPinsAsync(int boardId, string boardName)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    AttachAuthToken(client);

                    // ТОЧНЫЙ запрос как в веб-интерфейсе Pinry: pins__id={boardId}
                    string url = string.Format("{0}/api/v2/pins/?format=json&ordering=-id&limit=50&offset=0&pins__id={1}&_nocache={2}",
                        GetBaseUrl(), boardId, DateTime.Now.Ticks);

                    System.Diagnostics.Debug.WriteLine("FETCHING BOARD PINS: " + url);

                    string json = await client.GetStringAsync(url);
                    var parsed = await Task.Run(() => JsonConvert.DeserializeObject<PinResponse>(json));

                    if (parsed != null && parsed.Results != null)
                    {
                        return parsed;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GET BOARD PINS ERROR: " + ex.Message);
            }

            return new PinResponse { Results = new List<Pin>() };
        }

        // ==========================================
        // ДОСКИ: СПИСКИ И УПРАВЛЕНИЕ
        // ==========================================

        public async Task<BoardResponse> GetBoardsAsync(string username = null)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    AttachAuthToken(client);

                    string userFilter = !string.IsNullOrEmpty(username)
                        ? string.Format("&submitter__username={0}", Uri.EscapeDataString(username))
                        : string.Empty;

                    string url = string.Format("{0}/api/v2/boards/?format=json&offset=0&limit=50{1}&_nocache={2}",
                        GetBaseUrl(), userFilter, DateTime.Now.Ticks);

                    string json = await client.GetStringAsync(url);
                    return await Task.Run(() => JsonConvert.DeserializeObject<BoardResponse>(json));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("BOARDS FETCH ERROR: " + ex.Message);
                return new BoardResponse { Results = new List<Board>() };
            }
        }

        public async Task<BoardResponse> GetBoardsPageAsync(string nextUrl)
        {
            if (string.IsNullOrEmpty(nextUrl)) return null;
            try
            {
                using (var client = new HttpClient())
                {
                    AttachAuthToken(client);
                    string json = await client.GetStringAsync(nextUrl);
                    return await Task.Run(() => JsonConvert.DeserializeObject<BoardResponse>(json));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("BOARDS PAGINATION ERROR: " + ex.Message);
                return null;
            }
        }

        public async Task<bool> CreateBoardAsync(string name, string description = "", bool isPrivate = false)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    AttachAuthToken(client);
                    string url = string.Format("{0}/api/v2/boards/?format=json", GetBaseUrl());

                    // Отправляем оба варианта поля: "private" и "is_private"
                    var payload = new Dictionary<string, object>
            {
                { "name", name },
                { "private", isPrivate },
                { "is_private", isPrivate }
            };

                    string json = JsonConvert.SerializeObject(payload);
                    System.Diagnostics.Debug.WriteLine(">>> SENDING CREATE BOARD JSON: " + json);

                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(url, content);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    System.Diagnostics.Debug.WriteLine(string.Format(">>> CREATE BOARD STATUS: {0}, RESPONSE: {1}", response.StatusCode, responseBody));

                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("CREATE BOARD ERROR: " + ex.Message);
                return false;
            }
        }

        public async Task<bool> UpdateBoardAsync(int boardId, string name, string description = "", bool isPrivate = false)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    AttachAuthToken(client);
                    // Строго со слешем перед query-параметрами!
                    string url = string.Format("{0}/api/v2/boards/{1}/?format=json", GetBaseUrl(), boardId);

                    var payload = new Dictionary<string, object>
                        {
                            { "name", name },
                            { "private", isPrivate },
                            { "is_private", isPrivate }
                        };

                    string json = JsonConvert.SerializeObject(payload);
                    System.Diagnostics.Debug.WriteLine(">>> SENDING UPDATE BOARD JSON: " + json);

                    // Используем PATCH вместо PUT, чтобы обновить только переданные поля
                    var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    };

                    var response = await client.SendAsync(request);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    System.Diagnostics.Debug.WriteLine(string.Format(">>> UPDATE BOARD STATUS: {0}, BODY: {1}", response.StatusCode, responseBody));

                    // Если бэкенд не поддерживает PATCH (например, 405 Method Not Allowed), пробуем PUT
                    if (response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed)
                    {
                        var putContent = new StringContent(json, Encoding.UTF8, "application/json");
                        response = await client.PutAsync(url, putContent);
                        responseBody = await response.Content.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine(string.Format(">>> FALLBACK PUT BOARD STATUS: {0}, BODY: {1}", response.StatusCode, responseBody));
                    }

                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UPDATE BOARD ERROR: " + ex.Message);
                return false;
            }
        }

        public async Task<bool> DeleteBoardAsync(int id)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    AttachAuthToken(client);
                    string url = string.Format("{0}/api/v2/boards/{1}/", GetBaseUrl(), id);
                    var response = await client.DeleteAsync(url);
                    return response.IsSuccessStatusCode;
                }
            }
            catch { return false; }
        }

        public async Task<bool> AddPinToBoardAsync(int pinId, int boardId)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    AttachAuthToken(client);
                    string url = string.Format("{0}/api/v2/pins/{1}/boards/", GetBaseUrl(), pinId);
                    var payload = new { board_id = boardId };
                    string json = JsonConvert.SerializeObject(payload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(url, content);
                    return response.IsSuccessStatusCode;
                }
            }
            catch { return false; }
        }

        public async Task<bool> RemovePinFromBoardAsync(int pinId, int boardId)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    AttachAuthToken(client);
                    string url = string.Format("{0}/api/v2/pins/{1}/boards/{2}/", GetBaseUrl(), pinId, boardId);
                    var response = await client.DeleteAsync(url);
                    return response.IsSuccessStatusCode;
                }
            }
            catch { return false; }
        }
    }
}