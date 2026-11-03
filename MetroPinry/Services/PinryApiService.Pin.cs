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
        // ПИНЫ И ПАГИНАЦИЯ ЛЕНТЫ
        // ==========================================

        public async Task<PinResponse> GetRecentPinsAsync()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    AttachAuthToken(client);
                    string url = string.Format("{0}/api/v2/pins/?format=json&ordering=-id&limit=40&offset=0&_nocache={1}",
                        GetBaseUrl(), DateTime.Now.Ticks);

                    string json = await client.GetStringAsync(url);
                    return await Task.Run(() => JsonConvert.DeserializeObject<PinResponse>(json));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("PINS FETCH ERROR: " + ex.Message);
                return new PinResponse { Results = new List<Pin>() };
            }
        }

        public async Task<PinResponse> GetPinsPageAsync(string nextUrl)
        {
            if (string.IsNullOrEmpty(nextUrl)) return null;
            try
            {
                using (var client = new HttpClient())
                {
                    AttachAuthToken(client);
                    string json = await client.GetStringAsync(nextUrl);
                    return await Task.Run(() => JsonConvert.DeserializeObject<PinResponse>(json));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("PINS PAGINATION ERROR: " + ex.Message);
                return null;
            }
        }

        // ==========================================
        // СОЗДАНИЕ, ОБНОВЛЕНИЕ И УДАЛЕНИЕ ПИНОВ
        // ==========================================

        public async Task<bool> UpdatePinAsync(int pinId, string desc, List<string> tags, bool isPrivate = false, int? targetBoardId = null)
        {
            try
            {
                var boardsList = new List<int>();
                if (targetBoardId.HasValue && targetBoardId.Value > 0)
                {
                    boardsList.Add(targetBoardId.Value);
                }

                using (var client = new HttpClient())
                {
                    AttachAuthToken(client);
                    string url = string.Format("{0}/api/v2/pins/{1}/", GetBaseUrl(), pinId);
                    var payload = new
                    {
                        description = desc,
                        tags = tags ?? new List<string>(),
                        is_private = isPrivate,
                        boards = boardsList
                    };
                    string json = JsonConvert.SerializeObject(payload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await client.PutAsync(url, content);
                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UPDATE PIN ERROR: " + ex.Message);
                return false;
            }
        }

        public async Task<bool> CreatePinFromUrlAsync(string url, string desc, List<string> boardsList, List<string> tagsList, bool isPrivate = false, int? targetBoardId = null)
        {
            try
            {
                var boards = new List<int>();
                if (targetBoardId.HasValue && targetBoardId.Value > 0)
                {
                    boards.Add(targetBoardId.Value);
                }
                else if (boardsList != null)
                {
                    foreach (var b in boardsList)
                    {
                        int bId;
                        if (int.TryParse(b, out bId) && bId > 0)
                        {
                            boards.Add(bId);
                        }
                    }
                }

                using (var client = new HttpClient())
                {
                    AttachAuthToken(client);
                    string endpoint = string.Format("{0}/api/v2/pins/?format=json", GetBaseUrl());

                    // В Pinry поле приватности называется "private", а ссылка — "url" или "pin_image"
                    var payload = new Dictionary<string, object>
            {
                { "url", url ?? string.Empty },
                { "pin_image", url ?? string.Empty },
                { "description", desc ?? string.Empty },
                { "tags", tagsList ?? new List<string>() },
                { "private", isPrivate },
                { "is_private", isPrivate },
                { "boards", boards }
            };

                    string json = JsonConvert.SerializeObject(payload);
                    System.Diagnostics.Debug.WriteLine("CREATE PIN REQUEST URL: " + endpoint);
                    System.Diagnostics.Debug.WriteLine("CREATE PIN PAYLOAD: " + json);

                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(endpoint, content);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    System.Diagnostics.Debug.WriteLine("CREATE PIN STATUS: " + response.StatusCode);
                    System.Diagnostics.Debug.WriteLine("CREATE PIN RESPONSE: " + responseBody);

                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("CREATE PIN FROM URL ERROR: " + ex.Message);
                return false;
            }
        }

        public async Task<bool> UploadPinImageAsync(System.IO.Stream imageStream, string desc, List<string> boardsList, List<string> tagsList, bool isPrivate = false, int? targetBoardId = null)
        {
            try
            {
                byte[] imageBytes;
                using (var ms = new System.IO.MemoryStream())
                {
                    imageStream.CopyTo(ms);
                    imageBytes = ms.ToArray();
                }

                string uploadedUrl = await UploadPinImageAsync(imageBytes, "upload.jpg");
                if (string.IsNullOrEmpty(uploadedUrl))
                {
                    return false;
                }

                return await CreatePinFromUrlAsync(uploadedUrl, desc, boardsList, tagsList, isPrivate, targetBoardId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UPLOAD PIN STREAM ERROR: " + ex.Message);
                return false;
            }
        }

        public async Task<string> UploadPinImageAsync(byte[] imageBytes, string fileName)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    AttachAuthToken(client);
                    string url = string.Format("{0}/api/v2/images/", GetBaseUrl());

                    using (var content = new MultipartFormDataContent())
                    {
                        var imageContent = new ByteArrayContent(imageBytes);
                        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                        content.Add(imageContent, "pin_image", fileName);

                        var response = await client.PostAsync(url, content);
                        if (response.IsSuccessStatusCode)
                        {
                            string json = await response.Content.ReadAsStringAsync();
                            var result = JsonConvert.DeserializeObject<ImageUploadResponse>(json);
                            return result != null ? result.Url : null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UPLOAD IMAGE RAW ERROR: " + ex.Message);
            }
            return null;
        }

        public async Task<bool> DeletePinAsync(int id)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    AttachAuthToken(client);
                    string url = string.Format("{0}/api/v2/pins/{1}/", GetBaseUrl(), id);
                    var response = await client.DeleteAsync(url);
                    return response.IsSuccessStatusCode;
                }
            }
            catch { return false; }
        }
    }
}