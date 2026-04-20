using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace EternalReturnBot
{
    public class ErApiService
    {
        private readonly HttpClient _http;
        public Dictionary<int, string> CharacterMap = new Dictionary<int, string>();

        public ErApiService(HttpClient http) => _http = http;

        // 전역 API 호출 함수 (재시도 및 딜레이 포함)
        public async Task<string> GetAsync(string url)
        {
            await Program.ApiSemaphore.WaitAsync(); // 차례 기다리기
            try
            {
                if (!_http.DefaultRequestHeaders.Contains("x-api-key"))
                    _http.DefaultRequestHeaders.Add("x-api-key", Program.ErApiKey);

                var response = await _http.GetAsync(url);

                if (response.StatusCode == (System.Net.HttpStatusCode)429)
                {
                    await Task.Delay(2000); // 너무 빠르면 2초 쉬고 재시도
                    return await GetAsync(url);
                }

                return await response.Content.ReadAsStringAsync();
            }
            finally
            {
                await Task.Delay(1100); // 무조건 1.1초 대기 후 다음 사람 허용
                Program.ApiSemaphore.Release();
            }
        }

        public async Task UpdateCurrentSeasonAsync()
        {
            try
            {
                var res = await GetAsync("https://open-api.bser.io/v2/data/Season");
                var json = JObject.Parse(res);
                if (json["code"]?.ToString() == "200")
                {
                    foreach (var s in json["data"])
                    {
                        if (s["isCurrent"]?.ToString() == "1")
                        {
                            Program.CurrentSeasonId = s["seasonID"].ToObject<int>();
                            Console.WriteLine($"✅ 시즌 동기화: {s["seasonName"]} (ID:{Program.CurrentSeasonId})");
                            return;
                        }
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"⚠️ 시즌 업데이트 실패: {ex.Message}"); }
        }

        public async Task LoadCharactersAsync()
        {
            if (CharacterMap.Count > 0) return;
            try
            {
                var res = await GetAsync("https://open-api.bser.io/v1/data/Character");
                var json = JObject.Parse(res);
                foreach (var c in json["data"])
                    CharacterMap[(int)c["code"]] = c["name"].ToString();
            }
            catch { }
        }
    }
}