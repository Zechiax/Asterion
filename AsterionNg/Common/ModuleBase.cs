using System.Globalization;
using Discord.Interactions;
using Microsoft.Extensions.Localization;

namespace AsterionNg.Common;

/// <summary>
/// An implementation of the <see cref="InteractionModuleBase"/> for this solution.
/// </summary>
public abstract class ModuleBase(
    IStringLocalizerFactory factory,
    IGuildCultureProvider? cultureProvider = null) : InteractionModuleBase<SocketInteractionContext>
{
    private IStringLocalizer? _localizer;
    protected IStringLocalizer L => _localizer ??= factory.Create(
        $"Modules.{GetType().Name.Replace("Module", "")}",
        GetType().Assembly.FullName!);

    protected CultureInfo GuildCulture => cultureProvider?.GetGuildCulture(Context.Guild.Id) ?? new DefaultGuildCultureProvider().GetGuildCulture(Context.Guild.Id);
}