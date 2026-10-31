using Newtonsoft.Json;

namespace MetroPinry.Models
{
    public class AuthTokenResponse
    {
        [JsonProperty("token")]
        public string Token { get; set; }
    }
}