# Same contract as btw-template-studio/docs/pdf-api-contract.md

## POST /api/v1/pdf/by-cufe

### Request
```json
{
  "nit": "900000000",
  "cufe": "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
  "documentType": "factura",
  "templateId": null,
  "replaceBinding": false
}
```

Optional fields:
- `templateId` — render with that template's **published** version (instead of the pin / default).
- `replaceBinding` — when the CUFE already has a pin and `templateId` is set, overwrite the pin (`true`) or leave it (`false`, default).

Validation:
- `replaceBinding: true` **requires** a non-empty `templateId`. Otherwise the API returns **400** `validation_error` (it does **not** silently reuse the pin).

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
  "pdfBase64": "JVBERi0x…",
  "reusedPinnedTemplate": false,
  "bindingReplaced": false
}
```

Demo NIT seeded in memory: `900000000`.

### Error responses (relevant)

| HTTP | `code` | When |
|---|---|---|
| `400` | `validation_error` | Missing nit/cufe, or `replaceBinding` without `templateId` |
| `404` | `template_not_found` | No published template (by NIT/type or by `templateId`) |
| `404` | `invoice_not_found` | No UBL for nit + cufe |

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
