using System.Globalization;
using Asterion.Database.Models;
using Asterion.Interfaces;
using Discord;

namespace Asterion.ComponentBuilders;

/// <summary>
///     The server tabs of the manage panel. Custom IDs are <c>{id}:{ownerId}</c>, only the owner can use them.
/// </summary>
public static class ServerSettingsPanelBuilder
{
    public static readonly Color Accent = Color.LighterGrey;

    public const string ManageRoleMenuId = "srv_manage";
    public const string PingRoleMenuId = "srv_ping";
    public const string LanguageMenuId = "srv_lang";
    public const string MessageStyleMenuId = "srv_msgstyle";
    public const string ChangelogStyleMenuId = "srv_clstyle";
    public const string SubscribeButtonToggleId = "srv_subbtn";
    public const string ChangelogLengthButtonId = "srv_cllen";
    public const string ChangelogLengthModalId = "srv_cllen_modal";
    public const string ChangelogLengthInputId = "length";
    public const string PreviewButtonId = "srv_preview";
    public const string MilestoneToggleId = "srv_ms_toggle";
    public const string MilestoneLadderMenuId = "srv_ms_ladder";
    public const string MilestoneChannelMenuId = "srv_ms_channel";
    public const string MilestonePingRoleMenuId = "srv_ms_ping";
    public const string UnsubscribeAllButtonId = "srv_unsuball";
    public const string UnsubscribeAllModalId = "srv_unsuball_modal";
    public const string UnfollowAllButtonId = "srv_unfolall";
    public const string UnfollowAllModalId = "srv_unfolall_modal";
    public const string ConfirmInputId = "confirm";

    /// <summary>Value of the language option that follows Discord's locale</summary>
    public const string DefaultLanguageValue = "default";

    public const int MinChangelogLength = 100;
    public const int MaxChangelogLength = 4000;

    // Discord limit of options in one select menu
    private const int MaxSelectOptions = 25;

    /// <summary>
    ///     Who can manage the bot, the default ping role, the language, the search results and removing everything
    /// </summary>
    public static ContainerBuilder BuildGeneral(Guild guild, ulong ownerId, int projectCount, int followCount,
        IEnumerable<string> locales, ILocalizationService loc, CultureInfo? culture, string? notice = null)
    {
        string Id(string prefix) => $"{prefix}:{ownerId}";

        var fields = new[]
        {
            new SettingsField(loc.Get("server-manage-role", culture),
                SettingsPanelBuilder.RolePicker(Id(ManageRoleMenuId), guild.ManageRole,
                    loc.Get("server-manage-role-placeholder", culture))),
            new SettingsField(loc.Get("server-ping-role", culture),
                SettingsPanelBuilder.RolePicker(Id(PingRoleMenuId), guild.PingRole,
                    loc.Get("server-ping-role-placeholder", culture))),
            new SettingsField(loc.Get("server-language", culture),
                LanguageMenu(Id(LanguageMenuId), guild.GuildSettings.Language, locales, loc, culture))
        };

        var container = SettingsPanelBuilder.BuildContainer(loc.Get("server-general-title", culture), Accent,
            fields, [], JoinNotice(loc.Get("server-general-description", culture), notice));

        var showSubscribeButton = guild.GuildSettings.ShowSubscribeButton ?? true;
        container
            .AddComponent(new SeparatorBuilder())
            .AddComponent(new TextDisplayBuilder($"**{loc.Get("server-search-title", culture)}**"));
        SettingsPanelBuilder.AddButtonSetting(container, loc.Get("server-subscribe-button", culture),
            loc.Get(showSubscribeButton ? "server-subscribe-button-shown" : "server-subscribe-button-hidden",
                culture),
            new ButtonBuilder(loc.Get(showSubscribeButton ? "button-hide" : "button-show", culture),
                Id(SubscribeButtonToggleId), ButtonStyle.Secondary));

        return container
            .AddComponent(new SeparatorBuilder())
            .AddComponent(new TextDisplayBuilder($"**{loc.Get("server-danger-zone", culture)}**"))
            .AddComponent(new ActionRowBuilder().AddComponents(
                new ButtonBuilder(loc.Get("server-unsubscribe-all", culture), Id(UnsubscribeAllButtonId),
                    ButtonStyle.Danger, isDisabled: projectCount == 0),
                new ButtonBuilder(loc.Get("server-unfollow-all", culture), Id(UnfollowAllButtonId),
                    ButtonStyle.Danger, isDisabled: followCount == 0)));
    }

    /// <summary>
    ///     How the update messages look
    /// </summary>
    public static ContainerBuilder BuildNotifications(GuildSettings settings, ulong ownerId,
        ILocalizationService loc, CultureInfo? culture, string? notice = null)
    {
        string Id(string prefix) => $"{prefix}:{ownerId}";

        var header = $"## {loc.Get("server-notifications-title", culture)}\n" +
                     JoinNotice(loc.Get("server-notifications-description", culture), notice);

        // Preview next to the title, it shows what the settings below look like
        var container = new ContainerBuilder()
            .WithAccentColor(Accent)
            .AddComponent(new SectionBuilder()
                .AddComponent(new TextDisplayBuilder(header))
                .WithAccessory(new ButtonBuilder(loc.Get("button-preview", culture), Id(PreviewButtonId),
                    ButtonStyle.Secondary)));

        SettingsPanelBuilder.AddField(container, new SettingsField(loc.Get("server-message-style", culture),
            EnumMenu(Id(MessageStyleMenuId), settings.MessageStyle, "message-style", loc, culture)));
        SettingsPanelBuilder.AddField(container, new SettingsField(loc.Get("server-changelog-style", culture),
            EnumMenu(Id(ChangelogStyleMenuId), settings.ChangelogStyle, "changelog-style", loc, culture)));

        var length = new { length = settings.ChangeLogMaxLength };
        SettingsPanelBuilder.AddButtonSetting(container, loc.Get("server-changelog-length", culture),
            settings.ChangeLogMaxLength == GuildSettings.DefaultChangeLogMaxLength
                ? loc.Get("server-changelog-length-value-default", culture, length)
                : loc.Get("server-changelog-length-value", culture, length),
            new ButtonBuilder(loc.Get("button-change", culture), Id(ChangelogLengthButtonId), ButtonStyle.Secondary,
                isDisabled: settings.ChangelogStyle == ChangelogStyle.NoChangelog));

        AddMilestoneSettings(container, settings, Id, loc, culture);

        return container;
    }

    /// <summary>
    ///     The download milestone settings, the menus are only shown while milestones are enabled
    /// </summary>
    private static void AddMilestoneSettings(ContainerBuilder container, GuildSettings settings,
        Func<string, string> id, ILocalizationService loc, CultureInfo? culture)
    {
        container
            .AddComponent(new SeparatorBuilder())
            .AddComponent(new TextDisplayBuilder($"**{loc.Get("server-milestones-title", culture)}**"));

        SettingsPanelBuilder.AddButtonSetting(container, loc.Get("server-milestones", culture),
            loc.Get(settings.MilestonesEnabled ? "server-milestones-enabled" : "server-milestones-disabled", culture),
            new ButtonBuilder(loc.Get(settings.MilestonesEnabled ? "button-disable" : "button-enable", culture),
                id(MilestoneToggleId), settings.MilestonesEnabled ? ButtonStyle.Secondary : ButtonStyle.Success));

        if (!settings.MilestonesEnabled)
            return;

        SettingsPanelBuilder.AddField(container, new SettingsField(loc.Get("server-milestone-ladder", culture),
            EnumMenu(id(MilestoneLadderMenuId), settings.MilestoneLadder, "milestone-ladder", loc, culture)));
        SettingsPanelBuilder.AddField(container, new SettingsField(loc.Get("server-milestone-channel", culture),
            SettingsPanelBuilder.ChannelPicker(id(MilestoneChannelMenuId), settings.MilestoneChannel,
                loc.Get("server-milestone-channel-placeholder", culture), optional: true)));
        SettingsPanelBuilder.AddField(container, new SettingsField(loc.Get("server-milestone-ping-role", culture),
            SettingsPanelBuilder.RolePicker(id(MilestonePingRoleMenuId), settings.MilestonePingRole,
                loc.Get("server-milestone-ping-role-placeholder", culture))));
    }

    /// <summary>
    ///     Modal for the changelog length, prefilled with the current length. Modals can't have buttons,
    ///     so leaving the input empty resets it to the default.
    /// </summary>
    public static Modal BuildChangelogLengthModal(long current, ILocalizationService loc, CultureInfo? culture)
    {
        return new ModalBuilder()
            .WithTitle(loc.Get("server-changelog-length-title", culture))
            .WithCustomId(ChangelogLengthModalId)
            .AddLabel(loc.Get("server-changelog-length-input", culture),
                new TextInputBuilder()
                    .WithCustomId(ChangelogLengthInputId)
                    .WithStyle(TextInputStyle.Short)
                    .WithValue(current.ToString(CultureInfo.InvariantCulture))
                    .WithPlaceholder(GuildSettings.DefaultChangeLogMaxLength.ToString(CultureInfo.InvariantCulture))
                    .WithMaxLength(4)
                    .WithRequired(false),
                loc.Get("server-changelog-length-hint", culture, new
                {
                    min = MinChangelogLength, max = MaxChangelogLength,
                    @default = GuildSettings.DefaultChangeLogMaxLength
                }))
            .Build();
    }

    /// <summary>
    ///     Modal that explains what will be removed and asks to type the confirmation word,
    ///     so nothing is removed by a misclick
    /// </summary>
    /// <param name="keyPrefix">Prefix of the texts: "{prefix}-title", "{prefix}-warning" and "{prefix}-word"</param>
    public static Modal BuildDangerModal(string customId, string keyPrefix, int count, ILocalizationService loc,
        CultureInfo? culture)
    {
        var word = loc.Get($"{keyPrefix}-word", culture);

        return new ModalBuilder()
            .WithTitle(loc.Get($"{keyPrefix}-title", culture))
            .WithCustomId(customId)
            .AddTextDisplay(loc.Get($"{keyPrefix}-warning", culture, new { count }))
            .AddLabel(loc.Get("server-danger-confirm-input", culture, new { word }),
                new TextInputBuilder()
                    .WithCustomId(ConfirmInputId)
                    .WithStyle(TextInputStyle.Short)
                    .WithPlaceholder(word)
                    .WithMaxLength(100)
                    .WithRequired(true))
            .Build();
    }

    /// <summary>
    ///     If the text typed in the danger modal is the confirmation word, ignoring case and surrounding spaces
    /// </summary>
    /// <param name="culture">Culture of the word, the thread's culture can be a different language</param>
    public static bool IsConfirmed(string? input, string word, CultureInfo? culture)
    {
        return input is not null &&
               string.Compare(input.Trim(), word, culture ?? CultureInfo.InvariantCulture,
                   CompareOptions.IgnoreCase) == 0;
    }

    /// <summary>
    ///     Parses the changelog length entered in the modal: the default if it was left empty,
    ///     null if it is not a number in the allowed range
    /// </summary>
    public static int? ParseChangelogLength(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return GuildSettings.DefaultChangeLogMaxLength;

        return int.TryParse(input.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var length) &&
               length is >= MinChangelogLength and <= MaxChangelogLength
            ? length
            : null;
    }

    private static SelectMenuBuilder LanguageMenu(string customId, string? current, IEnumerable<string> locales,
        ILocalizationService loc, CultureInfo? culture)
    {
        var menu = new SelectMenuBuilder()
            .WithCustomId(customId)
            .WithMinValues(1)
            .WithMaxValues(1)
            .AddOption(loc.Get("server-language-default", culture), DefaultLanguageValue,
                isDefault: string.IsNullOrEmpty(current));

        foreach (var locale in locales.Order(StringComparer.OrdinalIgnoreCase).Take(MaxSelectOptions - 1))
            menu.AddOption(LanguageName(locale), locale,
                isDefault: string.Equals(current, locale, StringComparison.OrdinalIgnoreCase));

        return menu;
    }

    private static string LanguageName(string locale)
    {
        try
        {
            return CultureInfo.GetCultureInfo(locale).NativeName;
        }
        catch (CultureNotFoundException)
        {
            return locale;
        }
    }

    /// <summary>
    ///     Select menu with every value of the enum, labels are "{keyPrefix}-{value in lower case}"
    /// </summary>
    private static SelectMenuBuilder EnumMenu<T>(string customId, T current, string keyPrefix,
        ILocalizationService loc, CultureInfo? culture) where T : struct, Enum
    {
        var menu = new SelectMenuBuilder()
            .WithCustomId(customId)
            .WithMinValues(1)
            .WithMaxValues(1);

        foreach (var value in Enum.GetValues<T>())
            menu.AddOption(loc.Get($"{keyPrefix}-{value.ToString().ToLowerInvariant()}", culture), value.ToString(),
                isDefault: value.Equals(current));

        return menu;
    }

    private static string JoinNotice(string description, string? notice)
    {
        return string.IsNullOrEmpty(notice) ? description : $"{description}\n{notice}";
    }
}
