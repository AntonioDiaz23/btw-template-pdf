using Microsoft.EntityFrameworkCore;

namespace Btw.TemplatePdf.Infrastructure.Persistence;

public sealed class TemplateDbContext : DbContext
{
    public TemplateDbContext(DbContextOptions<TemplateDbContext> options)
        : base(options)
    {
    }

    public DbSet<TemplateEntity> Templates => Set<TemplateEntity>();
    public DbSet<TemplateVersionEntity> TemplateVersions => Set<TemplateVersionEntity>();
    public DbSet<InvoiceTemplateBindingEntity> InvoiceTemplateBindings => Set<InvoiceTemplateBindingEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TemplateEntity>(entity =>
        {
            entity.ToTable("templates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.DocumentType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Nit).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => new { x.Nit, x.DocumentType, x.Status });
            entity.HasMany(x => x.Versions)
                .WithOne(x => x.Template)
                .HasForeignKey(x => x.TemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TemplateVersionEntity>(entity =>
        {
            entity.ToTable("template_versions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.HasIndex(x => new { x.TemplateId, x.VersionNumber }).IsUnique();
            entity.Property(x => x.Html).HasColumnType("text");
            entity.Property(x => x.Css).HasColumnType("text");
            entity.Property(x => x.SchemaJson).HasColumnType("text");
            entity.Property(x => x.SampleDataJson).HasColumnType("text");
            entity.Property(x => x.BlocksJson).HasColumnType("text");
            entity.Property(x => x.PageJson).HasColumnType("text");
            entity.Property(x => x.AssetsJson).HasColumnType("text");
        });

        modelBuilder.Entity<InvoiceTemplateBindingEntity>(entity =>
        {
            entity.ToTable("invoice_template_bindings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Nit).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Cufe).HasMaxLength(128).IsRequired();
            entity.Property(x => x.DocumentType).HasMaxLength(40).IsRequired();
            entity.HasIndex(x => x.Cufe).IsUnique();
            entity.HasIndex(x => new { x.Nit, x.Cufe }).IsUnique();
        });
    }
}
