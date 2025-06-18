using Microsoft.EntityFrameworkCore;
using ClassLibrary_Core.Drone;
using ClassLibrary_Core.Mission;
using System.Text.Json;

namespace WebApplication.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Drone> Drones { get; set; }
        public DbSet<MissionHistory> MissionHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Drone>(entity =>
            {
                entity.ToTable("Drones");
                entity.HasKey(e => e.Id);
                
                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.ModelStatus)
                    .IsRequired()
                    .HasConversion<string>();

                entity.Property(e => e.CurrentPosition)
                    .HasColumnType("nvarchar(max)")
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                        v => JsonSerializer.Deserialize<GPSPosition>(v, (JsonSerializerOptions)null));

                entity.Property(e => e.Status)
                    .IsRequired()
                    .HasConversion<string>();

                entity.Property(e => e.cpu_used_rate)
                    .HasColumnType("decimal(5,2)");

                entity.Property(e => e.radius)
                    .HasColumnType("decimal(10,2)");

                entity.Property(e => e.left_bandwidth)
                    .HasColumnType("decimal(10,2)");

                entity.Property(e => e.memory)
                    .HasColumnType("decimal(10,2)");

                entity.Property(e => e.ConnectedDroneIds)
                    .HasColumnType("nvarchar(max)")
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                        v => JsonSerializer.Deserialize<List<int>>(v, (JsonSerializerOptions)null));

                entity.Property(e => e.AssignedSubTasks)
                    .HasColumnType("nvarchar(max)")
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                        v => JsonSerializer.Deserialize<List<SubTask>>(v, (JsonSerializerOptions)null));
            });

            modelBuilder.Entity<MissionHistory>(entity =>
            {
                entity.ToTable("MissionHistories");
                entity.HasKey(e => e.SubTaskId);

                entity.Property(e => e.SubTaskDescription)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(e => e.Operation)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.DroneName)
                    .HasMaxLength(100);

                entity.Property(e => e.Time)
                    .IsRequired();

                // 添加索引以提高查询性能
                entity.HasIndex(e => e.DroneName);
                entity.HasIndex(e => e.Time);
                entity.HasIndex(e => new { e.DroneName, e.Time });
            });
        }
    }
} 