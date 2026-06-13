using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Diplom_Bibl.Services
{
    public class AddressSuggestionService
    {
        private readonly HttpClient _httpClient;
        private const string API_KEY = "fa8268132e66435ee0b5961551ed008c55eb8b9b"; // Получите на dadata.ru
        private const string SECRET_KEY = "e8b4218db8cb51cb5ffceb608c48da1d5da4ffe6";

        public AddressSuggestionService()
        {
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri("https://suggestions.dadata.ru/suggestions/api/4_1/rs/");
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Token", API_KEY);
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<List<string>> SuggestAddress(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 3)
                return new List<string>();

            var request = new
            {
                query = query,
                count = 10,
                language = "ru"
            };

            var content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync("suggest/address", content);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<AddressSuggestionResponse>(json);
                return result?.Suggestions?.Select(s => s.Value).ToList() ?? new List<string>();
            }

            return new List<string>();
        }
    }

    public class AddressSuggestionResponse
    {
        public List<AddressSuggestion> Suggestions { get; set; }
    }

    public class AddressSuggestion
    {
        public string Value { get; set; }
        public string UnrestrictedValue { get; set; }
        public object Data { get; set; }
    }
}