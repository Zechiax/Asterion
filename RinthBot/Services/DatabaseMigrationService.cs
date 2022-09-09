﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RinthBot.Database;

namespace RinthBot.Services;

public class DatabaseMigrationService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DatabaseMigrationService> _logger;

    private bool Migrated { get; set; } = false;

    public DatabaseMigrationService(IServiceProvider services)
    {
        _services = services;
        _logger = services.GetRequiredService<ILogger<DatabaseMigrationService>>();
    }

    public void MigrateDatabase()
    {
        if (Migrated)
        {
            _logger.LogWarning("Database migration requested, database migration has already been run");
            return;
        }

        _logger.LogInformation("Migrating database");
        using var scope = _services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        db.Database.Migrate();
        _logger.LogInformation("Migration completed");

        Migrated = true;
    }
}