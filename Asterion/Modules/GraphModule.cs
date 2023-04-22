using Asterion.Services;
using Discord.Interactions;

namespace Asterion.Modules;

public class GraphModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DownloadManager _downloadManager;
    
    public GraphModule(DownloadManager downloadManager)
    {
        _downloadManager = downloadManager;
    }
    
    [SlashCommand("graph", "Graphs the downloads of a mod", runMode: RunMode.Async)]
    public async Task Graph()
    {
        
    }
}