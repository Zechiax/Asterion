namespace RinthBot.Models.Db;

public class ModrinthProject
{
    public string Id { get; set; } = null!;
    public string? LastCheckVersion { get; set; }
    public string? Title { get; set; }
}