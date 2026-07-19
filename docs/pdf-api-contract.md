# Same contract as btw-template-studio/docs/pdf-api-contract.md

## POST /api/v1/pdf/by-cufe

### Request
```json
{
  "nit": "900000000",
  "cufe": "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
  "documentType": "factura"
}
```

### Response 200
```json
{
  "nit": "900000000",
  "cufe": "…",
  "documentType": 0,
  "templateId": "11111111-1111-1111-1111-111111111111",
  "templateVersion": 1,
  "contentType": "application/pdf",
  "fileName": "FE-900000000-00000000.pdf",
  "pdfBase64": "JVBERi0x…"
}
```

Demo NIT seeded in memory: `900000000`.

## GET /api/v1/pdf/bindings/by-cufe

Consulta si un CUFE ya fue graficado (fila en `invoice_template_bindings`).

### Query
- `nit` (required)
- `cufe` (required)

### Response 200 — ya generado
```json
{
  "exists": true,
  "nit": "900000000",
  "cufe": "…",
  "documentType": 0,
  "templateId": "11111111-1111-1111-1111-111111111111",
  "templateVersion": 1,
  "boundAt": "2026-07-18T17:29:14.317Z"
}
```

### Response 200 — nunca generado
```json
{
  "exists": false,
  "nit": "900000000",
  "cufe": "…"
}
```
