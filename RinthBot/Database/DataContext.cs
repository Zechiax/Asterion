using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;

namespace RinthBot.Database
{
    public partial class DataContext : DbContext
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
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite("DataSource=data.sqlite");
                optionsBuilder.LogTo(Log.Logger.Error, LogLevel.Error, null);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Guild>().Property(p => p.Active).HasDefaultValue(true);
            modelBuilder.Entity<Guild>().Property(p => p.RemoveOnLeave).HasDefaultValue(true);
        }
    }
}
