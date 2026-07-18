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

### Docker (linux/amd64)

Production images **must** be built for `linux/amd64` (server is amd64; Mac Apple Silicon is arm64).

```bash
# Build + push to Docker Hub
./scripts/docker-build-push.sh
# equivalent:
docker build --platform linux/amd64 -t ingluigii/btw-template-pdf:latest .
docker push ingluigii/btw-template-pdf:latest
```

Local API + Postgres:

```bash
cp .env.example .env   # edit secrets
docker compose up -d --build
# API: http://localhost:8080
```

On the server (after Hub push):

```bash
docker pull --platform linux/amd64 ingluigii/btw-template-pdf:latest
docker compose up -d
```

Optional Docker Postgres only (port **5433**, user/password `btw`): `docker compose up -d postgres` then point the connection string to that port.

## Run

```bash
dotnet run --project src/Btw.TemplatePdf.Api --launch-profile http
```

API listens on `http://localhost:5299`.

Swagger UI (Development, or `Swagger:Enabled=true`):

- Local: http://localhost:5299/swagger
- Docker: http://localhost:8080/swagger

Use **Authorize** with the studio FE JWT (`Bearer <token>`).

### Templates (studio)

| Method | Path |
|---|---|
| GET | `/api/v1/templates` |
| GET | `/api/v1/templates/{id}` |
| POST | `/api/v1/templates` |
| PUT | `/api/v1/templates/{id}/draft` |

`PUT .../draft` with `status: "draft"` saves content; with `status: "published"` republishes the current tip (content fields optional). Create requires `name`, `documentType`, and `nit`.

### UBL by CUFE (GetDocumentFromDian)

Same FE call as ARService `DianDocument/GetUbl` / `FeDocumentClient`:

```http
GET /api/v1/ubl/by-cufe?cufe={CUFE}&typeDocument=UBL
```

Upstream:
`{FeDian:BaseUrl}clientDian/ClientWcfDian/GetDocumentFromDian/{cufe}/{Environment}/false?typeDocument=UBL`

Configure `FeDian` in `appsettings` (`BaseUrl` = URL_FE, `AuthKey` = FeAuthKey).

- Test: `http://192.168.12.70:37128/` (same as netframework `Web.config` / APIService)
- Prod: `http://192.168.10.55:37128/`

If FE is unreachable and `AllowStubFallback` is true, PDF generation can still use the demo UBL for NIT `900000000`.

### Auth (studio → API → FE)

The studio login token (same FE `auth/Authentication`) should be sent as:

```http
Authorization: Bearer <fe-jwt>
```

TemplatePdf forwards it to `GetDocumentFromDian`. No separate login is required when the Bearer is valid.

### PDF engine

HTML+CSS from the published template is filled with invoice placeholders, then printed with **Microsoft Playwright** (Chromium, MIT). First machine setup:

```bash
pwsh src/Btw.TemplatePdf.Api/bin/Debug/net10.0/playwright.ps1 install chromium
```

Demo PDF request: see `Btw.TemplatePdf.Api.http` (NIT `900000000`).

## Contract

Aligned with `btw-template-studio/docs/pdf-api-contract.md`.
