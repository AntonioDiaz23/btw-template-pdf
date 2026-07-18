# Btw.TemplatePdf

.NET 10 modular API for template-driven FE PDF generation.

## Solution layout

| Project | Role |
|---|---|
| `Domain` | `TemplateDefinition`, `InvoiceViewModel`, ports (`ITemplateStore`, `IUblStore`, …) |
| `Application` | Use case `GeneratePdfByCufeUseCase` |
| `Infrastructure` | PostgreSQL templates, stub UBL/PDF adapters |
| `Api` | REST templates CRUD + `POST /api/v1/pdf/by-cufe` |

## Prerequisites

- PostgreSQL on `localhost:5432` with database `btw_template_pdf`
- Default connection (override in `appsettings.Development.json`):

```
Host=localhost;Port=5432;Database=btw_template_pdf;Username=postgres;Password=postgres
```

Optional Docker Postgres (port **5433**, user/password `btw`): `docker compose up -d` then point the connection string to that port.

## Run

```bash
dotnet run --project src/Btw.TemplatePdf.Api --launch-profile http
```

API listens on `http://localhost:5299`.

### Templates (studio)

| Method | Path |
|---|---|
| GET | `/api/v1/templates` |
| GET | `/api/v1/templates/{id}` |
| POST | `/api/v1/templates` |
| PUT | `/api/v1/templates/{id}/draft` |
| POST | `/api/v1/templates/{id}/publish` |

Demo PDF request: see `Btw.TemplatePdf.Api.http` (NIT `900000000`).

## Contract

Aligned with `btw-template-studio/docs/pdf-api-contract.md`.
