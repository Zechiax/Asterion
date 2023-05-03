namespace Asterion.Services;

public interface IBotStatsService
{
    public void Initialize();
    public Task PublishToTopGg();
}