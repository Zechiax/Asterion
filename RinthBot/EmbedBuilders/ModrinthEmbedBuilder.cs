using System.Text;
using Discord;
using Humanizer;
using Humanizer.Bytes;
using Modrinth.RestClient.Models;
using Modrinth.RestClient.Models.Enums;
using RinthBot.Database.Models;
using RinthBot.Extensions;
using RinthBot.Services.Modrinth;
using Array = System.Array;
using Version = Modrinth.RestClient.Models.Version;

namespace RinthBot.EmbedBuilders;

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

    /// <summary>
    /// Creates direct URL string to the specific project 
    /// </summary>
    /// <param name="project"></param>
    /// <returns></returns>
    public static string GetProjectUrl(Project project)
    {
        return $"https://modrinth.com/{GetProjectUrlType(project)}/{project.Id}";
    }

    /// <summary>
    /// Returns formatted type used in Modrinth url to specific project
    /// </summary>
    /// <param name="project"></param>
    /// <returns></returns>
    public static string GetProjectUrlType(Project project)
    {
        return project.ProjectType switch
        {
            ProjectType.Mod => "mod",
            ProjectType.Modpack => "modpack",
            ProjectType.Resourcepack => "resourcepack",
            _ => string.Empty
        };
    }

    /// <summary>
    /// Creates direct URL string to the specific version details of the project 
    /// </summary>
    /// <param name="project"></param>
    /// <param name="version"></param>
    /// <returns></returns>
    public static string GetVersionUrl(Project project, Version version)
    {
        return $"{GetProjectUrl(project)}/version/{version.Id}";
    }

    public static string GetUserUrl(User user)
    {
        return $"https://modrinth.com/user/{user.Id}";
    }

    public static EmbedAuthorBuilder GetUserAuthor(User user)
    {
        var embedAuthor = new EmbedAuthorBuilder
        {
            Name = user.Username,
            IconUrl = user.AvatarUrl,
            Url = GetUserUrl(user)
        };

        return embedAuthor;
    }

    private static EmbedAuthorBuilder GetProjectAuthor(TeamMember owner)
    {
        var embedAuthor = new EmbedAuthorBuilder
        {
            Name = owner.User.Username,
            IconUrl = owner.User.AvatarUrl,
            Url = GetUserUrl(owner.User)
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
        return GetProjectEmbed(searchResult.Payload.Project, searchResult.Payload.MajorColor, teamMembers);
    }

    /// <summary>
    /// Creates embed with basic project info
    /// </summary>
    /// <param name="project">The project from which to get the info</param>
    /// <param name="majorColor"></param>
    /// <param name="teamMembers">Members of the team for this project, not required, if not provided, will show generic Modrinth Author</param>
    /// <returns>Embed builder, which can be further edited</returns>
    public static EmbedBuilder GetProjectEmbed(Project project, Color majorColor, IEnumerable<TeamMember>? teamMembers = null, DateTimeOffset? dataTime = null)
    {
        var author = GetEmbedAuthor(project, teamMembers);
        
        var embed = new EmbedBuilder
        {
            Author = author,
            Title = project.Title,
            Url = GetProjectUrl(project),
            Description = project.Description,
            ThumbnailUrl = project.IconUrl,
            // No icon, no major color, use Modrinth's color
            Color = string.IsNullOrEmpty(project.IconUrl) ? ModrinthColor : majorColor,
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
            Url = GetVersionUrl(project, version),
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

        var projectUrl = GetProjectUrl(project);
        
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
            Url = projectUrl,
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
                        $"[Changelog]({projectUrl}/changelog)", 
                        $"[Version Info]({GetVersionUrl(project, version)})")
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
            sb.Append(Format.Url(p.Title, GetProjectUrl(p)));
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
            Url = GetUserUrl(user),
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