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
    public static string GetProjectUrl(Project project)
    {
        return $"https://modrinth.com/{(project.ProjectType == ProjectType.Mod ? "mod" : "modpack")}/{project.Id}";
    }

    public static string GetVersionUrl(Project project, Version version)
    {
        return $"{GetProjectUrl(project)}/version/{version.Id}";
    }

    /// <summary>
    /// Creates embed with basic project info
    /// </summary>
    /// <param name="project">The project from which to get the info</param>
    /// <returns>Embed builder, which can be further edited</returns>
    public static EmbedBuilder GetProjectEmbed(Project project)
    {
        var embed = new EmbedBuilder
        {
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

    public static EmbedBuilder VersionUpdateEmbed(Project project, Version version)
    {
        var sbFiles = new StringBuilder();

        foreach (var file in version.Files)
        {
            sbFiles.AppendLine($"[{file.FileName}]({file.Url}) | {ByteSize.FromBytes(file.Size).Humanize()}");
        }

        var changelog = string.IsNullOrEmpty(version.Changelog) ? "\n\n*No changelog provided*" : $"\n\n{Format.Underline(Format.Bold("Changelog:"))}\n" +
            $"{version.Changelog}".Truncate(2000);

        var projectUrl = GetProjectUrl(project);

        var embed = new EmbedBuilder
        {
            Author = new EmbedAuthorBuilder
            {
                Name = $"Modrinth | {project.ProjectType.ToString()}",
                IconUrl = "https://avatars.githubusercontent.com/u/67560307",
                Url = "https://modrinth.com/"
            },
            Footer = new EmbedFooterBuilder
            {
                Text = "Published"
            },
            Title = $"{project.Title} | New Version Found",
            Description = $"Version **{version.VersionNumber}** has been uploaded to Modrinth" +
                          changelog,
            Url = projectUrl,
            ThumbnailUrl = project.IconUrl,
            ImageUrl = null,
            Fields = new List<EmbedFieldBuilder>
            {
                new()
                {
                    Name = "Type",
                    Value = version.VersionType.Humanize(),
                    IsInline = true
                },
                new()
                {
                    Name = "MC Version",
                    Value = string.Join(", ",version.GameVersions),
                    IsInline = true
                },
                new()
                {
                    Name = "Loaders",
                    Value = string.Join(", ", version.Loaders).Transform(To.TitleCase),
                    IsInline = true
                },
                new()
                {
                    Name = $"Files",
                    Value = sbFiles.ToString(),
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
            Color = GetColorByProjectVersionType(version.VersionType)
        };

        return embed;
    }
    
    private static Color GetColorByProjectVersionType(VersionType type)
    {
        return type switch
        {
            VersionType.Alpha => new Color(219, 49, 98),
            VersionType.Beta => new Color(247, 187, 67),
            VersionType.Release => new Color(27, 217, 106),
            _ => Color.Default
        };
    }
}