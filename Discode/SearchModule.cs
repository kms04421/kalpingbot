using Discord;
using Discord.Commands;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace EternalReturnBot
{
    public class SearchModule : ModuleBase<SocketCommandContext>
    {
        private readonly ErApiService _api;
        private readonly HttpClient _http;

        // [사용자 데이터] 장인 리스트
        private static readonly Dictionary<int, string> _artisanMap = new Dictionary<int, string>
        {
             {1, "트수급백수" }, {2,"KATIIYA" }, {4,"전쟁망치" }, {5,"Twitch오플몬" },
             {10,"KimShy" }, {13,"물냉파" }, {29,"준봉이" }, {30,"TwitchNunuzz" },
             {36,"Stormchaser" }, {39,"CAMILOKING" }, {48,"영만" }, {57,"ND989" },
             {63,"마돌체" }, {75,"kapum" }, {78,"초운" }, {84,"그녀의내장은파랑"},
             {74,"FreeSen" }, {79,"RIOORI" }, {27,"Complex" }, {31,"할수있다" },
             {58,"찍먹충" }, {26,"조용히겜만함" },{55,"voider26"},{21,"민환2"},{17,"이녀석은혼모노다"},{62,"소와소"},
             {22,"똥덩어리카르미스"},{73,"초재앙샬럿러버"},{42,"갈모매"},{33,"Glove"}
        };

        public SearchModule(ErApiService api, HttpClient http)
        {
            _api = api;
            _http = http;
        }
        [Command("전적")]
        public async Task GetStats([Remainder] string nickname)
        {
            await _api.LoadCharactersAsync();
            var msg = await ReplyAsync($"🔍 **{nickname}** 님의 전적을 분석 중입니다...");

            try
            {
                var uRes = await _api.GetAsync($"https://open-api.bser.io/v1/user/nickname?query={Uri.EscapeDataString(nickname.Trim())}");
                var uJson = JObject.Parse(uRes);
                if (uJson["code"]?.ToString() != "200") { await msg.ModifyAsync(x => x.Content = "❌ 유저를 찾을 수 없습니다."); return; }
                string uid = uJson["user"]["userId"].ToString();

                List<JToken> rankedGames = new List<JToken>();
                string nextId = "";
                for (int i = 0; i < 10; i++)
                {
                    var gRes = await _api.GetAsync($"https://open-api.bser.io/v1/user/games/uid/{uid}?next={nextId}");
                    var gJson = JObject.Parse(gRes);
                    if (gJson["userGames"] != null)
                    {
                        foreach (var g in gJson["userGames"])
                        {
                            if (g["matchingMode"]?.ToString() == "3") { rankedGames.Add(g); if (rankedGames.Count >= 10) break; }
                        }
                    }
                    if (rankedGames.Count >= 10 || gJson["next"] == null) break;
                    nextId = gJson["next"].ToString();
                }

                if (rankedGames.Count == 0) { await msg.ModifyAsync(x => x.Content = "⚠️ 최근 랭크 기록이 없습니다."); return; }

                double sumDmg = 0, sumHunt = 0, sumTK = 0, sumRank = 0, sumTaken = 0, sumVision = 0;
                int totalRpGain = 0;
                int wins = 0;

                var sb = new StringBuilder();
                // 🌟 표 헤더 정렬 왼쪽으로 조정
                sb.AppendLine("```ansi\n순위 험체   K/A    RP   딜량\n" + new string('─', 28));

                foreach (var g in rankedGames)
                {
                    int charCode = (int)g["characterNum"];
                    int rpGain = (int)g["mmrGain"];
                    int rank = (int)g["gameRank"];
                    int k = (int)g["playerKill"];
                    int a = (int)g["playerAssistant"];
                    int d = (int)g["damageToPlayer"];

                    totalRpGain += rpGain;
                    if (rank == 1) wins++;

                    sumDmg += (double)d;
                    sumHunt += (double)g["monsterKill"];
                    sumTK += (k + a);
                    sumRank += rank;
                    sumTaken += (double)g["damageFromPlayer"];
                    sumVision += (int)g["addSurveillanceCamera"] + (int)g["addTelephotoCamera"];

                    string charFullName = _api.CharacterMap.GetValueOrDefault(charCode, "??");
                    string charName = charFullName.Length >= 2 ? charFullName.Substring(0, 2) : charFullName.PadRight(2);

                    // 🌟 데이터 열 간격도 헤더에 맞춰 왼쪽으로 당김
                    string line = $"#{rank,-1}  {charName}  {k,1}/{a,-1}  {rpGain,+4}  {d,6}\n";

                    if (sb.Length + line.Length > 1800) break;
                    sb.Append(line);
                }
                sb.AppendLine("```");

                int cnt = rankedGames.Count;
                double avgDmg = sumDmg / cnt;
                double avgHunt = sumHunt / cnt;
                double avgTK = sumTK / cnt;
                double avgRank = sumRank / cnt;
                double avgTaken = sumTaken / cnt;
                double avgVision = sumVision / cnt;

                int scDmg = (int)Math.Min((avgDmg / 25000.0) * 100, 100);
                int scHunt = (int)Math.Min((avgHunt / 50.0) * 100, 100);
                int scVision = (int)Math.Min((avgVision / 12.0) * 100, 100);
                int scTK = (int)Math.Min((avgTK / 10.0) * 100, 100);
                int scTaken = (int)Math.Min((avgTaken / 25000.0) * 100, 100);
                int scRank = (int)Math.Max(((8.0 - avgRank) / 7.0) * 100, 10);

                // 레이블 텍스트 길이를 맞추어 차트 쏠림 방지
                string chartConfig = $"{{\"type\":\"radar\",\"data\":{{\"labels\":[\"딜({avgDmg:N0})\",\"야동({avgHunt:F1})\",\"시야({avgVision:F1})\",\"K+A({avgTK:F1})\",\"피해({avgTaken:N0})\",\"순위(#{avgRank:F1})\"],\"datasets\":[{{\"label\":\"Performance\",\"backgroundColor\":\"rgba(255,99,132,0.5)\",\"borderColor\":\"rgb(255,99,132)\",\"pointRadius\":3,\"data\":[{scDmg},{scHunt},{scVision},{scTK},{scTaken},{scRank}]}}]}},\"options\":{{\"legend\":{{\"display\":false}},\"scale\":{{\"pointLabels\":{{\"fontSize\":20,\"fontColor\":\"#FFD700\",\"fontStyle\":\"bold\"}},\"ticks\":{{\"beginAtZero\":true,\"max\":100,\"display\":false}},\"gridLines\":{{\"color\":\"rgba(255,255,255,0.3)\"}}}}}}}}";
                string chartUrl = $"https://quickchart.io/chart?c={Uri.EscapeDataString(chartConfig)}&w=500&h=450";

                var embed = new EmbedBuilder()
                    .WithTitle($"🏆 {nickname} 님의 랭크 리포트")
                    .WithUrl($"https://dak.gg/er/players/{Uri.EscapeDataString(nickname.Trim())}")
                    .WithDescription(sb.ToString())
                    .AddField("📊 최근 10판 성과", $"획득 RP: **{(totalRpGain >= 0 ? "+" : "")}{totalRpGain} RP**\n승률: **{wins * 10}%**", true)
                    .AddField("⚔️ 평균 딜량", $"**{avgDmg:N0}**", true)
                    .WithImageUrl(chartUrl)
                    .WithColor(totalRpGain >= 0 ? Color.Blue : Color.Red)
                    .WithFooter("데이터: BSER API | 분석: 칼핑봇")
                    .Build();

                await msg.ModifyAsync(x => { x.Content = ""; x.Embed = embed; });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                await msg.ModifyAsync(x => x.Content = $"💥 에러 발생: {ex.Message}");
            }
        }
        // ---------------------------------------------------------------------
        // 2. !장인비교 (13단 스캔 엔진 + 하이도 에디션 탑재)
        // ---------------------------------------------------------------------
        [Command("장인비교", RunMode = RunMode.Async)]
        [Alias("피드백", "장인")]
        public async Task CompareArtisan([Remainder] string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname)) return;
            await _api.LoadCharactersAsync();
            var msg = await ReplyAsync($"🔄 **{nickname}** 님의 전적을 수집 중입니다... (과부하 방지 가동 중)");

            try
            {
                // 1. 유저 ID 찾기
                string userRes = await _api.GetAsync("https://open-api.bser.io/v1/user/nickname?query=" + Uri.EscapeDataString(nickname.Trim()));
                var userJson = JObject.Parse(userRes);

                if (userJson["code"]?.ToString() != "200")
                {
                    await msg.ModifyAsync(x => x.Content = $"❌ {nickname} 님을 찾을 수 없습니다.");
                    return;
                }

                string userId = userJson["user"]["userId"].ToString();

                // 2. 최근 전적 수집 (최대 20판)
                List<JToken> rankedGames = new List<JToken>();
                string nextId = "";

                for (int i = 0; i < 3; i++)
                {
                    string gUrl = "https://open-api.bser.io/v1/user/games/uid/" + userId;
                    if (!string.IsNullOrEmpty(nextId)) gUrl += "?next=" + Uri.EscapeDataString(nextId);

                    var gRes = await _api.GetAsync(gUrl);
                    var gJson = JObject.Parse(gRes);

                    if (gJson["userGames"] != null)
                    {
                        foreach (var g in gJson["userGames"])
                            if (g["matchingMode"]?.ToString() == "3") rankedGames.Add(g);
                    }
                    if (rankedGames.Count >= 20 || gJson["next"] == null) break;
                    nextId = gJson["next"].ToString();
                }

                if (rankedGames.Count < 3)
                {
                    await msg.ModifyAsync(x => x.Content = "⚠️ 최근 랭크 기록이 너무 적습니다.");
                    return;
                }

                // 3. 모스트 캐릭터 통계 계산
                var mostGroup = rankedGames.GroupBy(g => (int)g["characterNum"]).OrderByDescending(g => g.Count()).First();
                int mostCode = mostGroup.Key;
                string charName = _api.CharacterMap.GetValueOrDefault(mostCode, $"#{mostCode}");

                double uDmg = 0, uHunt = 0, uTK = 0, uRank = 0, uTaken = 0, uVision = 0, uEscape = 0, uLegendary = 0, uSupport = 0, uRescue = 0, uDeath = 0;
                foreach (var g in mostGroup)
                {
                    uDmg += g["damageToPlayer"]?.ToObject<double>() ?? 0;
                    uHunt += g["monsterKill"]?.ToObject<double>() ?? 0;
                    uTK += (g["playerKill"]?.ToObject<double>() ?? 0) + (g["playerAssistant"]?.ToObject<double>() ?? 0);
                    uRank += g["gameRank"]?.ToObject<double>() ?? 8;
                    uTaken += g["damageFromPlayer"]?.ToObject<double>() ?? 0;
                    uVision += (g["addSurveillanceCamera"]?.ToObject<double>() ?? 0) + (g["addTelephotoCamera"]?.ToObject<double>() ?? 0);
                    uEscape += (g["escapeState"]?.ToString() == "1") ? 1 : 0;
                    uLegendary += g["craftLegendary"]?.ToObject<double>() ?? 0;
                    uSupport += (g["healAmount"]?.ToObject<double>() ?? 0) + (g["protectAbsorb"]?.ToObject<double>() ?? 0);
                    uRescue += (g["rescueCount"]?.ToObject<double>() ?? 0) + (g["useRevivalConsole"]?.ToObject<double>() ?? 0);
                    uDeath += g["playerDeaths"]?.ToObject<double>() ?? 0;
                }

                int count = mostGroup.Count();
                uDmg /= count; uHunt /= count; uTK /= count; uRank /= count; uTaken /= count;
                uVision /= count; uLegendary /= count; uSupport /= count; uRescue /= count; uDeath /= count;
                uEscape = (uEscape / count) * 100;
                double uCredit = uLegendary * 350;

                // 4. 장인 데이터 및 역할군 판정
                string role = GetCharacterRole(mostCode);
                double aDmg = 18500, aHunt = 62, aTK = 6.0, aRank = 3.5, aTaken = 18000;
                
                string artisanNickname = _artisanMap.GetValueOrDefault(mostCode);
                string targetText = "데이터 기반 직업군 평균";

                if (!string.IsNullOrEmpty(artisanNickname))
                {
                    await msg.ModifyAsync(x => x.Content = $"👑 장인 **{artisanNickname}** 님의 전적을 실시간 대조 중...");
                    var artisanStats = await FetchArtisanStatsAsync(artisanNickname, mostCode);
                    if (artisanStats != null)
                    {
                        aDmg = artisanStats.Value.Dmg; aHunt = artisanStats.Value.Hunt;
                        aTK = artisanStats.Value.TK; aRank = artisanStats.Value.Rank;
                        targetText = $"👑 장인 ({artisanNickname}) 실시간";
                    }
                }

                // 5. AI 피드백 엔진 가동
                await msg.ModifyAsync(x => x.Content = $"🧠 [{role}] 장인 스탯과 1:1 대조 분석 중...");
                string aiFeedback = await GetAIFeedbackAsync(charName, nickname, role, uDmg, uHunt, uTK, uRank, uTaken, uCredit, uVision, uEscape, uLegendary, uSupport, uRescue, aDmg, aHunt, aTK, aRank, aTaken, uDeath);

                var embed = new EmbedBuilder()
                    .WithTitle($"📊 루미아 섬 전술 리포트 : {nickname}")
                    .WithUrl($"https://dak.gg/er/players/{Uri.EscapeDataString(nickname)}")
                    .WithDescription($"**모스트 실험체:** `{charName}`\n**판정 포지션:** {role}\n**비교 대상:** `{targetText}`\n\n**[AI 팩트 폭격]**\n{aiFeedback}")
                    .WithColor(uRank <= 3.5 ? Color.Green : Color.Orange)
                    .AddField("⚔️ 무력/성장", $"평딜: {uDmg:N0}\n동물: {uHunt:N1}\n킬관여: {uTK:F1}", true)
                    .AddField("🛡️ 생존/운영", $"평순: #{uRank:F1}\n시야: {uVision:F1}\n전설: {uLegendary:F1}", true)
                    .WithFooter("데이터: BSER 실시간 API | 분석: 칼핑봇")
                    .Build();

                await msg.ModifyAsync(x => { x.Content = ""; x.Embed = embed; });
            }
            catch (Exception ex)
            {
                await msg.ModifyAsync(x => x.Content = $"💥 오류 발생: {ex.Message}");
            }
        }

        // ---------------------------------------------------------------------
        // 3. !티어측정 (최근 100판 중 랭크 10판 탐색 완료 시 즉시 반환)
        // ---------------------------------------------------------------------
        [Command("티어측정", RunMode = RunMode.Async)]
        public async Task PredictTier([Remainder] string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname)) return;

            var msg = await ReplyAsync($"🔮 **{nickname}** 님의 전투력을 측정 중입니다... (랭크 최근 10판을 찾습니다!)");

            using (var localClient = new HttpClient())
            {
                localClient.DefaultRequestHeaders.Add("x-api-key", Program.ErApiKey);

                try
                {
                    string cleanNick = nickname.Trim();
                    string safeNick = Uri.EscapeDataString(cleanNick);
                    string userUrl = "https://open-api.bser.io/v1/user/nickname?query=" + safeNick;

                    var uRes = await localClient.GetStringAsync(userUrl);
                    var uJson = Newtonsoft.Json.Linq.JObject.Parse(uRes);

                    if (uJson["code"]?.ToString() != "200")
                    {
                        await msg.ModifyAsync(x => x.Content = $"❌ {cleanNick} 님을 찾을 수 없습니다.");
                        return;
                    }
                    string userId = uJson["user"]["userId"].ToString();
                    await Task.Delay(1000);

                    List<Newtonsoft.Json.Linq.JToken> rankedGames = new List<Newtonsoft.Json.Linq.JToken>();
                    string nextId = "";
                    int currentMMR = 0;

                    // 🌟 최대 10페이지(100판) 스캔. 단, 랭크 10판이 모이면 즉시 탈출
                    for (int i = 0; i < 10; i++)
                    {
                        string gamesUrl = "https://open-api.bser.io/v1/user/games/uid/" + userId;
                        if (!string.IsNullOrEmpty(nextId)) gamesUrl += "?next=" + Uri.EscapeDataString(nextId);

                        var gRes = await localClient.GetStringAsync(gamesUrl);
                        var gJson = Newtonsoft.Json.Linq.JObject.Parse(gRes);

                        if (gJson["userGames"] != null)
                        {
                            foreach (var game in gJson["userGames"])
                            {
                                if (game["matchingMode"]?.ToString() == "3")
                                {
                                    rankedGames.Add(game);
                                    if (currentMMR == 0) currentMMR = game["mmrAfter"]?.ToObject<int>() ?? 0;

                                    // 10판 모이면 for문 전체를 끝내기 위해 break
                                    if (rankedGames.Count >= 10) break;
                                }
                            }
                        }

                        // 랭크 10판을 채웠거나, 더 이상 다음 기록이 없으면 반복 종료
                        if (rankedGames.Count >= 10 || gJson["next"] == null) break;

                        nextId = gJson["next"].ToString();
                        await Task.Delay(1000); // API Rate Limit 보호
                    }

                    if (rankedGames.Count < 3)
                    {
                        await msg.ModifyAsync(x => x.Content = $"⚠️ 최근 100판의 전적을 뒤졌지만 랭크 기록이 부족합니다. (현재 {rankedGames.Count}판 찾음)");
                        return;
                    }

                    // =========================================================
                    // 3. 퍼포먼스 분석 및 📈 MMR / 퍼포먼스 데이터 추출
                    // =========================================================
                    double sumDmg = 0, sumTK = 0;
                    int sumRank = 0;

                    List<int> graphMMRs = new List<int>();
                    List<int> graphPerfs = new List<int>();

                    var chronGames = rankedGames.AsEnumerable().Reverse().ToList();

                    foreach (var game in chronGames)
                    {
                        int rnk = game["gameRank"]?.ToObject<int>() ?? 8;
                        int tk = (game["playerKill"]?.ToObject<int>() ?? 0) + (game["playerAssistant"]?.ToObject<int>() ?? 0);
                        int dmg = game["damageToPlayer"]?.ToObject<int>() ?? 0;
                        int mmr = game["mmrAfter"]?.ToObject<int>() ?? 0;

                        double singlePerf = (dmg * 0.3) + (tk * 850) + ((8.0 - rnk) * 1200);
                        singlePerf = Math.Max(0, singlePerf);

                        graphMMRs.Add(mmr);
                        graphPerfs.Add((int)singlePerf);
                    }

                    foreach (var game in rankedGames)
                    {
                        sumDmg += game["damageToPlayer"]?.ToObject<int>() ?? 0;
                        sumTK += (game["playerKill"]?.ToObject<int>() ?? 0) + (game["playerAssistant"]?.ToObject<int>() ?? 0);
                        sumRank += game["gameRank"]?.ToObject<int>() ?? 0;
                    }

                    double avgDmg = sumDmg / rankedGames.Count;
                    double avgTK = sumTK / rankedGames.Count;
                    double avgRank = (double)sumRank / rankedGames.Count;

                    double combatScore = (avgDmg * 0.3) + (avgTK * 850) + ((8.0 - avgRank) * 1200);
                    combatScore += (currentMMR * 0.7);

                    if (avgTK < 4.0 && avgDmg < 8000) combatScore -= 1500;
                    if (avgRank >= 5.5) combatScore -= 1500;
                    if (avgTK < 3.0 && avgDmg < 6000) combatScore -= 2000;

                    combatScore = Math.Max(0, combatScore);

                    // =========================================================
                    // 4. 결과 티어 및 코멘트 산출
                    // =========================================================
                    string predictedTier;
                    string comment;
                    Color color;

                    if (combatScore >= 27000) { predictedTier = "👑 이터니티 (Eternity)"; comment = "프로게이머 제의 안 오나요?"; color = new Color(148, 0, 211); }
                    else if (combatScore >= 24000) { predictedTier = "👹 데미갓 (Demigod)"; comment = "이터니티가 코앞입니다."; color = new Color(220, 20, 60); }
                    else if (combatScore >= 21000) { predictedTier = "🟣 미스릴 (Mithril)"; comment = "일반인 중에서 적수가 거의 없습니다."; color = Color.Magenta; }
                    else if (combatScore >= 18000) { predictedTier = "☄️ 메테오라이트 (Meteorite)"; comment = "다이아의 벽을 깼습니다!"; color = new Color(75, 0, 130); }
                    else if (combatScore >= 15000) { predictedTier = "💎 다이아몬드 (Diamond)"; comment = "상위권의 상징! 피지컬이 확실하네요."; color = Color.Blue; }
                    else if (combatScore >= 12000) { predictedTier = "💠 플래티넘 (Platinum)"; comment = "숙련자입니다. 뉴비 탈출은 진작에 했군요."; color = new Color(0, 255, 255); }
                    else if (combatScore >= 9000) { predictedTier = "🥇 골드 (Gold)"; comment = "사관후보생 스킨 받기 딱 좋은 실력입니다."; color = Color.Gold; }
                    else if (combatScore >= 6000) { predictedTier = "🥈 실버 (Silver)"; comment = "기본기는 잡혀있네요."; color = Color.LightGrey; }
                    else { predictedTier = "🥉 브론즈/아이언"; comment = "아직 배우는 단계! 맞으면서 크는 겁니다."; color = new Color(205, 127, 50); }

                    string currentTierName = $"{currentMMR} RP";
                    if (currentMMR >= 8120) currentTierName = "👑 이터니티";
                    else if (currentMMR >= 8100) currentTierName = "👹 데미갓";
                    else if (currentMMR >= 7400) currentTierName = "🟣 미스릴";
                    else if (currentMMR >= 6400) currentTierName = "☄️ 메테오라이트";
                    else if (currentMMR >= 5000) currentTierName = "💎 다이아몬드";
                    else if (currentMMR >= 4000) currentTierName = "💠 플래티넘";
                    else if (currentMMR >= 3000) currentTierName = "🥇 골드";
                    else if (currentMMR >= 2000) currentTierName = "🥈 실버";
                    else if (currentMMR >= 1000) currentTierName = "🥉 브론즈";
                    else currentTierName = "🧱 아이언";

                    double expectedScore = (currentMMR * 2.0) + 6000;
                    string gapDesc = "";

                    if (combatScore >= 21000 && currentMMR < 4500)
                        gapDesc = $"### 🚨 생태계 파괴자 (양학 금지)\n실력은 **천상계**인데 일부러 아래에서 현지인들을 괴롭히고 계십니까? 빨리 위로 올라가세요!\n\n{comment}";
                    else if (combatScore > expectedScore + 2000)
                        gapDesc = $"### 📈 떡상 열차 탑승!\n현재 구간에 있을 실력이 아닙니다. 조금만 더 돌리면 금방 다음 티어로 올라가시겠네요!\n\n{comment}";
                    else if (combatScore < expectedScore - 2500)
                        gapDesc = $"### 📉 거품 경보 (버스 판독기)\n솔직히 말씀드릴게요. 지금 티어는 본인 실력에 비해 조금 과분합니다. 강등당하기 전에 아래 그래프를 보며 반성하세요!\n\n{comment}";
                    else if (Math.Abs(combatScore - expectedScore) <= 1000)
                        gapDesc = $"### 🔒 지독한 현지인\n더도 말고 덜도 말고 딱 지금 티어가 당신의 실력입니다. 진정한 '수문장'이시군요.\n\n{comment}";
                    else
                        gapDesc = $"### 👌 무난한 1인분\n현재 티어에서 밥값은 충분히 하고 있습니다.\n\n{comment}";

                    string mmrDataStr = string.Join(",", graphMMRs);
                    string perfDataStr = string.Join(",", graphPerfs);
                    string labelsStr = string.Join(",", Enumerable.Range(1, graphMMRs.Count).Select(x => $"'{x}'"));

                    string chartConfig = $@"{{
                        type: 'bar',
                        data: {{
                            labels: [{labelsStr}],
                            datasets: [
                                {{
                                    type: 'line',
                                    label: '점수 (MMR)',
                                    data: [{mmrDataStr}],
                                    borderColor: '#FF6384',
                                    backgroundColor: 'rgba(255, 99, 132, 0)',
                                    borderWidth: 3,
                                    fill: false,
                                    yAxisID: 'y'
                                }},
                                {{
                                    type: 'bar',
                                    label: '단일 퍼포먼스 (전투력)',
                                    data: [{perfDataStr}],
                                    backgroundColor: 'rgba(54, 162, 235, 0.6)',
                                    yAxisID: 'y1'
                                }}
                            ]
                        }},
                        options: {{
                            title: {{ display: true, text: '최근 {graphMMRs.Count}판 MMR 및 퍼포먼스 (왼쪽: 과거 -> 오른쪽: 최근)', fontColor: '#FFFFFF', fontSize: 14 }},
                            legend: {{ labels: {{ fontColor: '#FFFFFF', fontSize: 12, fontStyle: 'bold' }} }},
                            scales: {{
                                yAxes: [
                                    {{
                                        id: 'y',
                                        position: 'left',
                                        ticks: {{ fontColor: '#FFFFFF' }},
                                        gridLines: {{ color: 'rgba(255, 255, 255, 0.2)' }}
                                    }},
                                    {{
                                        id: 'y1',
                                        position: 'right',
                                        ticks: {{ beginAtZero: true, fontColor: '#FFFFFF' }},
                                        gridLines: {{ display: false }}
                                    }}
                                ],
                                xAxes: [{{ ticks: {{ fontColor: '#FFFFFF' }}, gridLines: {{ color: 'rgba(255, 255, 255, 0.2)' }} }}]
                            }}
                        }}
                    }}";

                    string shortChartUrl = "";
                    try
                    {
                        var qcPayload = new { chart = chartConfig, width = 600, height = 300 };
                        var qcContent = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(qcPayload), System.Text.Encoding.UTF8, "application/json");

                        var qcRes = await localClient.PostAsync("https://quickchart.io/chart/create", qcContent);
                        var qcJson = Newtonsoft.Json.Linq.JObject.Parse(await qcRes.Content.ReadAsStringAsync());

                        shortChartUrl = qcJson["url"]?.ToString() ?? "";
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[그래프 생성 실패] {ex.Message}");
                    }

                    var embed = new EmbedBuilder()
                        .WithTitle($"🔮 {nickname} 님의 티어 정밀 분석")
                        .WithUrl($"https://dak.gg/er/players/{safeNick}")
                        .WithColor(color)
                        .WithDescription(gapDesc)
                        .AddField("현재 티어 (Current)", $"{currentTierName}\n(MMR: {currentMMR})", true)
                        .AddField("분석된 적정 티어 (Potential)", $"**{predictedTier}**\n(종합 전투력: {combatScore:N0})", true)
                        .AddField($"📊 분석 데이터 (최근 {rankedGames.Count}판 평균)", $"평딜: {avgDmg:N0}\n평균 TK: {avgTK:F1}\n평균 순위: #{avgRank:F1}", false);

                    if (!string.IsNullOrEmpty(shortChartUrl))
                    {
                        embed.WithImageUrl(shortChartUrl);
                    }

                    embed.WithFooter("※ 파란 막대(퍼포먼스)가 높은데 빨간 선(MMR)이 떨어진다면 완벽한 팀운망겜입니다.");

                    await msg.ModifyAsync(x => { x.Content = ""; x.Embed = embed.Build(); });
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    await msg.ModifyAsync(x => x.Content = "💥 오류 발생. 닉네임을 확인해주세요.");
                }
            }
        }


        // --- 보조 메소드 ---

        private async Task<(double Dmg, double Hunt, double TK, double Rank)?> FetchArtisanStatsAsync(string nickname, int charCode)
        {
            try
            {
                string uRes = await _api.GetAsync("https://open-api.bser.io/v1/user/nickname?query=" + Uri.EscapeDataString(nickname.Trim()));
                var uJson = JObject.Parse(uRes);
                if (uJson["code"]?.ToString() != "200") return null;

                string userId = uJson["user"]["userId"].ToString();
                List<JToken> games = new List<JToken>();
                string nextId = "";

                for (int i = 0; i < 15; i++)
                {
                    string gUrl = "https://open-api.bser.io/v1/user/games/uid/" + userId;
                    if (!string.IsNullOrEmpty(nextId)) gUrl += "?next=" + Uri.EscapeDataString(nextId);

                    var gRes = await _api.GetAsync(gUrl);
                    var gJson = JObject.Parse(gRes);

                    if (gJson["userGames"] != null)
                    {
                        foreach (var g in gJson["userGames"])
                            if (g["matchingMode"]?.ToString() == "3" && (int)g["characterNum"] == charCode) games.Add(g);
                    }
                    if (games.Count >= 10 || gJson["next"] == null) break;
                    nextId = gJson["next"].ToString();
                }

                if (games.Count == 0) return null;
                return (games.Average(x => (double)x["damageToPlayer"]), games.Average(x => (double)x["monsterKill"]), games.Average(x => (double)x["playerKill"] + (double)x["playerAssistant"]), games.Average(x => (double)x["gameRank"]));
            }
            catch { return null; }
        }

        private string GetCharacterRole(int charCode)
        {
            int[] tanks = { 4, 47, 50, 52, 65, 71, 72, 30, 13, 74, 55 };
            int[] supports = { 38, 59, 66, 70, 73 };
            int[] adcs = { 2, 5, 6, 8, 9, 12, 14, 16, 18, 19, 24, 25, 30, 31, 58, 32, 34, 36, 37, 40, 48, 45, 49, 51, 54, 57, 69, 75, 26, 21, 17, 62 };
            int[] assassins = { 35, 64, 67 };
            if (tanks.Contains(charCode)) return "🛡️ 퓨어 탱커";
            if (supports.Contains(charCode)) return "🚑 서포터";
            if (adcs.Contains(charCode)) return "🏹 원거리 딜러";
            if (assassins.Contains(charCode)) return "🗡️ 암살자";
            return "⚔️ 브루저";
        }

        private async Task<string> GetAIFeedbackAsync(
            string charName, string nickname, string role,
            double uDmg, double uHunt, double uTK, double uRank, double uTaken,
            double uCredit, double uVision, double uEscape, double uLegendary, double uSupport, double uRescue,
            double aDmg, double aHunt, double aTK, double aRank, double aTaken, double uDeath)
        {
            List<string> strengths = new List<string>();
            List<string> weaknesses = new List<string>();
            List<string> solutions = new List<string>();

            // =========================================================
            // 🟢 [장점 다중 정밀 검사]
            // =========================================================

            // 1. 역할군별 캐리력 측정
            if (role.Contains("서포터") && uSupport > 15000)
                strengths.Add("• 아군을 불사신으로 만드는 진정한 '수호천사'입니다.");

            if (uDeath <= 0.8)
                strengths.Add("• 평균 데스가 1점대 미만인 '절대 생존자'입니다. 완벽한 포지셔닝으로 팀의 부활 크레딧을 아껴줍니다.");

            if (role.Contains("탱커") && uTaken > aTaken && uRank <= 3.5)
                strengths.Add("• 팀을 위해 묵묵히 매를 맞는 '든든한 방패' 역할을 훌륭히 수행합니다.");

            if (uRescue > 1.5)
                strengths.Add("• 팀원의 목숨을 구하는 '슈퍼세이버' 기질이 있어 한타 뒤집기에 능합니다.");

            if (uVision >= 10 && uRank <= 4.0)
                strengths.Add("• 맵 리딩 능력이 탁월합니다. 랭킹 1위처럼 적의 동선을 역산하는 플레이가 돋보입니다.");

            if (uDmg > aDmg - 2000)
                strengths.Add($"• 무력이 랭커에 도달했습니다! {charName}의 스킬 잠재력을 100% 활용합니다.");

            // 2. 특수 플레이 스타일 (확장팩)
            // ✨ 걸어다니는 트럭
            if (uLegendary >= 4.5 && uRank <= 4)
                strengths.Add("• 전설 아이템으로 온몸을 두른 '걸어다니는 트럭'입니다. 압도적인 체급으로 상대를 찢어발깁니다.");

            // ✨ 탈출의 귀재
            if (uEscape >= 20.0)
                strengths.Add("• 불리한 판을 기가 막히게 읽고 루트코인(탈출)을 타는 '생존의 귀재'입니다. 점수 방어 능력이 탁월합니다.");

            // ✨ 운영의 신
            if (Math.Abs(uHunt - aHunt) <= 15 && uRank <= 4.0)
                strengths.Add("• 장인과 완벽하게 동일한 템포로 야생동물 사이클을 굴리고 있습니다. 빈틈없는 '운영의 신'입니다.");

            // ✨ 생태계 최상위 포식자
            if (uTK >= 13)
                strengths.Add("• 루미아 섬의 생태계 최상위 포식자입니다. 맵 전역의 교전에 참여하여 적을 찢어발기는 압도적인 킬 관여율을 자랑합니다.");

            // ✨ 우승컵 수집가
            if (uRank <= 2.2)
                strengths.Add("• 진정한 '우승컵 수집가'입니다. 어떤 억까 상황에서도 끝까지 살아남아 우승이나 최상위권을 쟁취하는 승리 DNA가 있습니다.");

            // ✨ 육각형 완전체
            if (uDmg >= aDmg && uTaken >= aTaken && uTK >= aTK && uRank <= 2.5)
                strengths.Add("• 무력, 맷집, 킬 캐치까지 모든 것을 완벽하게 해내는 '육각형 완전체'입니다. 팀원들이 가장 믿고 의지하는 핵심 전력입니다.");

            // 3. 신규 추가 장점 (줄타기/모루/소생술사)
            if ((role.Contains("원거리") || role.Contains("포킹")) && uDmg >= aDmg + 4000 && uTaken >= aTaken + 2000 && uDeath <= 1.5)
                strengths.Add("• 뒤에서 구경만 하는 원딜이 아닙니다. 과감한 앞무빙과 줄타기로 어그로 핑퐁까지 해내는 '진정한 전투기계'입니다.");

            if ((role.Contains("탱커") || role.Contains("브루저")) && uTaken >= aTaken + 8000 && uDeath <= 1.5)
                strengths.Add("• 적의 모든 궁극기와 어그로를 묵묵히 다 쳐맞고도 살아남는 '불굴의 모루'입니다. 교전 대승의 1등 공신입니다.");

            if (uRescue >= 2.5 && uRank <= 3.5)
                strengths.Add("• 위기에 빠진 팀을 몇 번이고 키오스크에서 살려내는 '기적의 소생술사'입니다. 이 유저와 함께라면 역전승이 가능합니다.");

            // 🌟 [하이도 운영법 1] 완벽한 교전 설계자
            if (uTK >= aTK - 1.0 && uTaken <= aTaken - 2000 && uRank <= 3.0)
                strengths.Add("• [하이도식 운영] 완벽한 교전 설계자입니다. 적의 정보(크레딧, 부활 타이밍)를 읽고 확실히 이길 수 있는 교전만 골라 패는 능력이 예술입니다.");

            // 🌟 [하이도 운영법 2] 시야 컨트롤의 지배자
            if (uVision >= 12.0 && uDeath <= 1.5 && uRank <= 3.5)
                strengths.Add("• [하이도식 운영] 이동 경로마다 시야를 따고 하이에나를 사전에 차단하는 맵 리딩이 천상계 수준입니다.");

            if (strengths.Count == 0) strengths.Add("무난합니다.");

            // =========================================================
            // 🔴 [문제점 및 솔루션 다중 정밀 검사]
            // =========================================================

            // 1. 피지컬/딜량 부족
            if (uDmg < aDmg - 10000)
            {
                weaknesses.Add($"• 평균 딜량이 장인 기준치보다 수천 점이나 부족한 '솜주먹'입니다. 한타 때 존재감이 지우개 수준입니다.");
                solutions.Add("• 스킬 콤보를 허공에 날리거나 진입 타이밍을 못 재고 있습니다. 연습 모드에서 허수아비부터 깎으며 콤보 숙련도를 올리세요.");
            }

            // 2. 시야 부족 (가장 치명적)
            if (uVision <= 7)
            {
                weaknesses.Add("• 평균 와드 설치가 7개도 안 되어 팀 전체가 하이에나에 무방비하게 노출됩니다.");
                solutions.Add("• 루미아 섬의 모든 박쥐는 당신 몫입니다. 교전 전 시야를 먼저 따는 것이 승리의 0순위 조건입니다.");
            }

            // 3. 초반 전투광
            if (uHunt < aHunt - 15 && uTK >= aTK - 2.0)
            {
                weaknesses.Add("• 야생동물을 버리고 사람만 쫓아다니는 '초반 전투광'입니다. 초반 킬은 점수 복사에 큰 도움이 안 됩니다.");
                solutions.Add("• [랭커의 꿀팁] 1~2일차 교전은 냉정하게 피하고, 동물 숙작에 올인하여 3일차 이후의 고가치 킬을 노리세요.");
            }

            // 4. 악덕 구두쇠
            if (uRank <= 4 && uHunt >= 50 && uLegendary < 2)
            {
                weaknesses.Add("• 살아남아 동물을 다 씨말려놓고 정작 가챠 템은 뽑지 않는 '악덕 구두쇠'입니다.");
                solutions.Add("• 비싼 포스코어만 보지 말고, 가성비 전설템으로 템포를 당긴 뒤 '혈액팩'을 뽑아 고점을 뚫으세요.");
            }

            // 5. 박치기 공룡
            if (uRank > 6.0 && uTK >= 6 && uDeath > 2)
            {
                weaknesses.Add("• 킬에 눈이 멀어 불리한 각에서도 일단 들이박고 보는 '무뇌형 박치기 공룡'입니다.");
                solutions.Add("• 우리가 굳이 들어가지 않아도 상대가 와야 하는 각(금구 등)에서는 절대 먼저 교전을 열지 말고 기다리세요.");
            }

            // 6. 숟가락 살인마 (무임승차)
            if (uTK >= aTK && uDmg < aDmg - 6000)
            {
                weaknesses.Add("• 교전에서 딜은 하나도 안 넣고 막타만 주워 먹는 '숟가락 살인마(무임승차)'입니다.");
                solutions.Add("• 킬 캐치도 능력이지만, 본대 교전 기여도를 높이려면 스킬 적중률과 평타 카이팅 횟수를 의식적으로 늘려야 합니다.");
            }

            // 7. 앞대시 원딜 / 급사병
            if ((role.Contains("원거리") || role.Contains("포킹") || role.Contains("암살자")) && uTaken > aTaken + 3000)
            {
                weaknesses.Add("• 딜러가 탱커보다 더 많이 쳐맞는 '앞대시 병'에 걸렸습니다. 생존력이 최악입니다.");
                solutions.Add("• 포지셔닝부터 다시 배우세요. 적 브루저/탱커의 핵심 진입 스킬이 빠진 것을 눈으로 확인한 뒤에 딜 각을 잡아야 합니다.");
            }

            // 8. 평화주의자 (도망자)
            if (uRank <= 3.5 && uDmg < aDmg - 8000 && uTK < aTK - 2.0)
            {
                weaknesses.Add("• 교전을 극도로 피하고 야생동물만 잡으며 도망만 다니는 '루미아 섬의 평화주의자'입니다.");
                solutions.Add("• 이렇게 숨어서 올린 점수는 고점을 뚫을 수 없습니다. 만만한 상대(빈사 상태, 혼자 남은 적)는 적극적으로 물어뜯는 하이에나 근성을 기르세요.");
            }

            // 9. 가짜 서포터
            if (role.Contains("서포터") && uSupport < 4000)
            {
                weaknesses.Add("• 아군 케어는 내다 버리고 본인이 딜을 넣으려 드는 '가짜 딜서폿 호소인'입니다.");
                solutions.Add("• 서포터의 1순위는 아군 캐리의 생존입니다. 본인의 생존기나 보호 스킬을 적에게 꽂지 말고 아군에게 덮어씌우세요.");
            }

            // 10. 최악의 산책객
            if (uHunt < aHunt - 25 && uTK < aTK - 1.0 && uDmg < aDmg - 3000)
            {
                weaknesses.Add("• 성장도 못하고 교전도 안 한 최악의 '루미아 섬 산책객'이라 팀의 발목을 잡습니다.");
                solutions.Add("• 1일차 낮에 20크레딧 스노우볼을 굴리는 연습부터 하세요. 초반에 말리면 끝까지 쫓겨 다닙니다.");
            }

            // 11. 걸어다니는 ATM
            if (uDeath >= 3.5 && uDmg < aDmg - 3000)
            {
                weaknesses.Add($"• 한 판에 무려 {uDeath:F1}번씩 죽어주는 '루미아 섬의 공용 자판기(ATM)'입니다. 팀원들이 당신을 살리느라 가챠를 못 뽑습니다.");
                solutions.Add("• 교전 시 제발 아군 1선보다 앞에 서지 마세요. 생존을 1순위로 두고 포지셔닝 리플레이를 깎아야 합니다.");
            }

            // 12. 가미카제 (폭사형 딜러)
            if (uDeath >= 2.5 && uDmg >= aDmg)
            {
                weaknesses.Add($"• 딜은 잘 넣는데 본인도 무조건 같이 터지는 '가미카제(폭사형)' 딜러입니다.");
                solutions.Add("• 적을 죽이는 것보다 내가 안 죽으면서 딜을 구겨 넣는 것이 진정한 고수의 카이팅입니다. 진입 핑을 한 템포 늦추세요.");
            }

            // 13. 마비노기 유저 (RPG형)
            if (uHunt >= aHunt + 10 && uDmg < aDmg - 5000 && uTK < aTK)
            {
                weaknesses.Add("• 사람 잡으라고 보냈더니 템만 깎고 야생동물만 학살하고 다니는 '루미아 섬의 RPG 유저'입니다.");
                solutions.Add("• 야생동물 숙작은 교전을 이기기 위한 수단일 뿐입니다. 코어템이 나왔다면 제발 핑을 찍고 본대에 합류해서 싸움을 거세요.");
            }

            // 14. 인간 CCTV (와드싸개)
            if (uVision >= 10 && uDmg < aDmg - 6000 && uRank > 4.5)
            {
                weaknesses.Add("• 와드는 기가 막히게 박아두는데, 정작 싸움이 시작되면 존재감이 지우개가 되는 '인간 CCTV'입니다.");
                solutions.Add("• 맵 리딩은 좋으나 본대의 무력이 너무 약합니다. 억지로 정면 한타를 하지 말고 시야를 활용해 1명을 먼저 자르는 기습을 설계하세요.");
            }

            // 15. 녹슨 암살자
            if (role.Contains("암살자") && uTK < aTK - 2.5 && uDeath >= 2.0)
            {
                weaknesses.Add("• 적 원딜의 털끝 하나 건드리지 못하고 허공에 스킬을 날리며 산화하는 '녹슨 암살자'입니다. 픽의 의미가 없습니다.");
                solutions.Add("• 팀의 탱커/브루저가 먼저 들어가서 적의 주요 CC기(군중제어기)를 빼는 것을 눈으로 꼭 확인한 뒤에 후진입하는 인내심을 기르세요.");
            }

            // 16. 이기적인 빤스런
            if (uEscape >= 15.0 && uRescue <= 0.5 && uDmg < aDmg - 5000)
            {
                weaknesses.Add("• 교전 기여도는 0에 수렴하는데, 본인 목숨만 챙겨서 도망가고 팀원은 안 살려주는 '이기적인 빤스런 장인'입니다.");
                solutions.Add("• 불리한 판에 도망쳐서 순위 방어를 하는 건 좋지만, 도망친 뒤에는 야생동물을 긁어모아 팀원을 살릴 크레딧을 버는 것이 기본 매너입니다.");
            }

            // 🚨 [하이도 팩폭 1] 근시안적 전투광
            if (uDmg >= aDmg && uTK >= aTK && uVision <= 5.0 && uRank > 4.5)
            {
                weaknesses.Add("• [하이도 팩폭] 무력은 좋으나 맵을 전혀 보지 않는 '시야 좁은 전투광'입니다. 싸움에 심취해 있다가 매번 제3자(하이에나)에게 당합니다.");
                solutions.Add("• [하이도 꿀팁] 교전 중이거나 이긴 직후에도 미니맵을 봐야 합니다. 이겼다고 시체 파밍하며 안심하지 말고, 핑을 찍어 팀원과 빠르게 정비하세요.");
            }

            // 🚨 [하이도 팩폭 2] 무지성 불도저
            if (uTaken > aTaken + 3000 && uDmg < aDmg - 4000 && uDeath >= 2.5)
            {
                weaknesses.Add("• [하이도 팩폭] 적의 체급이나 정보(크레딧 소모 여부, 오브젝트 버프 등)를 전혀 읽지 않고 무작정 들이박고 터지는 '무지성 불도저'입니다.");
                solutions.Add("• [하이도 꿀팁] 상대가 나보다 약한지(부활 직후인지), 돈을 쌓아두고 안 썼는지 탭(Tab)을 눌러 확인하고 싸움을 거는 습관을 들이세요.");
            }

            if (weaknesses.Count == 0)
            {
                weaknesses.Add("• 무난하게 1인분은 하지만, 불리한 판도를 스스로 뒤집을 '캐리력'이 2% 부족합니다.");
                solutions.Add("• 팀 오더에만 끌려다니지 말고, 주도적으로 핑을 찍으며 게임을 리드해보세요.");
            }

            string strText = string.Join("\n", strengths);
            string weakText = string.Join("\n", weaknesses);
            string solText = string.Join("\n", solutions);

            return $"✅ **[장점]**\n{strText}\n\n❌ **[문제점]**\n{weakText}\n\n💡 **[개선 방향]**\n{solText}";
        }
    }
}