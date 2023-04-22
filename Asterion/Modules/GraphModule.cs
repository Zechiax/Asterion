using Asterion.Services;
using Asterion.Services.Modrinth;
using Discord.Interactions;

namespace Asterion.Modules;

public class GraphModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DownloadManager _downloadManager;
    private readonly ModrinthService _modrinthService;
    
    public GraphModule(DownloadManager downloadManager, ModrinthService modrinthService)
    {
        _downloadManager = downloadManager;
        _modrinthService = modrinthService;
    }
    
    [SlashCommand("chart", "Graphs the downloads of a mod", runMode: RunMode.Async)]
    public async Task Chart(string projectId = "fabric-api")
    {
        await DeferAsync();
        
        var mod = await _modrinthService.FindProject(projectId);
        if (mod.Success == false)
        {
            await FollowupAsync("Project not found", ephemeral: true);
            return;
        }

        var downloadData = await _downloadManager.GetTotalDownloadsAsync(mod.Payload.Project.Id);
        
        if (downloadData.Count == 0)
        {
            await FollowupAsync("No downloads are currently stored", ephemeral: true);
            return;
        }
    }
}