using Asterion.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Asterion.Services;

public class DatabaseMigrationService
{
    private readonly ILogger<DatabaseMigrationService> _logger;
    private readonly IServiceProvider _services;

    public DatabaseMigrationService(IServiceProvider services)
    {
        _services = services;
        _logger = services.GetRequiredService<ILogger<DatabaseMigrationService>>();
    }

    private bool Migrated { get; set; }

    public void MigrateDatabase()
    {
        if (Migrated)
        {
            _logger.LogWarning("Request for database migration rejected, the migration has already been done");
            return;
        }

        _logger.LogInformation("Migrating database");
        using var scope = _services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        db.Database.Migrate();
        _logger.LogInformation("Migration complete");

        Migrated = true;
    }
}