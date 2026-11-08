using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using MetroPinry.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

        public async Task<bool> UpdatePinAsync(int pinId, string desc, List<string> tags, bool isPrivate = false)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    AttachAuthToken(client);
                    string url = string.Format("{0}/api/v2/pins/{1}/", GetBaseUrl(), pinId);

                    var payload = new
                    {
                        description = desc ?? string.Empty,
                        tags = tags ?? new List<string>(),
                        @private = isPrivate,
                        is_private = isPrivate
                    };

                    string json = JsonConvert.SerializeObject(payload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
                    {
                        Content = content
                    };

                    var response = await client.SendAsync(request);

                    if (!response.IsSuccessStatusCode)
                    {
                        string err = await response.Content.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine("UPDATE PIN FAIL: " + response.StatusCode + " " + err);
                    }

                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UPDATE PIN ERROR: " + ex.Message);
                return false;
            }
        }

        public async Task<bool> CreatePinAsync(string url, int? imageId, string desc, List<string> boardsList, List<string> tagsList, bool isPrivate = false, int? targetBoardId = null)
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
                    string endpoint = string.Format("{0}/api/v2/pins/", GetBaseUrl());

                    var payload = new Dictionary<string, object>
                    {
                        { "description", desc ?? string.Empty },
                        { "tags", tagsList ?? new List<string>() },
                        { "private", isPrivate },
                        { "is_private", isPrivate }
                    };

                    if (!string.IsNullOrEmpty(url))
                    {
                        payload.Add("url", url);
                    }
                    else if (imageId.HasValue)
                    {
                        payload.Add("image_by_id", imageId.Value);
                    }

                    string json = JsonConvert.SerializeObject(payload);
                    System.Diagnostics.Debug.WriteLine("CREATE PIN JSON: " + json);

                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(endpoint, content);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    System.Diagnostics.Debug.WriteLine("CREATE PIN STATUS: " + response.StatusCode);

                    if (response.IsSuccessStatusCode)
                    {
                        // Если выбрана доска, прикрепляем созданный пин через PATCH /api/v2/boards/{boardId}/
                        if (boards.Count > 0)
                        {
                            var jObj = JObject.Parse(responseBody);
                            if (jObj["id"] != null)
                            {
                                int createdPinId = jObj["id"].Value<int>();
                                foreach (int bId in boards)
                                {
                                    await AddPinToBoardAsync(bId, createdPinId);
                                }
                            }
                        }
                        return true;
                    }

                    System.Diagnostics.Debug.WriteLine("CREATE PIN FAIL: " + response.StatusCode + " " + responseBody);
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("CREATE PIN ERROR: " + ex.Message);
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

                int? uploadedImageId = await UploadPinImageAsync(imageBytes, "upload.jpg");

                if (uploadedImageId == null)
                {
                    System.Diagnostics.Debug.WriteLine("UPLOAD PIN STREAM ERROR: Image upload failed");
                    return false;
                }

                return await CreatePinAsync(null, uploadedImageId, desc, boardsList, tagsList, isPrivate, targetBoardId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UPLOAD PIN STREAM ERROR: " + ex.Message);
                return false;
            }
        }

        public async Task<int?> UploadPinImageAsync(byte[] imageBytes, string fileName)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    AttachAuthToken(client);
                    string url = string.Format("{0}/api/v2/images/", GetBaseUrl());
                    string boundary = "----Boundary" + Guid.NewGuid().ToString("N");

                    using (var content = new MultipartFormDataContent(boundary))
                    {
                        var imageContent = new ByteArrayContent(imageBytes);
                        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                        imageContent.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("form-data")
                        {
                            Name = "\"image\"",
                            FileName = "\"" + fileName + "\""
                        };

                        content.Add(imageContent);

                        var response = await client.PostAsync(url, content);
                        if (response.IsSuccessStatusCode)
                        {
                            string json = await response.Content.ReadAsStringAsync();
                            var result = JsonConvert.DeserializeObject<PinImage>(json);
                            return result != null ? (int?)result.Id : null;
                        }
                        else
                        {
                            string err = await response.Content.ReadAsStringAsync();
                            System.Diagnostics.Debug.WriteLine("UPLOAD IMAGE FAIL: " + response.StatusCode + " " + err);
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

        private class PinImage
        {
            [JsonProperty("id")]
            public int Id { get; set; }
        }
    }
}