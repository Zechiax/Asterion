using System.Data.SQLite;
using SqlKata.Execution;
using Discord.WebSocket;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modrinth.RestClient.Models;
using RinthBot.Models.Db;
using SqlKata.Compilers;
using File = System.IO.File;


namespace RinthBot.Services;

public class DataService
{
    private readonly ILogger _logger;
    private const string DbName = "data.sqlite";
    private readonly Compiler _sqLiteCompiler;
    private readonly DiscordSocketClient _client;

    public DataService(IServiceProvider serviceProvider)
    {
        _logger = serviceProvider.GetRequiredService<ILogger<DataService>>();
        _client = serviceProvider.GetRequiredService<DiscordSocketClient>();

        _sqLiteCompiler = new SqliteCompiler();

        _client.Ready += CheckAllGuilds;
        _client.JoinedGuild += RegisterNewGuild;
        _client.LeftGuild += UnregisterGuild;
    }

    public void Initialize()
    {
	    _logger.LogInformation("Initializing Data Service");
	    if (File.Exists(DbName)) return;
	    _logger.LogInformation("No database file found, creating new one..");
	    FirstTimeDbSetup();
    }

    /// <summary>
    /// Check every guild the bot is in if it is registered (as the bot might have joined the guild while being offline)
    /// </summary>
    public Task CheckAllGuilds()
    {
	    _logger.LogInformation("Checking connected guilds");
	    //TODO: Do the same for guilds that the bot is not in but have database record
	    var connectedGuilds = _client.Guilds;

	    foreach (var guild in connectedGuilds)
	    {
		    using var db = new QueryFactory(NewSqlConnection(), _sqLiteCompiler);
		    
		    var guildDb = db.Query("Guilds").Where("ID", guild.Id).Get<Guild>();
		    
		    // The guild is registered
		    if (guildDb.Any()) continue;
		    
		    _logger.LogInformation("Registering unregistered guild ID {GuildID}:{Name}", guild.Id, guild.Name);
		    RegisterNewGuild(guild);
	    }

	    _logger.LogInformation("Checking connected guilds done");

	    return Task.CompletedTask;
    }

    private static SqliteConnection NewSqlConnection()
    {
	    var builder = new SQLiteConnectionStringBuilder
	    {
		    DataSource = DbName
	    };
	    
	    return new SqliteConnection(builder.ToString());
    }

    private void FirstTimeDbSetup()
    {
        SQLiteConnection.CreateFile(DbName);
        _logger.LogInformation("Database file created");
        
        var dbConnection =
            new SQLiteConnection($"Data Source={DbName};Version=3;");
        dbConnection.Open();
        
        _logger.LogInformation("Creating tables..");
        string createArray = @"CREATE TABLE ""Arrays"" (
	""ID""	INTEGER,
	""Type""	INTEGER,
	PRIMARY KEY(""ID"" AUTOINCREMENT)
)";
        
        string createArrayItem = @"CREATE TABLE ""ArrayProjects"" (
	""ArrayId""	INTEGER,
	""ProjectId""	TEXT,
	""CustomUpdateChannel""	INTEGER,
	FOREIGN KEY(""ArrayId"") REFERENCES ""Arrays""(""ID"")
)";
        string createModrinthProject = @"CREATE TABLE ""ModrinthProjects"" (
	""ID""	TEXT NOT NULL UNIQUE,
	""LastCheckVersion""	TEXT,
	""Title""	TEXT,
	PRIMARY KEY(""ID"")
)";
        string createGuildTable = @"CREATE TABLE ""Guilds"" (
	""ID""	INTEGER NOT NULL UNIQUE,
	""UpdateChannel""	INTEGER,
	""MessageStyle""	INTEGER NOT NULL DEFAULT 0,
	""RemoveOnLeave""	INTEGER NOT NULL DEFAULT 1,
	""Active""	INTEGER NOT NULL DEFAULT 1,
	""SubscribedProjectsArrayId""	INTEGER NOT NULL,
	""PingRole""	INTEGER,
	""ManageRole""	INTEGER,
	PRIMARY KEY(""ID"")
)";
        
        var command = new SQLiteCommand(createArray, dbConnection);
        command.ExecuteNonQuery();
        command = new SQLiteCommand(createModrinthProject, dbConnection);
        command.ExecuteNonQuery();
        command = new SQLiteCommand(createArrayItem, dbConnection);
        command.ExecuteNonQuery();
        command = new SQLiteCommand(createGuildTable, dbConnection);
        command.ExecuteNonQuery();
        
        _logger.LogInformation("Tables created");
    }

    public Task RegisterNewGuild(SocketGuild guild)
    {
	    _logger.LogDebug("Inserting new guild into database (id: {Value})", guild.Id);
	    
	    using var db = new QueryFactory(NewSqlConnection(), _sqLiteCompiler);

	    var array = db.Query("arrays").InsertGetId<ulong>(new
	    {
		    Type = 0
	    });
	    
	    var affected = db.Query("guilds").Insert(new
	    {
			ID = guild.Id,
			SubscribedProjectsArrayId = array,
			/*MessageStyle = 0,
			RemoveOnLeave = true,
			Active = true*/
	    });

	    if (affected > 0)
	    {
		    _logger.LogDebug("Guild successfully inserted (id: {Value})", guild.Id);
	    }
	    else
	    {
		    _logger.LogWarning("Guild Insertion failed (id: {Value})", guild.Id);
	    }
	    
	    return Task.CompletedTask;
    }

    public Task UnregisterGuild(SocketGuild guild)
    {
	    _logger.LogInformation("Removing guild from database (id: {Value})", guild.Id);

	    using var db = new QueryFactory(NewSqlConnection(), _sqLiteCompiler);

		// Get Guild
		var guildDb = db.Query("guilds").Select("SubscribedProjectsArrayId").Where("id", guild.Id).First<Guild>();
		// Get Array
		var array = db.Query("arrays").Where("id", guildDb.SubscribedProjectsArrayId).First<Models.Db.Array>();
		
		// Remove all array items
		db.Query("ArrayProjects").Where("ArrayId", array.Id).Delete();
		
		// Remove array
		db.Query("arrays").Where("id", guildDb.SubscribedProjectsArrayId).Delete();
		
		// Remove guild
		db.Query("guilds").Where("id", guild.Id).Delete();

		_logger.LogInformation("Guild removed (id: {Value})", guild.Id);

		return Task.CompletedTask;
    }

    public Task AddWatchedProject(SocketGuild guildId, Project modrinthProject, string latestVersionId, SocketChannel? customUpdateChannel = null)
    {
	    AddWatchedProject(guildId.Id, modrinthProject, latestVersionId, customUpdateChannel?.Id);

	    return Task.CompletedTask;
    }

    public Task AddWatchedProject(ulong guildId, Project modrinthProject, string latestVersionId, ulong? customUpdateChannelId = null)
    {
	    using var db = new QueryFactory(NewSqlConnection(), _sqLiteCompiler);
	    var guildDb = db.Query("guilds").Where("ID", guildId).First<Guild>();

	    _logger.LogDebug("Adding watched project {ModrinthProject} for guild {GuildId}", modrinthProject.Id, guildId);
	    // Check if Project exists, if not, create one
	    var projects = db.Query("ModrinthProjects").Where("ID", modrinthProject.Id).Get<ModrinthProject>();
	    // No project
	    if (!projects.Any())
	    {
		    // Let's create one
		    db.Query("ModrinthProjects").Insert(new
		    {
				Id = modrinthProject.Id,
				Title = modrinthProject.Title,
				LastCheckVersion = latestVersionId
		    });
	    }

	    var arrayProjects = db.Query("ArrayProjects").Where(new
	    {
		    ArrayId = guildDb.SubscribedProjectsArrayId,
		    ProjectId = modrinthProject.Id
	    }).Get();
	    // The project is already being watched
	    if (arrayProjects.Any())
	    {
		    _logger.LogDebug("The project {ModrinthProjectId} is already being watched", modrinthProject.Id);
		    return Task.CompletedTask;
	    }
	    
		// Add project to subscribes 
	    if (customUpdateChannelId != null)
	    {
		    db.Query("ArrayProjects").Insert(new
		    {
			    ArrayId = guildDb.SubscribedProjectsArrayId,
			    ProjectId = modrinthProject.Id,
			    CustomUpdateChannel = customUpdateChannelId
		    });
	    }
	    else
	    {
		    db.Query("ArrayProjects").Insert(new
		    {
			    ArrayId = guildDb.SubscribedProjectsArrayId,
			    ProjectId = modrinthProject.Id
		    });
	    }

	    return Task.CompletedTask;
    }

    public void RemoveWatchedProject(SocketGuild guild, string modrinthProjectId)
    {
	    RemoveWatchedProject(guild.Id, modrinthProjectId);
    }

    public void RemoveWatchedProject(ulong guildId, string modrinthProjectId)
    {
	    using var db = new QueryFactory(NewSqlConnection(), _sqLiteCompiler);
	    var guildDb = db.Query("guilds").Where("ID", guildId).First<Guild>();
	    
	    // Query for searching ArrayProjects
	    var affected = db.Query("ArrayProjects").Where(new
	    {
		    ArrayId = guildDb.SubscribedProjectsArrayId,
		    ProjectId = modrinthProjectId
	    }).Delete();

	    // The ID probably didn't exist
	    if (affected == 0)
	    {
		    return;
	    }
	    
	    // Let's check if there are any more guilds subscribed to this project is, if not, we can remove the project from db
	    var projects = db.Query("ArrayProjects").Where("ProjectId", modrinthProjectId).Get();
	    // No projects, let's remove the main project from directory
	    if (!projects.Any())
	    {
		    db.Query("ModrinthProjects").Where("ID", modrinthProjectId).Delete();
	    }
    }

    public IEnumerable<ArrayProject> ListProjects(SocketGuild guild)
    {
	    using var db = new QueryFactory(NewSqlConnection(), _sqLiteCompiler);
	    var guildDb = db.Query("guilds").Where("id", guild.Id).First<Guild>();

	    var projects = db.Query("ArrayProjects").Where("ArrayId", guildDb.SubscribedProjectsArrayId).Get<ArrayProject>();

	    return projects;
    }

    public ArrayProject? GetProjectInfo(SocketGuild guild, Project project)
    {
	    return GetProjectInfo(guild.Id, project.Id);
    }
    
    public ArrayProject? GetProjectInfo(Guild guild, Project project)
    {
	    return GetProjectInfo(guild.Id, project.Id);
    }
    
    public ArrayProject? GetProjectInfo(ulong guildId, string projectId)
    {
	    using var db = new QueryFactory(NewSqlConnection(), _sqLiteCompiler);

	    var guild = GetGuild(guildId);
	    var project = db.Query("ArrayProjects").Where(new
	    {
		    ArrayId = guild.SubscribedProjectsArrayId,
		    ProjectId = projectId
	    }).First<ArrayProject>();

	    return project;
    }

    public IEnumerable<Guild> GetAllGuilds()
    {
	    using var db = new QueryFactory(NewSqlConnection(), _sqLiteCompiler);

	    var guilds = db.Query("guilds").Get<Guild>();

	    return guilds;
    }

    public IEnumerable<ArrayProject> GetGuildsSubscribedProjects(SocketGuild guild)
    {
	    return GetGuildsSubscribedProjects(guild.Id);
    }

    public IEnumerable<ArrayProject> GetGuildsSubscribedProjects(ulong guildId)
    {
	    using var db = new QueryFactory(NewSqlConnection(), _sqLiteCompiler);

	    var guild = db.Query("guilds").Where("id", guildId).Select("SubscribedProjectsArrayId").First<Guild>();

	    var projects = db.Query("ArrayProjects").Where("ArrayId", guild.SubscribedProjectsArrayId).Get<ArrayProject>();

	    return projects;
    }

    public IEnumerable<ModrinthProject> GetAllProjects()
    {
	    using var db = new QueryFactory(NewSqlConnection(), _sqLiteCompiler);

	    var projects = db.Query("ModrinthProjects").Get<ModrinthProject>();

	    return projects;
    }

    public IEnumerable<Guild> GetAllGuildsSubscribedTo(ModrinthProject project)
    {
	    using var db = new QueryFactory(NewSqlConnection(), _sqLiteCompiler);

	    // Get all array IDs of array projects which have this project's ID
	    var arrays = db.Query("ArrayProjects").Select("ArrayId").Where("ProjectId", project.Id).Get<ArrayProject>();

	    var arrayList = arrays.Select(array => array.ArrayId);

	    // Get all guilds which have this array IDs
	    var guilds = db.Query("Guilds").WhereIn("SubscribedProjectsArrayId", arrayList).Get<Guild>();

	    return guilds;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="projectId"></param>
    /// <param name="versionId"></param>
    /// <returns>Null if the version is already latest</returns>
    public ModrinthProject? UpdateProjectVersionAndReturnOldOne(string projectId, string versionId)
    {
	    // TODO: This return bool, returning null when the version is already latest is confusing
	    using var db = new QueryFactory(NewSqlConnection(), _sqLiteCompiler);

	    var version = db.Query("ModrinthProjects").Where("ID", projectId)
		    .First<ModrinthProject>();

	    // We already have the latest version in DB
	    if (versionId == version.LastCheckVersion)
	    {
		    return null;
	    }

	    db.Query("ModrinthProjects").Where("ID", projectId).Update(new
	    {
		    LastCheckVersion = versionId
	    });

	    // Return previous version
	    return version;
    }

    public void SetUpdateChannel(SocketGuild guild, SocketTextChannel channel)
    {
	    SetUpdateChannel(guild.Id, channel.Id);
    }

    public void SetUpdateChannel(ulong guildId, ulong channelId)
    {
	    using var db = new QueryFactory(NewSqlConnection(), _sqLiteCompiler);

	    db.Query("Guilds").Where("ID", guildId).Update(new
	    {
		    UpdateChannel = channelId
	    });
    }

    public bool IsGuildSubscribedToProject(Project project, SocketGuild guild)
    {
	    return IsGuildSubscribedToProject(project.Id, guild.Id);
    }

    public bool IsGuildSubscribedToProject(string projectId, ulong guildId)
    {
	    using var db = new QueryFactory(NewSqlConnection(), _sqLiteCompiler);
	    
	    // Get guild
	    var guild = db.Query("Guilds").Where("ID", guildId).Select("SubscribedProjectsArrayId").First<Guild>();
		
	    // Get all 
	    var arrayProjects = db.Query("ArrayProjects").Where(new
	    {
		    ArrayId = guild.SubscribedProjectsArrayId,
		    ProjectId = projectId
	    }).Get<ArrayProject>();
	    
	    return arrayProjects.Any();
    }

    public Guild GetGuild(SocketGuild guild)
    {
	    return GetGuild(guild.Id);
    }

    public Guild GetGuild(ulong guildId)
    {
	    using var db = new QueryFactory(NewSqlConnection(), _sqLiteCompiler);
	    
	    var guild = db.Query("Guilds").Where("ID", guildId).First<Guild>();

	    return guild;
    }

    //TODO: Rewrite queries to be more efficient
}