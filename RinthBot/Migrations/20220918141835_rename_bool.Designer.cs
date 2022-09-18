﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RinthBot.Database;

#nullable disable

namespace RinthBot.Migrations
{
    [DbContext(typeof(DataContext))]
    [Migration("20220918141835_rename_bool")]
    partial class rename_bool
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "6.0.9");

            modelBuilder.Entity("RinthBot.Database.Models.Array", b =>
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

            modelBuilder.Entity("RinthBot.Database.Models.Guild", b =>
                {
                    b.Property<ulong>("GuildId")
                        .HasColumnType("INTEGER");

                    b.Property<bool?>("Active")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER")
                        .HasDefaultValue(true);

                    b.Property<DateTime>("Created")
                        .HasColumnType("TEXT");

                    b.Property<bool?>("HideChannelSelection")
                        .HasColumnType("INTEGER");

                    b.Property<ulong?>("ManageRole")
                        .HasColumnType("INTEGER");

                    b.Property<int>("MessageStyle")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("ModrinthArrayId")
                        .HasColumnType("INTEGER");

                    b.Property<ulong?>("PingRole")
                        .HasColumnType("INTEGER");

                    b.Property<bool?>("RemoveOnLeave")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER")
                        .HasDefaultValue(true);

                    b.Property<ulong?>("UpdateChannel")
                        .HasColumnType("INTEGER");

                    b.HasKey("GuildId");

                    b.HasIndex("ModrinthArrayId")
                        .IsUnique();

                    b.ToTable("Guilds");
                });

            modelBuilder.Entity("RinthBot.Database.Models.ModrinthEntry", b =>
                {
                    b.Property<ulong>("EntryId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("ArrayId")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("Created")
                        .HasColumnType("TEXT");

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

            modelBuilder.Entity("RinthBot.Database.Models.ModrinthProject", b =>
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

            modelBuilder.Entity("RinthBot.Database.Models.Guild", b =>
                {
                    b.HasOne("RinthBot.Database.Models.Array", "ModrinthArray")
                        .WithOne("Guild")
                        .HasForeignKey("RinthBot.Database.Models.Guild", "ModrinthArrayId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("ModrinthArray");
                });

            modelBuilder.Entity("RinthBot.Database.Models.ModrinthEntry", b =>
                {
                    b.HasOne("RinthBot.Database.Models.Array", "Array")
                        .WithMany()
                        .HasForeignKey("ArrayId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("RinthBot.Database.Models.Guild", "Guild")
                        .WithMany()
                        .HasForeignKey("GuildId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("RinthBot.Database.Models.ModrinthProject", "Project")
                        .WithMany()
                        .HasForeignKey("ProjectId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Array");

                    b.Navigation("Guild");

                    b.Navigation("Project");
                });

            modelBuilder.Entity("RinthBot.Database.Models.Array", b =>
                {
                    b.Navigation("Guild")
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}
