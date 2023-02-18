using System.Text;
using Asterion.Database.Models;
using Asterion.Services.Modrinth;
using Discord;
using Humanizer;
using Humanizer.Bytes;
using Modrinth.Extensions;
using Modrinth.Models;
using Modrinth.Models.Enums;
using Asterion.Extensions;
using Array = System.Array;
using Version = Modrinth.Models.Version;

namespace Asterion.EmbedBuilders;

public static class ModrinthEmbedBuilder
{
    /// <summary>
    /// The main color of the Modrinth logo
    /// </summary>
    private static readonly Color ModrinthColor = new(27, 217, 106);

    /// <summary>
    /// Limit for the length of value of fields on embed (Discord limit is 1024)
    /// </summary>
    private const int EmbedFieldLimit = 512;

    /// <summary>
    /// Limit for the length of description on embed (Discord limit is 4096)
    /// </summary>
    private const int DescriptionLimit = 2000;

    public static EmbedAuthorBuilder GetUserAuthor(User user)
    {
        var embedAuthor = new EmbedAuthorBuilder
        {
            Name = user.Username,
            IconUrl = user.AvatarUrl,
            Url = user.Url
        };

        return embedAuthor;
    }

    private static EmbedAuthorBuilder GetProjectAuthor(TeamMember owner)
    {
        var embedAuthor = new EmbedAuthorBuilder
        {
            Name = owner.User.Username,
            IconUrl = owner.User.AvatarUrl,
            Url = owner.User.Url
        };

        return embedAuthor;
    }

    /// <summary>
    /// Returns generic Modrinth embed author
    /// </summary>
    /// <param name="project"></param>
    /// <param name="user"></param>
    /// <returns></returns>
    private static EmbedAuthorBuilder GetModrinthAuthor(Project? project = null, User? user = null)
    {
        var embedAuthor = new EmbedAuthorBuilder
        {
            Name =
                $"Modrinth {(project is not null || user is not null ? '|' : null)} {(project is not null ? project.ProjectType.ToString() : "User")}",
            IconUrl = "https://avatars.githubusercontent.com/u/67560307",
            Url = "https://modrinth.com/"
        };

        return embedAuthor;
    }

    /// <summary>
    /// Creates EmbedAuthor based on provided information
    /// </summary>
    /// <param name="project"></param>
    /// <param name="teamMembers"></param>
    /// <param name="version"></param>
    /// <returns></returns>
    private static EmbedAuthorBuilder GetEmbedAuthor(Project project, IEnumerable<TeamMember>? teamMembers = null, Version? version = null)
    {
        var author = version is not null ? teamMembers.GetByUserId(version.AuthorId) : teamMembers.GetOwner();
        
        return author is null ? GetModrinthAuthor(project) : GetProjectAuthor(author);
    }

    public static EmbedBuilder GetProjectEmbed(SearchResult<ProjectDto> searchResult, IEnumerable<TeamMember>? teamMembers = null)
    {
        return GetProjectEmbed(searchResult.Payload.Project, teamMembers);
    }

    /// <summary>
    /// Creates embed with basic project info
    /// </summary>
    /// <param name="project">The project from which to get the info</param>
    /// <param name="majorColor"></param>
    /// <param name="teamMembers">Members of the team for this project, not required, if not provided, will show generic Modrinth Author</param>
    /// <returns>Embed builder, which can be further edited</returns>
    public static EmbedBuilder GetProjectEmbed(Project project, IEnumerable<TeamMember>? teamMembers = null, DateTimeOffset? dataTime = null)
    {
        var author = GetEmbedAuthor(project, teamMembers);
        
        var embed = new EmbedBuilder
        {
            Author = author,
            Title = project.Title,
            Url = project.Url,
            Description = project.Description,
            ThumbnailUrl = project.IconUrl,
            // No icon, no major color, use Modrinth's color
            Color = project.Color is null ? ModrinthColor : project.Color.Value.ToDiscordColor(),
            Fields = new List<EmbedFieldBuilder?>
            {
                // Format downloads from 319803 to 319,8K 
                 new() { Name = "Downloads", Value = project.Downloads.ToMetric(decimals: 1).Transform(To.UpperCase), IsInline = true },
                // Format downloads from 319803 to 319 803
                // new() { Name = "Downloads", Value = project.Downloads.SeparateThousands(), IsInline = true },
                new() { Name = "Followers", Value = project.Followers.SeparateThousands(), IsInline = true },
                new()
                {
                    Name = "Categories", Value = project.Categories.Length > 0
                        ? string.Join(", ", project.Categories).Transform(To.TitleCase) 
                    : Format.Italics("No categories") , IsInline = true },
                new() { Name = "Type", Value = project.ProjectType.Humanize(), IsInline = true },
                new() { Name = "ID", Value = project.Id, IsInline = true },
                new() { Name = "Created | Last updated", Value = $"{TimestampTag.FromDateTime(project.Published, TimestampTagStyles.Relative)} | {TimestampTag.FromDateTime(project.Updated, TimestampTagStyles.Relative)}"  }
            },
            // Choose 'random' picture from gallery through TickCount
            ImageUrl = project.Gallery.Length > 0 ? project.Gallery[Math.Abs(Environment.TickCount) % project.Gallery.Length].Url : null,
            Footer = new EmbedFooterBuilder
            {
                Text = "Information to date"
            },
            Timestamp = dataTime ?? DateTimeOffset.Now
        };

        return embed;
    }

    public static EmbedBuilder VersionUpdateEmbed(MessageStyle style, Project project, Version version,
        IEnumerable<TeamMember>? teamMembers = null)
    {
        return style switch
        {
            MessageStyle.Full => GetFullVersionUpdateEmbed(project, version, teamMembers),
            MessageStyle.Compact => GetCompactVersionUpdateEmbed(project, version, teamMembers),
            _ => throw new ArgumentOutOfRangeException(nameof(style), style, null)
        };
    }

    private static EmbedBuilder GetCompactVersionUpdateEmbed(Project project, Version version,
        IEnumerable<TeamMember>? teamMembers = null)
    {
        var embed = new EmbedBuilder
        {
            Title = $"{Format.Bold(project.Title)} {version.VersionNumber}",
            Description = $"{version.Name}",
            Url = project.GetVersionUrl(version),
            Timestamp = version.DatePublished,
            Color = version.ProjectVersionType.ToColor(),
            Footer = new EmbedFooterBuilder
            {
                IconUrl = GetEmbedAuthor(project, teamMembers, version).IconUrl,
                Text = "Published"
            }
        };

        return embed;
    }

    private static EmbedBuilder GetFullVersionUpdateEmbed(Project project, Version version, IEnumerable<TeamMember>? teamMembers = null)
    {
        var sbFiles = new StringBuilder();

        foreach (var file in version.Files)
        {
            sbFiles.AppendLine($"[{file.FileName}]({file.Url}) | {ByteSize.FromBytes(file.Size).Humanize()}");
        }

        var changelog = string.IsNullOrEmpty(version.Changelog) ? "\n\n*No changelog provided*" : $"\n\n{Format.Underline(Format.Bold("Changelog:"))}\n" +
            $"{version.Changelog}".Truncate(DescriptionLimit);

        var embedAuthor = GetEmbedAuthor(project, teamMembers, version);
        
        var embed = new EmbedBuilder
        {
            Author = embedAuthor,
            Footer = new EmbedFooterBuilder
            {
                Text = "Published"
            },
            Title = $"{Format.Bold(project.Title)} | New Version Found",
            Description = $"Version {Format.Bold(version.VersionNumber)} has been uploaded to Modrinth" +
                          changelog,
            Url = project.Url,
            ThumbnailUrl = project.IconUrl,
            ImageUrl = null,
            Fields = new List<EmbedFieldBuilder>
            {
                new()
                {
                    Name = "MC Versions",
                    Value = string.Join(", ",version.GameVersions).Truncate(EmbedFieldLimit),
                    IsInline = true
                },
                new()
                {
                    Name = "Loaders",
                    Value = string.Join(", ", version.Loaders).Transform(To.TitleCase).Truncate(EmbedFieldLimit),
                    IsInline = true
                },
                new()
                {
                    Name = $"Files ({version.Files.Length})",
                    Value = sbFiles.ToString().Truncate(EmbedFieldLimit),
                },
                new()
                {
                    Name = "Links",
                    Value = string.Join(" | ", 
                        $"[Changelog]({project.Url}/changelog)", 
                        $"[Version Info]({project.GetVersionUrl(version)})")
                }
            },
            Timestamp = version.DatePublished,
            Color = version.ProjectVersionType.ToColor()
        };

        return embed;
    }

    private static string FormatMostDownloaded(IEnumerable<Project> mostDownloaded)
    {
        var sb = new StringBuilder();
        
        foreach (var p in mostDownloaded.Take(3))
        {
            sb.Append(Format.Url(p.Title, p.Url));
            sb.Append($" | {p.Downloads.ToModrinthFormat()} downloads");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public static EmbedBuilder GetUserEmbed(SearchResult<UserDto> searchResult)
    {
        return GetUserEmbed(searchResult.Payload.User, searchResult.Payload.Projects, searchResult.Payload.MajorColor,
            searchResult.SearchTime);
    }

    public static EmbedBuilder GetUserEmbed(User user, Project[] userProjects, Color? majorColor = null, DateTimeOffset? dataTime = null)
    {
        var mostDownloaded = userProjects.OrderByDescending(x => x.Downloads + x.Followers);
        var embed = new EmbedBuilder()
        {
            Author = GetModrinthAuthor(user: user),
            Title = $"{Format.Bold(user.Username)}",
            Url = user.Url,
            ThumbnailUrl = user.AvatarUrl,
            Color = majorColor,
            Description = string.IsNullOrEmpty(user.Bio) ? Format.Italics("No bio set") : user.Bio.Truncate(DescriptionLimit),
            Fields = new List<EmbedFieldBuilder>
            {
                new()
                {
                    Name = "Info",
                    Value = Format.Bold("Projects: ") + userProjects.Length +
                            Format.Bold("\nDownloads: ") + userProjects.Sum(x => x.Downloads).ToModrinthFormat() +
                            Format.Bold("\nFollowers: ") + userProjects.Sum(x => x.Followers).SeparateThousands()
                },
                new()
                {
                    Name = "Most popular projects",
                    Value = userProjects.Length < 1 ? Format.Italics("No projects") : FormatMostDownloaded(mostDownloaded),
                    IsInline = false
                },
                new()
                {
                    Name = "Role",
                    Value = user.Role.Humanize(),
                    IsInline = true
                },
                new()
                {
                    Name = "Joined",
                    Value = TimestampTag.FromDateTime(user.Created, TimestampTagStyles.Relative),
                    IsInline = true
                },
                new()
                {
                    Name = "Id",
                    Value = user.Id,
                    IsInline = true
                }
            },
            Footer = new EmbedFooterBuilder()
            {
                Text = "Information to date"
            },
            Timestamp = dataTime ?? DateTimeOffset.Now
        };
        return embed;
    }

    public static EmbedBuilder GetMoreResultsEmbed(IEnumerable<SearchResult> projects, string query)
    {
        var embed = new EmbedBuilder()
        {
            Title = "More results"
        };

        var limitProjects = projects.Take(10);

        var description = new StringBuilder();
        description.AppendLine($"For query: {Format.Italics(query)}");
        description.AppendLine();
        
        var counter = 1;
        foreach (var p in limitProjects)
        {
            description.AppendLine($"{Format.Bold($"{counter}.")} {Format.Url(p.Title, p.Url)} | {p.Downloads.ToModrinthFormat()}");
            counter++;
        }

        embed.Description = description.ToString();
        
        return embed;
    }

    /// <summary>
    /// Chooses color based on the version type
    /// </summary>
    /// <param name="versionType"></param>
    /// <returns></returns>
    private static Color ToColor(this ProjectVersionType versionType)
    {
        return versionType switch
        {
            ProjectVersionType.Alpha => new Color(219, 49, 98),
            ProjectVersionType.Beta => new Color(247, 187, 67),
            ProjectVersionType.Release => new Color(27, 217, 106),
            _ => Color.Default
        };
    }
}