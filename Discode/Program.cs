using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace EternalReturnBot
{
    class Program
    {
        // 설정값
        private string DiscordToken => Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
        public static string ErApiKey => Environment.GetEnvironmentVariable("ER_API_KEY");
        public static int CurrentSeasonId = 33;

        // API 요청 제한 방어를 위한 세마포어 (초당 1회 제한 준수)
        public static readonly SemaphoreSlim ApiSemaphore = new SemaphoreSlim(1, 1);

        private DiscordSocketClient _client;
        private CommandService _commands;
        private IServiceProvider _services;

        static void Main(string[] args) => new Program().RunBotAsync().GetAwaiter().GetResult();

        public async Task RunBotAsync()
        {
            StartHealthCheckServer();
            // 환경 변수가 제대로 설정되었는지 검사
            if (string.IsNullOrEmpty(DiscordToken) || string.IsNullOrEmpty(ErApiKey))
            {
                Console.WriteLine("❌ 에러: 환경 변수(DISCORD_BOT_TOKEN 또는 ER_API_KEY)가 설정되지 않았습니다.");
                Console.WriteLine("로컬에서 실행 시 시스템 환경 변수를 설정하거나, Render 대시보드에서 설정해 주세요.");
                return;
            }


            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
            };

            _client = new DiscordSocketClient(config);
            _commands = new CommandService();

            // 서비스 등록 (API 전용 서비스 추가)
            _services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(_commands)
                .AddSingleton<HttpClient>()
                .AddSingleton<ErApiService>()
                .BuildServiceProvider();

            _client.Log += Log;

            // 시즌 업데이트
            var erApi = _services.GetRequiredService<ErApiService>();
            await erApi.UpdateCurrentSeasonAsync();

            await _client.LoginAsync(TokenType.Bot, DiscordToken);
            await _client.StartAsync();

            _client.MessageReceived += HandleCommandAsync;
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

            await Task.Delay(-1);
        }

        private async Task HandleCommandAsync(SocketMessage arg)
        {
            var message = arg as SocketUserMessage;
            if (message == null || message.Author.IsBot) return;

            int argPos = 0;
            if (message.Content == "!")
            {
                var embed = new EmbedBuilder()
                    .WithTitle("🤖 칼핑봇 사용 설명서")
                    .WithDescription("명령어 목록입니다. (Rate Limit 방어 적용됨)")
                    .WithColor(Color.Green)
                    .AddField("🔍 전적 검색", "`!전적 [닉네임]`", true)
                    .AddField("⚔️ 장인 비교", "`!장인비교 [닉네임]`", true)
                    .AddField("🔮 티어 측정", "`!티어측정 [닉네임]`", true);
                await message.Channel.SendMessageAsync(embed: embed.Build());
                return;
            }

            if (message.HasCharPrefix('!', ref argPos))
            {
                var context = new SocketCommandContext(_client, message);
                await _commands.ExecuteAsync(context, argPos, _services);
            }
        }

        private Task Log(LogMessage arg) { Console.WriteLine(arg); return Task.CompletedTask; }
        private void StartHealthCheckServer()
        {
            var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
            var listener = new System.Net.HttpListener();
            listener.Prefixes.Add($"http://*:{port}/");
            listener.Start();
            Task.Run(() => {
                while (true)
                {
                    var context = listener.GetContext();
                    var response = context.Response;
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes("Bot is running!");
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                    response.Close();
                }
            });
        }
    }

}