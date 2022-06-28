namespace RinthBot.Models.Db;

public class ArrayProject
{
    public ulong ArrayId { get; set; }
    public string ProjectId { get; set; } = null!;
    public ulong? CustomUpdateChannel { get; set; }
}