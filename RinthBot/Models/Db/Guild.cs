namespace RinthBot.Models.Db;

public class Guild
{
    public ulong Id { get; set; }
    public ulong? UpdateChannel { get; set; }
    public int MessageStyle { get; set; }
    public bool RemoveOnLeave { get; set; }
    public int Active { get; set; }
    public ulong SubscribedProjectsArrayId { get; set; }
    public ulong? PingRole { get; set; }
    public ulong? ManageRole { get; set; }
}