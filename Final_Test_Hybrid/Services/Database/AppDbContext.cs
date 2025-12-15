using Final_Test_Hybrid.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace Final_Test_Hybrid.Services.Database;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<BoilerType> BoilerTypes => Set<BoilerType>();
    public DbSet<BoilerTypeCycle> BoilerTypeCycles => Set<BoilerTypeCycle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureBoilerType(modelBuilder);
        ConfigureBoilerTypeCycle(modelBuilder);
    }

    private static void ConfigureBoilerType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BoilerType>(entity =>
        {
            entity.ToTable("TB_BOILER_TYPE");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            entity.Property(e => e.Article).HasColumnName("ARTICLE").IsRequired().HasMaxLength(10);
            entity.Property(e => e.Name).HasColumnName("NAME").IsRequired();
            entity.Property(e => e.Version).HasColumnName("VERSION").IsConcurrencyToken();
            entity.HasIndex(e => e.Article).IsUnique().HasDatabaseName("IDX_TB_BOILER_TYPE_UNQ_ARTICLE");
        });
    }

    private static void ConfigureBoilerTypeCycle(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BoilerTypeCycle>(entity =>
        {
            entity.ToTable("TB_BOILER_TYPE_CYCLE");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            entity.Property(e => e.BoilerTypeId).HasColumnName("BOILER_TYPE_ID").IsRequired();
            entity.Property(e => e.Type).HasColumnName("TYPE").IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").IsRequired().HasDefaultValue(false);
            entity.Property(e => e.Article).HasColumnName("ARTICLE").IsRequired();
            entity.Property(e => e.Version).HasColumnName("VERSION").IsConcurrencyToken();
        });
    }
}
