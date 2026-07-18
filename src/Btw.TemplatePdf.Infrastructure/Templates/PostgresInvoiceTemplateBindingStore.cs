using Btw.TemplatePdf.Domain.Abstractions;
using Btw.TemplatePdf.Domain.Common;
using Btw.TemplatePdf.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Btw.TemplatePdf.Infrastructure.Templates;

public sealed class PostgresInvoiceTemplateBindingStore : IInvoiceTemplateBindingStore
{
    private readonly TemplateDbContext _db;

    public PostgresInvoiceTemplateBindingStore(TemplateDbContext db)
    {
        _db = db;
    }

    public async Task<InvoiceTemplateBinding?> FindAsync(
        string nit,
        string cufe,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.InvoiceTemplateBindings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Cufe == cufe && x.Nit == nit,
                cancellationToken)
            .ConfigureAwait(false);

        return row is null
            ? null
            : new InvoiceTemplateBinding(
                row.Nit,
                row.Cufe,
                DocumentTypeMapper.FromApi(row.DocumentType),
                row.TemplateId,
                row.TemplateVersionNumber,
                row.BoundAt);
    }

    public async Task SaveAsync(
        InvoiceTemplateBinding binding,
        CancellationToken cancellationToken = default)
    {
        var exists = await _db.InvoiceTemplateBindings
            .AnyAsync(x => x.Cufe == binding.Cufe && x.Nit == binding.Nit, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
            return;

        _db.InvoiceTemplateBindings.Add(new InvoiceTemplateBindingEntity
        {
            Id = Guid.NewGuid(),
            Nit = binding.Nit,
            Cufe = binding.Cufe,
            DocumentType = DocumentTypeMapper.ToApi(binding.DocumentType),
            TemplateId = binding.TemplateId,
            TemplateVersionNumber = binding.TemplateVersionNumber,
            BoundAt = binding.BoundAt
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Concurrent first render for the same CUFE — keep the winner.
            _db.ChangeTracker.Clear();
        }
    }
}
