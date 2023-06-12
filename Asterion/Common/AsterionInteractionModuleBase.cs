using System.Globalization;
using Discord;
using Discord.Interactions;

namespace Asterion.Common;

public class AsterionInteractionModuleBase : InteractionModuleBase<SocketInteractionContext>
{
    protected CultureInfo? CommandCultureInfo { get; set; }
    
    
    public override void BeforeExecute(ICommandInfo cmd)
    {
        // We currently set US culture for all commands
        CommandCultureInfo = CultureInfo.GetCultureInfo("en-US");
        
        base.BeforeExecute(cmd);
    }
}