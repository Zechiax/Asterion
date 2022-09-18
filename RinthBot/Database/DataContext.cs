using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using RinthBot.Database.Models;
using Serilog;
using Array = RinthBot.Database.Models.Array;

namespace RinthBot.Database
{
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

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder.IsConfigured) return;
            
            optionsBuilder.UseSqlite("DataSource=data.sqlite");
            optionsBuilder.LogTo(Log.Logger.Error, LogLevel.Error, null);
            
            // Disables DbContext initialized messages
            optionsBuilder.ConfigureWarnings(warnings => warnings
                .Ignore(CoreEventId.ContextInitialized));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Guild>().Property(p => p.Active).HasDefaultValue(true);
            modelBuilder.Entity<Guild>().Property(p => p.RemoveOnLeave).HasDefaultValue(true);
            modelBuilder.Entity<Guild>().Property(p => p.HideChannelSelection).HasDefaultValue(false);
        }
    }
}
