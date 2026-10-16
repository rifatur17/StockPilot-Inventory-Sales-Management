# REST API

Same-origin cookie authentication; base `/api/v1`. Development OpenAPI: `/openapi/v1.json`. No CORS integration or bearer-token public API is implied. All unsafe requests, including API login, require the antiforgery cookie plus `X-CSRF-TOKEN`. Fetch a new token after login/logout because the authenticated identity changes. Do not store passwords in browser persistent storage.

```javascript
const csrf = await fetch('/api/v1/session/csrf').then(r => r.json());
// Call login with credentials entered in a secure UI, not hardcoded here.
// Repeat csrf GET after login, then use the fresh token on each mutation.
const result = await fetch('/api/v1/inventory/adjustments', {
  method: 'POST',
  headers: {'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf.token},
  body: JSON.stringify({productId, change: 1, unitCost: 2,
    reason: 'Counted opening stock', key: crypto.randomUUID()})
});
```

The example assumes an authenticated inventory-authorized user and a valid product. Keep a financial command's original UUID and exact payload across retries; generating a new key per retry defeats request replay protection. Core document state protection remains effective independently.

| Method / path | Contract / result |
|---|---|
| GET session/csrf | anonymous, `{token}` plus antiforgery cookie |
| POST session/login | `{email,password}`, generic 401 on failure, 429 IP limit |
| GET session | current active account ID and roles |
| POST session/logout | clears cookie, 204 |
| GET products | search/categoryId/page/size/sort(name,sku,newest)/low/archived |
| GET products/{id} | product DTO; cost null for SalesStaff |
| POST products | ProductInput: sku/name/categoryId/unitId/sellingPrice/minimumStock/description?/barcode? |
| PUT products/{id} | same DTO; no direct stock or cost editing |
| POST products/{id}/archive | requires zero remaining stock |
| GET/POST lookups/{kind} | kind=categories or units; POST `{name}` |
| PUT/DELETE lookups/{kind}/{id} | PUT `{name}`; referenced lookup cannot be deleted |
| GET/POST partners/{kind} | kind=Supplier or Customer; search/page/size/archived; POST name/contact?/address?/notes? |
| GET partner/{id} | partner DTO with permission boundary |
| PUT partners/{kind}/{id} | validated partner edit |
| POST partner/{id}/archive | archive preserving history |
| GET purchases / sales | search/partnerId/state/from/to/page; SalesStaff own records only |
| POST purchases / sales | DraftInput below; correct kind required; returns document ID |
| GET purchases/{id} / sales/{id} | snapshot lines, totals, status and settlements |
| POST purchases/{id}/receive | `{key}`; full atomic receipt from Draft only |
| POST sales/{id}/confirm | `{key}`; revalidates prices and stock |
| POST purchases/{id}/cancel / sales/{id}/cancel | `{key}`; Draft only, no stock movement |
| POST sales/{id}/payments | `{amount,method,key,reason?}` |
| POST sales/{id}/refunds | `{amount,method,key,reason}`; Admin/Manager, money only |
| POST sales/{id}/reverse | `{reason,key}`; Admin/Manager, all physical goods returned once, no automatic cash refund |
| GET inventory | search/page/low balance DTO page |
| GET inventory/movements | productId?/page, immutable before/change/after DTOs |
| POST inventory/adjustments | `{productId,change,reason,unitCost,key}` |
| GET inventory/audit | page, Admin/Manager only |
| GET reports | from/to inclusive UTC date range; 1–366 days and <=10000 documents |
| GET users | Admin; up to 200 account DTOs, no password/hash/token |
| POST users | Admin; email/displayName/password/role; server validation |
| PUT users/{id} | Admin; role/active, stamps revoked, last active Admin preserved |
| GET settings | business name/currency for authenticated UI |
| PUT settings | Admin; `{name,currencyCode}`, currency immutable after documents |

Draft payload:

```json
{
  "kind": "Sale",
  "partnerId": null,
  "items": [{"productId": "PRODUCT_UUID", "quantity": 1, "unitCost": 0}],
  "discount": 0,
  "taxRate": 0,
  "reference": "Optional reference",
  "key": "CLIENT_OPERATION_UUID"
}
```

Replace placeholder strings with real UUIDs. For Purchase, supplier is required and line unitCost sets proposed purchase unit price; for Sale, browser unitCost/price is ignored. Product state/price/stock is read from persistence and validated by shared services. Payment methods: Cash, Card, BankTransfer, MobileFinancialService. DocumentState: Draft, Completed, Cancelled, Reversed (UI labels Completed purchase as Received).

Pages return `{items,total,number,size}`; query page/size are clamped to 1–10000 / 1–100. Validation is 400, unauthenticated 401, permission denial 403, absent or concealed other-owner invoice 404, state/stock/retry conflicts 409, throttle 429 and safe unexpected failures 500. ProblemDetails includes safe titles and trace IDs where applicable; never rely on raw SQL diagnostics. MVC forms use the same services, show validation summaries and preserve entered values for supported edit/adjustment forms. A failed financial POST never reports success; use Back and retry the original operation key after resolving the conflict.
