using Microsoft.EntityFrameworkCore;

public enum CompanyRole { Owner = 0, Admin = 1, Manager = 2, Member = 3 }

public class Company
{
    public long Id { get; set; }                           // bigint identity
    public string Name { get; set; } = default!;
    public string Plan { get; set; } = "free";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CompanyMember
{
    public long Id { get; set; }                           // NEW: PK (bigint identity)
    public long CompanyId { get; set; }                    // FK -> Company.Id
    public string UserId { get; set; } = default!;
    public CompanyRole Role { get; set; } = CompanyRole.Owner;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Company? Company { get; set; }
}

public class CompanyDb : DbContext
{
    public CompanyDb(DbContextOptions<CompanyDb> opt) : base(opt) { }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyMember> CompanyMembers => Set<CompanyMember>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Company>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).UseIdentityByDefaultColumn();   // PostgreSQL identity
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Plan).HasMaxLength(40).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<CompanyMember>(e =>
        {
            e.HasKey(x => x.Id);                                   // PK = Id
            e.Property(x => x.Id).UseIdentityByDefaultColumn();    // identity
            e.Property(x => x.UserId).HasMaxLength(128).IsRequired();

            e.HasOne(x => x.Company)!.WithMany()
             .HasForeignKey(x => x.CompanyId)
             .OnDelete(DeleteBehavior.Cascade);

            // prevent duplicate memberships
            e.HasIndex(x => new { x.CompanyId, x.UserId }).IsUnique();
        });
    }
}
