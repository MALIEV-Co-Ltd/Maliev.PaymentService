using Microsoft.EntityFrameworkCore;
using Maliev.PaymentService.Data.Database.PaymentContext;

namespace Maliev.PaymentService.Data.Database.PaymentContext
{
    public class PaymentContext : DbContext
    {
        public PaymentContext(DbContextOptions<PaymentContext> options) : base(options)
        {
        }

        public DbSet<Account> Accounts { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<PaymentDirection> PaymentDirections { get; set; }
        public DbSet<PaymentFile> PaymentFiles { get; set; }
        public DbSet<PaymentMethod> PaymentMethods { get; set; }
        public DbSet<PaymentType> PaymentTypes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Account entity
            modelBuilder.Entity<Account>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Balance).IsRequired().HasColumnType("decimal(18,2)");
            });

            // Configure Payment entity
            modelBuilder.Entity<Payment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).IsRequired().HasColumnType("decimal(18,2)");
                entity.Property(e => e.Date).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.HasOne(d => d.Account)
                      .WithMany(p => p.Payments)
                      .HasForeignKey(d => d.AccountId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(d => d.PaymentDirection)
                      .WithMany(p => p.Payments)
                      .HasForeignKey(d => d.PaymentDirectionId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(d => d.PaymentMethod)
                      .WithMany(p => p.Payments)
                      .HasForeignKey(d => d.PaymentMethodId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(d => d.PaymentType)
                      .WithMany(p => p.Payments)
                      .HasForeignKey(d => d.PaymentTypeId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure PaymentDirection entity
            modelBuilder.Entity<PaymentDirection>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            });

            // Configure PaymentFile entity
            modelBuilder.Entity<PaymentFile>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.FilePath).IsRequired().HasMaxLength(1000);
                entity.Property(e => e.UploadDate).IsRequired();
                entity.HasOne(d => d.Payment)
                      .WithMany(p => p.PaymentFiles)
                      .HasForeignKey(d => d.PaymentId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure PaymentMethod entity
            modelBuilder.Entity<PaymentMethod>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            });

            // Configure PaymentType entity
            modelBuilder.Entity<PaymentType>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            });
        }
    }
}