using DwaarixVMS.Models;
using Microsoft.EntityFrameworkCore;

namespace DwaarixVMS.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Resident> Residents => Set<Resident>();
    public DbSet<Visitor> Visitors => Set<Visitor>();
    public DbSet<PreApproval> PreApprovals => Set<PreApproval>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<Emergency> Emergencies => Set<Emergency>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>().ToTable("Users");
        b.Entity<AppUser>().HasIndex(x => x.Username).IsUnique();
        b.Entity<Visitor>().HasIndex(x => x.VisitorCode).IsUnique();
        b.Entity<Visitor>().HasIndex(x => x.Status);
        b.Entity<Notification>().HasIndex(x => new { x.UserId, x.IsRead });

        // Nothing is deleted from the database; records are deactivated instead.
        foreach (var fk in b.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }
}
