using System.Text;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Asterion.Services;

public class BotStatsService : IBotStatsService
{
    private readonly DiscordSocketClient _discordClient;
    private readonly HttpClient _httpClient;
    private readonly ILogger<BotStatsService> _logger;
    private readonly string? _topGgToken;

    public BotStatsService(IConfiguration config, HttpClient httpClient, DiscordSocketClient discordClient,
        ILogger<BotStatsService> logger)
    {
        _httpClient = httpClient;
        _discordClient = discordClient;
        _logger = logger;

        _topGgToken = config["TopGgToken"];
    }

    public void Initialize()
    {
        _ = PublishToTopGg();
    }

    public async Task PublishToTopGg()
    {
        if (_discordClient.ShardId != 0)
            return;

        if (string.IsNullOrEmpty(_topGgToken))
            return;

        var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        while (await timer.WaitForNextTickAsync().ConfigureAwait(false))
            try
            {
                _logger.LogInformation("Publishing bot stats to top.gg, server count: {ServerCount}",
                    _discordClient.Guilds.Count);
                using var request = new HttpRequestMessage(HttpMethod.Post,
                    $"https://top.gg/api/bots/{_discordClient.CurrentUser.Id}/stats");
                request.Content = new StringContent(JsonConvert.SerializeObject(new
                {
                    server_count = _discordClient.Guilds.Count,
                    // shard_count = 1,
                    shards = Array.Empty<string>()
                }), Encoding.UTF8, "application/json");

                request.Headers.Add("Authorization", _topGgToken);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                    _logger.LogWarning("Failed to publish bot stats to top.gg: {StatusCode} ({ReasonPhrase})",
                        response.StatusCode, response.ReasonPhrase);
                else
                    _logger.LogInformation("Published bot stats to top.gg");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to publish bot stats to top.gg");
            }
    }
}