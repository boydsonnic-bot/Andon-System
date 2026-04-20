using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SharedLib.Services
{
    public class ZaloSender
    {
        private readonly HttpClient _http;
        private readonly string _accessToken;
        private readonly string _oaId;

        public ZaloSender(HttpClient http, string accessToken, string oaId)
        {
            _http = http;
            _accessToken = accessToken;
            _oaId = oaId;
        }

        // userId là Zalo user_id hoặc phone tùy cấu hình OA của bạn
        public async Task SendTextAsync(string userId, string text)
        {
            var payload = new
            {
                recipient = new { user_id = userId },
                message = new { text }
            };
            var req = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://openapi.zalo.me/v3.0/oa/message?access_token={_accessToken}")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            var res = await _http.SendAsync(req);
            res.EnsureSuccessStatusCode();
        }
    }
}