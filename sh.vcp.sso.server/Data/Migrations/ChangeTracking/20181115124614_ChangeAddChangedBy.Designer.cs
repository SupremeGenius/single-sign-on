﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using sh.vcp.ldap.ChangeTracking;

namespace sh.vcp.sso.server.Migrations
{
    [DbContext(typeof(ChangeTrackingDbContext))]
    [Migration("20181115124614_ChangeAddChangedBy")]
    partial class ChangeAddChangedBy
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "2.1.4-rtm-31024")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("sh.vcp.ldap.ChangeTracking.Change", b =>
                {
                    b.Property<Guid>("Guid")
                        .ValueGeneratedOnAdd()
                        .HasColumnName("guid");

                    b.Property<string>("ChangedBy")
                        .HasColumnName("changed_by");

                    b.Property<DateTime>("ConcurrencyToken")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnName("concurrency_token");

                    b.Property<DateTime>("CreatedAt")
                        .ValueGeneratedOnAdd()
                        .HasColumnName("created_at");

                    b.Property<string>("Dn")
                        .HasColumnName("dn");

                    b.Property<string>("NewValue")
                        .HasColumnName("new_value");

                    b.Property<string>("ObjectClass")
                        .HasColumnName("object_class");

                    b.Property<string>("Property")
                        .HasColumnName("property");

                    b.Property<int>("Type")
                        .HasColumnName("type");

                    b.HasKey("Guid");

                    b.ToTable("Changes");
                });
#pragma warning restore 612, 618
        }
    }
}
