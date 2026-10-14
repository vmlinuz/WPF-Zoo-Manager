using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using WPF_Zoo_Manager.Models;

namespace WPF_Zoo_Manager.Data;

public partial class ZooContext : DbContext
{
    public ZooContext(DbContextOptions<ZooContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Animal> Animals { get; set; }

    public virtual DbSet<Zoo> Zoos { get; set; }

    public virtual DbSet<ZooAnimal> ZooAnimals { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Animal>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__tmp_ms_x__3214EC070A7F6E46");

            entity.ToTable("Animal");

            entity.Property(e => e.Name).HasMaxLength(50);
        });

        modelBuilder.Entity<Zoo>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__tmp_ms_x__3214EC075DB6797F");

            entity.ToTable("Zoo");

            entity.Property(e => e.Location).HasMaxLength(50);
        });

        modelBuilder.Entity<ZooAnimal>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ZooAnima__3214EC072E7D83F4");

            entity.ToTable("ZooAnimal");

            entity.HasIndex(e => new { e.ZooId, e.AnimalId }, "UQ_ZooAnimal_ZooId_AnimalId").IsUnique();

            entity.HasOne(d => d.Animal).WithMany(p => p.ZooAnimals)
                .HasForeignKey(d => d.AnimalId)
                .HasConstraintName("AnimalFK");

            entity.HasOne(d => d.Zoo).WithMany(p => p.ZooAnimals)
                .HasForeignKey(d => d.ZooId)
                .HasConstraintName("ZooFK");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
