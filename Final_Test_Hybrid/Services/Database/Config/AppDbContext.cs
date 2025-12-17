using Final_Test_Hybrid.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace Final_Test_Hybrid.Services.Database.Config;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<BoilerType> BoilerTypes => Set<BoilerType>();
    public DbSet<BoilerTypeCycle> BoilerTypeCycles => Set<BoilerTypeCycle>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<ResultSettings> ResultSettings => Set<ResultSettings>();
    public DbSet<ResultSettingHistory> ResultSettingHistories => Set<ResultSettingHistory>();
    public DbSet<StepFinalTest> StepFinalTests => Set<StepFinalTest>();
    public DbSet<StepFinalTestHistory> StepFinalTestHistories => Set<StepFinalTestHistory>();
    public DbSet<ErrorSettingsTemplate> ErrorSettingsTemplates => Set<ErrorSettingsTemplate>();
    public DbSet<ErrorSettingsHistory> ErrorSettingsHistories => Set<ErrorSettingsHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureBoilerType(modelBuilder);
        ConfigureBoilerTypeCycle(modelBuilder);
        ConfigureRecipe(modelBuilder);
        ConfigureResultSettings(modelBuilder);
        ConfigureResultSettingHistory(modelBuilder);
        ConfigureStepFinalTest(modelBuilder);
        ConfigureStepFinalTestHistory(modelBuilder);
        ConfigureErrorSettingsTemplate(modelBuilder);
        ConfigureErrorSettingsHistory(modelBuilder);
    }

    private static void ConfigureBoilerType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BoilerType>(entity =>
        {
            entity.ToTable("TB_BOILER_TYPE");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            entity.Property(e => e.Article).HasColumnName("ARTICLE").IsRequired().HasMaxLength(10);
            entity.Property(e => e.Type).HasColumnName("TYPE").IsRequired().HasMaxLength(100);
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
            entity.Property(e => e.Type).HasColumnName("TYPE").IsRequired().HasMaxLength(100);
            entity.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").IsRequired().HasDefaultValue(false);
            entity.Property(e => e.Article).HasColumnName("ARTICLE").IsRequired().HasMaxLength(10);
            entity.HasIndex(e => e.BoilerTypeId)
                .IsUnique()
                .HasFilter("\"IS_ACTIVE\" = true")
                .HasDatabaseName("IDX_TB_BOILER_TYPE_CYCLE_UNQ_ACTIVE");
        });
    }

    private static void ConfigureRecipe(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.ToTable("TB_RECIPE");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            entity.Property(e => e.BoilerTypeId).HasColumnName("BOILER_TYPE_ID").IsRequired();
            entity.Property(e => e.PlcType).HasColumnName("PLC_TYPE").IsRequired()
                .HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.IsPlc).HasColumnName("IS_PLC").HasDefaultValue(false);
            entity.Property(e => e.Address).HasColumnName("ADDRESS").IsRequired().HasMaxLength(100);
            entity.Property(e => e.TagName).HasColumnName("TAG_NAME").IsRequired().HasMaxLength(100);
            entity.Property(e => e.Value).HasColumnName("VALUE").IsRequired().HasMaxLength(255);
            entity.Property(e => e.Description).HasColumnName("DESCRIPTION").HasMaxLength(500);
            entity.Property(e => e.Unit).HasColumnName("UNIT").HasMaxLength(20);
            entity.HasOne(e => e.BoilerType)
                .WithMany()
                .HasForeignKey(e => e.BoilerTypeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.BoilerTypeId).HasDatabaseName("IDX_TB_RECIPE_BOILER_TYPE");
            entity.HasIndex(e => new { e.Address, e.BoilerTypeId })
                .IsUnique()
                .HasDatabaseName("IDX_TB_RECIPE_UNQ_ADDRESS_BOILER_TYPE");
            entity.HasIndex(e => new { e.TagName, e.BoilerTypeId })
                .IsUnique()
                .HasDatabaseName("IDX_TB_RECIPE_UNQ_TAG_NAME_BOILER_TYPE");
        });
    }

    private static void ConfigureResultSettings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ResultSettings>(entity =>
        {
            entity.ToTable("TB_RESULT_SETTINGS");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            entity.Property(e => e.ParameterName).HasColumnName("PARAMETER_NAME").IsRequired().HasMaxLength(100);
            entity.Property(e => e.AddressValue).HasColumnName("ADDRESS_VALUE").IsRequired().HasMaxLength(100);
            entity.Property(e => e.AddressMin).HasColumnName("ADDRESS_MIN").HasMaxLength(100);
            entity.Property(e => e.AddressMax).HasColumnName("ADDRESS_MAX").HasMaxLength(100);
            entity.Property(e => e.AddressStatus).HasColumnName("ADDRESS_STATUS").HasMaxLength(100);
            entity.Property(e => e.PlcType).HasColumnName("PLC_TYPE").IsRequired()
                .HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.Nominal).HasColumnName("NOMINAL").HasMaxLength(30);
            entity.Property(e => e.Unit).HasColumnName("UNIT").HasMaxLength(20);
            entity.Property(e => e.Description).HasColumnName("DESCRIPTION").HasMaxLength(500);
            entity.Property(e => e.AuditType).HasColumnName("AUDIT_TYPE").IsRequired()
                .HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.BoilerTypeId).HasColumnName("BOILER_TYPE_ID").IsRequired();
            entity.HasOne(e => e.BoilerType).WithMany().HasForeignKey(e => e.BoilerTypeId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.BoilerTypeId).HasDatabaseName("IDX_TB_RESULT_SETTINGS_BOILER_TYPE");
            entity.HasIndex(e => e.AddressMin).HasDatabaseName("IDX_TB_RESULT_SETTINGS_ADDRESS_MIN");
            entity.HasIndex(e => new { e.ParameterName, e.BoilerTypeId }).IsUnique()
                .HasDatabaseName("IDX_TB_RESULT_SETTINGS_UNQ_PARAMETER_NAME_BOILER_TYPE");
            entity.HasIndex(e => new { e.AddressValue, e.BoilerTypeId }).IsUnique()
                .HasDatabaseName("IDX_TB_RESULT_SETTINGS_UNQ_ADDRESS_VALUE_BOILER_TYPE");
        });
    }

    private static void ConfigureResultSettingHistory(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ResultSettingHistory>(entity =>
        {
            entity.ToTable("TB_RESULT_SETTING_HISTORY");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            entity.Property(e => e.ResultsSettingsId).HasColumnName("RESULTS_SETTINGS_ID").IsRequired();
            entity.Property(e => e.BoilerTypeId).HasColumnName("BOILER_TYPE_ID").IsRequired();
            entity.Property(e => e.ParameterName).HasColumnName("PARAMETER_NAME").IsRequired().HasMaxLength(100);
            entity.Property(e => e.AddressValue).HasColumnName("ADDRESS_VALUE").IsRequired().HasMaxLength(100);
            entity.Property(e => e.AddressMin).HasColumnName("ADDRESS_MIN").HasMaxLength(100);
            entity.Property(e => e.AddressMax).HasColumnName("ADDRESS_MAX").HasMaxLength(100);
            entity.Property(e => e.AddressStatus).HasColumnName("ADDRESS_STATUS").HasMaxLength(100);
            entity.Property(e => e.Nominal).HasColumnName("NOMINAL").HasMaxLength(30);
            entity.Property(e => e.PlcType).HasColumnName("PLC_TYPE").IsRequired()
                .HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.Unit).HasColumnName("UNIT").HasMaxLength(20);
            entity.Property(e => e.Description).HasColumnName("DESCRIPTION").HasMaxLength(500);
            entity.Property(e => e.AuditType).HasColumnName("AUDIT_TYPE").IsRequired()
                .HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").IsRequired().HasDefaultValue(false);
            entity.HasIndex(e => e.ResultsSettingsId).HasDatabaseName("IDX_TB_RESULT_SETTING_HISTORY_RESULTS_SETTINGS_ID");
            entity.HasIndex(e => e.BoilerTypeId).HasDatabaseName("IDX_TB_RESULT_SETTING_HISTORY_BOILER_TYPE");
            entity.HasIndex(e => e.ResultsSettingsId)
                .IsUnique()
                .HasFilter("\"IS_ACTIVE\" = true")
                .HasDatabaseName("IDX_TB_RESULT_SETTING_HISTORY_UNQ_ACTIVE");
        });
    }

    private static void ConfigureStepFinalTest(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StepFinalTest>(entity =>
        {
            entity.ToTable("TB_STEP_FINAL_TEST");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            entity.Property(e => e.Name).HasColumnName("NAME").IsRequired().HasMaxLength(500);
            entity.HasIndex(e => e.Name).IsUnique().HasDatabaseName("IDX_TB_STEP_FINAL_TEST_UNQ_NAME");
        });
    }

    private static void ConfigureStepFinalTestHistory(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StepFinalTestHistory>(entity =>
        {
            entity.ToTable("TB_STEP_FINAL_TEST_HISTORY");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            entity.Property(e => e.StepFinalTestId).HasColumnName("STEP_FINAL_TEST_ID").IsRequired();
            entity.Property(e => e.Name).HasColumnName("NAME").IsRequired().HasMaxLength(500);
            entity.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").IsRequired().HasDefaultValue(false);
            entity.HasIndex(e => e.StepFinalTestId).HasDatabaseName("IDX_TB_STEP_FINAL_TEST_HISTORY_STEP_FINAL_TEST");
            entity.HasIndex(e => e.StepFinalTestId)
                .IsUnique()
                .HasFilter("\"IS_ACTIVE\" = true")
                .HasDatabaseName("IDX_TB_STEP_FINAL_TEST_HISTORY_UNQ_ACTIVE");
        });
    }

    private static void ConfigureErrorSettingsTemplate(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ErrorSettingsTemplate>(entity =>
        {
            entity.ToTable("TB_ERROR_SETTINGS_TEMPLATE");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            entity.Property(e => e.StepId).HasColumnName("STEP_ID");
            entity.Property(e => e.AddressError).HasColumnName("ADDRESS_ERROR").IsRequired().HasMaxLength(500);
            entity.Property(e => e.Description).HasColumnName("DESCRIPTION");
            entity.HasOne(e => e.Step).WithMany().HasForeignKey(e => e.StepId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => e.StepId).HasDatabaseName("IDX_TB_ERROR_SETTINGS_TEMPLATE_STEP");
            entity.HasIndex(e => e.AddressError).HasDatabaseName("IDX_TB_ERROR_SETTINGS_TEMPLATE_ADDRESS_ERROR");
            entity.HasIndex(e => new { e.AddressError, e.StepId })
                .IsUnique()
                .HasDatabaseName("IDX_TB_ERROR_SETTINGS_TEMPLATE_UNQ_ADDRESS_ERROR_STEP");
        });
    }

    private static void ConfigureErrorSettingsHistory(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ErrorSettingsHistory>(entity =>
        {
            entity.ToTable("TB_ERROR_SETTINGS_HISTORY");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            entity.Property(e => e.ErrorSettingsTemplateId).HasColumnName("ERROR_SETTINGS_TEMPLATE_ID").IsRequired();
            entity.Property(e => e.StepHistoryId).HasColumnName("STEP_HISTORY_ID");
            entity.Property(e => e.AddressError).HasColumnName("ADDRESS_ERROR").IsRequired().HasMaxLength(500);
            entity.Property(e => e.Description).HasColumnName("DESCRIPTION");
            entity.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").IsRequired().HasDefaultValue(false);
            entity.HasOne(e => e.ErrorSettingsTemplate).WithMany().HasForeignKey(e => e.ErrorSettingsTemplateId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.StepHistory).WithMany().HasForeignKey(e => e.StepHistoryId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => e.ErrorSettingsTemplateId).HasDatabaseName("IDX_TB_ERROR_SETTINGS_HISTORY_ERROR_SETTINGS_TEMPLATE");
            entity.HasIndex(e => e.StepHistoryId).HasDatabaseName("IDX_TB_ERROR_SETTINGS_HISTORY_STEP_HISTORY");
            entity.HasIndex(e => e.AddressError).HasDatabaseName("IDX_TB_ERROR_SETTINGS_HISTORY_ADDRESS_ERROR");
            entity.HasIndex(e => e.ErrorSettingsTemplateId)
                .IsUnique()
                .HasFilter("\"IS_ACTIVE\" = true")
                .HasDatabaseName("IDX_TB_ERROR_SETTINGS_HISTORY_UNQ_ACTIVE");
        });
    }
}
