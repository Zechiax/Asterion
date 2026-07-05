using Asterion.Database;
using Asterion.Database.Models;
using Microsoft.EntityFrameworkCore;
using Modrinth;
using Array = Asterion.Database.Models.Array;

// Popular, frequently updated Modrinth project slugs used as test data.
// Edit this list to seed different projects.
string[] PopularProjectSlugs =
[
    "sodium",
    "lithium",
    "fabric-api",
    "iris",
    "cloth-config",
    "modmenu",
    "starlight",
    "ferrite-core"
];

if (args.Length < 2 || !ulong.TryParse(args[0], out var guildId) || !ulong.TryParse(args[1], out var channelId))
{
    Console.WriteLine("Usage: dotnet run --project Seeder -- <guildId> <channelId> [versionsBack] [projectCount] [dbPath]");
    return 1;
}

var versionsBack = args.Length > 2 && int.TryParse(args[2], out var vb) ? vb : 3;
var projectCount = args.Length > 3 && int.TryParse(args[3], out var pc) ? pc : 3;
var dbPath = args.Length > 4 ? args[4] : "data.sqlite";

if (versionsBack < 1)
{
    Console.WriteLine("versionsBack must be at least 1.");
    return 1;
}

var slugs = PopularProjectSlugs.Take(Math.Clamp(projectCount, 1, PopularProjectSlugs.Length)).ToArray();

var modrinth = new ModrinthClient(new ModrinthClientConfig
{
    UserAgent = "Zechiax/Asterion-Seeder",
    RateLimitRetryCount = 3
});

var options = new DbContextOptionsBuilder<DataContext>()
    .UseSqlite($"Data Source={dbPath}")
    .Options;

await using var db = new DataContext(options);

var guild = await db.Guilds.Include(g => g.ModrinthArray).SingleOrDefaultAsync(g => g.GuildId == guildId);
if (guild is null)
{
    var arrayEntry = db.Arrays.Add(new Array { Type = ArrayType.Modrinth });
    var guildSettingsEntry = db.GuildSettings.Add(new GuildSettings());
    var guildEntry = db.Guilds.Add(new Guild
    {
        GuildId = guildId,
        ModrinthArray = arrayEntry.Entity,
        GuildSettings = guildSettingsEntry.Entity,
        Created = DateTime.Now
    });
    guildSettingsEntry.Entity.Guild = guildEntry.Entity;
    arrayEntry.Entity.Guild = guildEntry.Entity;
    await db.SaveChangesAsync();
    guild = guildEntry.Entity;
    Console.WriteLine($"Created guild {guildId}.");
}

foreach (var slug in slugs)
{
    Console.WriteLine($"--- {slug} ---");

    Modrinth.Models.Project project;
    IReadOnlyList<Modrinth.Models.Version> versions;
    try
    {
        project = await modrinth.Project.GetAsync(slug);
        versions = await modrinth.Version.GetProjectVersionListAsync(project.Id);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Skipped: failed to fetch from Modrinth ({ex.Message})");
        continue;
    }

    if (versions.Count < 2)
    {
        Console.WriteLine("  Skipped: project has fewer than 2 published versions, nothing real to rewind to.");
        continue;
    }

    var baselineIndex = Math.Min(versionsBack, versions.Count - 1);
    var baselineVersion = versions[baselineIndex];

    var dbProject = await db.ModrinthProjects.FindAsync(project.Id);
    if (dbProject is null)
    {
        dbProject = new ModrinthProject { ProjectId = project.Id, Created = DateTime.Now };
        db.ModrinthProjects.Add(dbProject);
    }

    dbProject.Title = project.Title;
    dbProject.LastCheckVersion = baselineVersion.Id;
    dbProject.LastUpdated = baselineVersion.DatePublished;

    var entryExists = await db.ModrinthEntries.AnyAsync(e => e.GuildId == guildId && e.ProjectId == project.Id);
    if (!entryExists)
    {
        db.ModrinthEntries.Add(new ModrinthEntry
        {
            ArrayId = guild.ModrinthArrayId,
            ProjectId = project.Id,
            GuildId = guildId,
            CustomUpdateChannel = channelId,
            Created = DateTime.Now
        });
    }

    await db.SaveChangesAsync();

    Console.WriteLine($"  Rewound to version {baselineVersion.VersionNumber} ({baselineVersion.Id}), published {baselineVersion.DatePublished:u}.");
    Console.WriteLine($"  {baselineIndex} version(s) newer than that are now \"unseen\" and should notify.");
}

Console.WriteLine();
Console.WriteLine("Done. Start the bot against the same database, then run /force-update in the target guild");
Console.WriteLine("(or wait up to 10 minutes for the normal update-detection pass) to see the notifications sent.");

return 0;
