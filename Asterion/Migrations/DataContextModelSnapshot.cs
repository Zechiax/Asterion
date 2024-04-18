﻿// <auto-generated />
using System;
using Asterion.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Asterion.Migrations
{
    [DbContext(typeof(DataContext))]
    partial class DataContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "8.0.4");

            modelBuilder.Entity("Asterion.Database.Models.Array", b =>
                {
                    b.Property<ulong>("ArrayId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("GuildId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Type")
                        .HasColumnType("INTEGER");

                    b.HasKey("ArrayId");

                    b.ToTable("Arrays");
                });

            modelBuilder.Entity("Asterion.Database.Models.Guild", b =>
                {
                    b.Property<ulong>("GuildId")
                        .HasColumnType("INTEGER");

                    b.Property<bool?>("Active")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER")
                        .HasDefaultValue(true);

                    b.Property<DateTime>("Created")
                        .HasColumnType("TEXT");

                    b.Property<ulong>("GuildSettingsId")
                        .HasColumnType("INTEGER");

                    b.Property<ulong?>("ManageRole")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("ModrinthArrayId")
                        .HasColumnType("INTEGER");

                    b.Property<ulong?>("PingRole")
                        .HasColumnType("INTEGER");

                    b.HasKey("GuildId");

                    b.HasIndex("ModrinthArrayId")
                        .IsUnique();

                    b.ToTable("Guilds");
                });

            modelBuilder.Entity("Asterion.Database.Models.GuildSettings", b =>
                {
                    b.Property<ulong>("GuildSettingsId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<long>("ChangeLogMaxLength")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER")
                        .HasDefaultValue(2000L);

                    b.Property<int>("ChangelogStyle")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER")
                        .HasDefaultValue(0);

                    b.Property<bool?>("CheckMessagesForModrinthLink")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER")
                        .HasDefaultValue(false);

                    b.Property<ulong>("GuildId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("MessageStyle")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER")
                        .HasDefaultValue(0);

                    b.Property<bool?>("RemoveOnLeave")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER")
                        .HasDefaultValue(true);

                    b.Property<bool?>("ShowChannelSelection")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER")
                        .HasDefaultValue(true);

                    b.Property<bool?>("ShowSubscribeButton")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER")
                        .HasDefaultValue(true);

                    b.HasKey("GuildSettingsId");

                    b.HasIndex("GuildId")
                        .IsUnique();

                    b.ToTable("GuildSettings");
                });

            modelBuilder.Entity("Asterion.Database.Models.ModrinthEntry", b =>
                {
                    b.Property<ulong>("EntryId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("ArrayId")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("Created")
                        .HasColumnType("TEXT");

                    b.Property<ulong?>("CustomPingRole")
                        .HasColumnType("INTEGER");

                    b.Property<ulong?>("CustomUpdateChannel")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("GuildId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("ProjectId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("EntryId");

                    b.HasIndex("ArrayId");

                    b.HasIndex("GuildId");

                    b.HasIndex("ProjectId");

                    b.ToTable("ModrinthEntries");
                });

            modelBuilder.Entity("Asterion.Database.Models.ModrinthInstanceStatistics", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("Authors")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Files")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Projects")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Versions")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("ModrinthInstanceStatistics");
                });

            modelBuilder.Entity("Asterion.Database.Models.ModrinthProject", b =>
                {
                    b.Property<string>("ProjectId")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("Created")
                        .HasColumnType("TEXT");

                    b.Property<string>("LastCheckVersion")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("LastUpdated")
                        .HasColumnType("TEXT");

                    b.Property<string>("Title")
                        .HasColumnType("TEXT");

                    b.HasKey("ProjectId");

                    b.ToTable("ModrinthProjects");
                });

            modelBuilder.Entity("Asterion.Database.Models.TotalDownloads", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("Downloads")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Followers")
                        .HasColumnType("INTEGER");

                    b.Property<string>("ProjectId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("Timestamp")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("ProjectId");

                    b.ToTable("TotalDownloads");
                });

            modelBuilder.Entity("Asterion.Database.Models.Guild", b =>
                {
                    b.HasOne("Asterion.Database.Models.Array", "ModrinthArray")
                        .WithOne("Guild")
                        .HasForeignKey("Asterion.Database.Models.Guild", "ModrinthArrayId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("ModrinthArray");
                });

            modelBuilder.Entity("Asterion.Database.Models.GuildSettings", b =>
                {
                    b.HasOne("Asterion.Database.Models.Guild", "Guild")
                        .WithOne("GuildSettings")
                        .HasForeignKey("Asterion.Database.Models.GuildSettings", "GuildId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Guild");
                });

            modelBuilder.Entity("Asterion.Database.Models.ModrinthEntry", b =>
                {
                    b.HasOne("Asterion.Database.Models.Array", "Array")
                        .WithMany()
                        .HasForeignKey("ArrayId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Asterion.Database.Models.Guild", "Guild")
                        .WithMany()
                        .HasForeignKey("GuildId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Asterion.Database.Models.ModrinthProject", "Project")
                        .WithMany()
                        .HasForeignKey("ProjectId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Array");

                    b.Navigation("Guild");

                    b.Navigation("Project");
                });

            modelBuilder.Entity("Asterion.Database.Models.TotalDownloads", b =>
                {
                    b.HasOne("Asterion.Database.Models.ModrinthProject", "Project")
                        .WithMany("TotalDownloads")
                        .HasForeignKey("ProjectId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Project");
                });

            modelBuilder.Entity("Asterion.Database.Models.Array", b =>
                {
                    b.Navigation("Guild")
                        .IsRequired();
                });

            modelBuilder.Entity("Asterion.Database.Models.Guild", b =>
                {
                    b.Navigation("GuildSettings")
                        .IsRequired();
                });

            modelBuilder.Entity("Asterion.Database.Models.ModrinthProject", b =>
                {
                    b.Navigation("TotalDownloads");
                });
#pragma warning restore 612, 618
        }
    }
}
