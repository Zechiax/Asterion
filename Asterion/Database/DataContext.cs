﻿using System.Text.Json;
using Asterion.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Serilog;
using Array = Asterion.Database.Models.Array;

namespace Asterion.Database;

public class DataContext : DbContext
{
    public DataContext()
    {
    }

    public DataContext(DbContextOptions<DataContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Guild> Guilds { get; set; } = null!;
    public virtual DbSet<ModrinthProject> ModrinthProjects { get; set; } = null!;
    public virtual DbSet<Array> Arrays { get; set; } = null!;
    public virtual DbSet<ModrinthEntry> ModrinthEntries { get; set; } = null!;
    public virtual DbSet<GuildSettings> GuildSettings { get; set; } = null!;
    public virtual DbSet<TotalDownloads> TotalDownloads { get; set; } = null!;
    public virtual DbSet<ModrinthInstanceStatistics> ModrinthInstanceStatistics { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured) return;

        optionsBuilder.UseSqlite("DataSource=data.sqlite");
        optionsBuilder.LogTo(Log.Logger.Error, LogLevel.Error);

        // Disables DbContext initialized messages
        optionsBuilder.ConfigureWarnings(warnings => warnings
            .Ignore(CoreEventId.ContextInitialized));

#if DEBUG
        optionsBuilder.EnableSensitiveDataLogging();
#endif
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Guild>().Property(p => p.Active).HasDefaultValue(true);
        modelBuilder.Entity<GuildSettings>().Property(p => p.RemoveOnLeave).HasDefaultValue(true);
        modelBuilder.Entity<GuildSettings>().Property(p => p.ShowChannelSelection).HasDefaultValue(true);
        modelBuilder.Entity<GuildSettings>().Property(p => p.CheckMessagesForModrinthLink).HasDefaultValue(false);
        modelBuilder.Entity<GuildSettings>().Property(p => p.ShowSubscribeButton).HasDefaultValue(true);
        modelBuilder.Entity<GuildSettings>().Property(p => p.MessageStyle).HasDefaultValue(MessageStyle.Full);
        modelBuilder.Entity<GuildSettings>().Property(p => p.ChangelogStyle).HasDefaultValue(ChangelogStyle.PlainText);
        modelBuilder.Entity<GuildSettings>().Property(p => p.ChangeLogMaxLength).HasDefaultValue(2000);

        modelBuilder.Entity<ModrinthEntry>()
            .Property(e => e.ReleaseFilter)
            .HasDefaultValue(ReleaseType.Alpha | ReleaseType.Beta | ReleaseType.Release);

        // Ignore the LoaderFilter property
        modelBuilder.Entity<ModrinthEntry>()
            .Ignore(e => e.LoaderFilter);
    }

}