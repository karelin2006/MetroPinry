using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using MetroPinry.Models;
using Newtonsoft.Json;

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

                    var payload = new Dictionary<string, object>
                    {
                        { "name", name },
                        { "private", isPrivate },
                        { "is_private", isPrivate }
                    };

                    string json = JsonConvert.SerializeObject(payload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(url, content);
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
                    string url = string.Format("{0}/api/v2/boards/{1}/?format=json", GetBaseUrl(), boardId);

                    var payload = new Dictionary<string, object>
                    {
                        { "name", name },
                        { "private", isPrivate },
                        { "is_private", isPrivate }
                    };

                    string json = JsonConvert.SerializeObject(payload);
                    var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    };

                    var response = await client.SendAsync(request);

                    if (response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed)
                    {
                        var putContent = new StringContent(json, Encoding.UTF8, "application/json");
                        response = await client.PutAsync(url, putContent);
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

        // ПРИКРЕПЛЕНИЕ ПИНА К ДОСКЕ ЧЕРЕЗ PATCH /api/v2/boards/{boardId}/
        public async Task<bool> AddPinToBoardAsync(int boardId, int pinId)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    AttachAuthToken(client);
                    string url = string.Format("{0}/api/v2/boards/{1}/", GetBaseUrl(), boardId);

                    var payload = new
                    {
                        pins_to_add = new int[] { pinId }
                    };

                    string json = JsonConvert.SerializeObject(payload);
                    System.Diagnostics.Debug.WriteLine("ADD PIN TO BOARD JSON: " + json + " -> " + url);

                    var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    };

                    var response = await client.SendAsync(request);
                    string respBody = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine("ADD PIN TO BOARD STATUS: " + response.StatusCode + " " + respBody);

                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ADD PIN TO BOARD ERROR: " + ex.Message);
                return false;
            }
        }

        // ОТКРЕПЛЕНИЕ ПИНА ОТ ДОСКИ ЧЕРЕЗ PATCH /api/v2/boards/{boardId}/
        public async Task<bool> RemovePinFromBoardAsync(int boardId, int pinId)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    AttachAuthToken(client);
                    string url = string.Format("{0}/api/v2/boards/{1}/", GetBaseUrl(), boardId);

                    var payload = new
                    {
                        pins_to_remove = new int[] { pinId }
                    };

                    string json = JsonConvert.SerializeObject(payload);
                    System.Diagnostics.Debug.WriteLine("REMOVE PIN FROM BOARD JSON: " + json + " -> " + url);

                    var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    };

                    var response = await client.SendAsync(request);
                    string respBody = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine("REMOVE PIN FROM BOARD STATUS: " + response.StatusCode + " " + respBody);

                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("REMOVE PIN FROM BOARD ERROR: " + ex.Message);
                return false;
            }
        }
    }
}