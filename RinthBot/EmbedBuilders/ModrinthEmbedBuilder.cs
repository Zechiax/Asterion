using System.Text;
using Discord;
using Humanizer;
using Humanizer.Bytes;
using Modrinth.RestClient.Models;
using Modrinth.RestClient.Models.Enums;
using RinthBot.Extensions;
using Version = Modrinth.RestClient.Models.Version;

namespace RinthBot.EmbedBuilders;

public static class ModrinthEmbedBuilder
{
    private static readonly Color ModrinthColor = new Color(27, 217, 106);

    /// <summary>
    /// Limit for the length of value of fields on embed (Discord limit is 1024)
    /// </summary>
    private static int _embedFieldLimit = 512;
    /// <summary>
    /// Limit for the length of description on embed (Discord limit is 4096)
    /// </summary>
    private static int _descriptionLimit = 2000;
    
    /// <summary>
    /// Creates direct URL string to the specific project 
    /// </summary>
    /// <param name="project"></param>
    /// <returns></returns>
    public static string GetProjectUrl(Project project)
    {
        return $"https://modrinth.com/{GetProjectUrlType(project)}/{project.Id}";
    }

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
    
    private static EmbedAuthorBuilder GetProjectAuthor(TeamMember owner)
    {
        var embedAuthor = new EmbedAuthorBuilder
        {
            Name = owner.User.Username,
            IconUrl = owner.User.AvatarUrl,
            Url = $"https://modrinth.com/user/{owner.User.Id}"
        };

        return embedAuthor;
    }

    private static EmbedAuthorBuilder GetModrinthAuthor(Project project)
    {
        var embedAuthor = new EmbedAuthorBuilder
        {
            Name = $"Modrinth | {project.ProjectType.ToString()}",
            IconUrl = "https://avatars.githubusercontent.com/u/67560307",
            Url = "https://modrinth.com/"
        };

        return embedAuthor;
    }
    
    private static TeamMember? GetOwner(IEnumerable<TeamMember>? teamMembers)
    {
        return (teamMembers ?? Array.Empty<TeamMember>()).FirstOrDefault(x =>
            string.Equals(x.Role, "owner", StringComparison.InvariantCultureIgnoreCase));
    }

    private static EmbedAuthorBuilder GetEmbedAuthor(Project project, IEnumerable<TeamMember>? teamMembers = null)
    {
        var owner = GetOwner(teamMembers);
        
        return owner is null ? GetModrinthAuthor(project) : GetProjectAuthor(owner);
    }

    /// <summary>
    /// Creates embed with basic project info
    /// </summary>
    /// <param name="project">The project from which to get the info</param>
    /// <returns>Embed builder, which can be further edited</returns>
    public static EmbedBuilder GetProjectEmbed(Project project, IEnumerable<TeamMember>? teamMembers = null)
    {
        var author = GetEmbedAuthor(project, teamMembers);
        
        var embed = new EmbedBuilder
        {
            Author = author,
            Title = project.Title,
            Url = GetProjectUrl(project),
            Description = project.Description,
            ThumbnailUrl = project.IconUrl,
            Color = ModrinthColor,
            Fields = new List<EmbedFieldBuilder>
            {
                // Format downloads from 319803 to 319,8K 
                 new() { Name = "Downloads", Value = project.Downloads.ToMetric(decimals: 1).Transform(To.UpperCase), IsInline = true },
                // Format downloads from 319803 to 319 803
                // new() { Name = "Downloads", Value = project.Downloads.SeparateThousands(), IsInline = true },
                new() { Name = "Followers", Value = project.Followers.SeparateThousands(), IsInline = true },
                new() { Name = "Categories", Value = string.Join(", ", project.Categories).Transform(To.TitleCase), IsInline = true },
                new() { Name = "Type", Value = project.ProjectType.Humanize(), IsInline = true },
                new() { Name = "ID", Value = project.Id, IsInline = true },
                new() { Name = "Created | Last updated", Value = $"{TimestampTag.FromDateTime(project.Published, TimestampTagStyles.Relative)} | {TimestampTag.FromDateTime(project.Updated, TimestampTagStyles.Relative)}"  }
            }
        };

        return embed;
    }

    public static EmbedBuilder VersionUpdateEmbed(Project project, Version version, IEnumerable<TeamMember>? teamMembers = null)
    {
        var sbFiles = new StringBuilder();

        foreach (var file in version.Files)
        {
            sbFiles.AppendLine($"[{file.FileName}]({file.Url}) | {ByteSize.FromBytes(file.Size).Humanize()}");
        }

        var changelog = string.IsNullOrEmpty(version.Changelog) ? "\n\n*No changelog provided*" : $"\n\n{Format.Underline(Format.Bold("Changelog:"))}\n" +
            $"{version.Changelog}".Truncate(_descriptionLimit);

        var projectUrl = GetProjectUrl(project);
        
        var embedAuthor = GetEmbedAuthor(project, teamMembers);

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
                    Name = "MC Version",
                    Value = string.Join(", ",version.GameVersions).Truncate(_embedFieldLimit),
                    IsInline = true
                },
                new()
                {
                    Name = "Loaders",
                    Value = string.Join(", ", version.Loaders).Transform(To.TitleCase).Truncate(_embedFieldLimit),
                    IsInline = true
                },
                new()
                {
                    Name = $"Files ({version.Files.Length})",
                    Value = sbFiles.ToString().Truncate(_embedFieldLimit),
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
            Color = version.VersionType.ToColor()
        };

        return embed;
    }
    
    /// <summary>
    /// Chooses color based on the version type
    /// </summary>
    /// <param name="versionType"></param>
    /// <returns></returns>
    private static Color ToColor(this VersionType versionType)
    {
        return versionType switch
        {
            VersionType.Alpha => new Color(219, 49, 98),
            VersionType.Beta => new Color(247, 187, 67),
            VersionType.Release => new Color(27, 217, 106),
            _ => Color.Default
        };
    }
}