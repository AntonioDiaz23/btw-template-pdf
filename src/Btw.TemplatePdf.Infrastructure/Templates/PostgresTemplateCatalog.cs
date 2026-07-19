using Btw.TemplatePdf.Application.Templates;
using Btw.TemplatePdf.Infrastructure.Persistence;

namespace Btw.TemplatePdf.Infrastructure.Templates;

public sealed partial class PostgresTemplateCatalog : ITemplateCatalog
{
    private readonly TemplateDbContext _db;

    public PostgresTemplateCatalog(TemplateDbContext db)
    {
        _db = db;
    }
}
