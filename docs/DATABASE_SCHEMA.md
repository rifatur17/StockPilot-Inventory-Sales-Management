# Database schema

Two additive migrations create the schema: `InitialInventory` and `ConfigureBusinessCurrency`. The initial Identity key mapping was corrected in a follow-up commit after SQL CI rejected inconsistent foreign-key widths; no existing published commit was rewritten and no existing database was reset. The fresh-schema SQL migration path is exercised in CI.

| Tables | Key relationships and invariants |
|---|---|
| AspNetUsers/Roles/UserRoles/Claims/Logins/Tokens | Standard Identity, string IDs/FKs 128; role/provider composite key lengths bounded; active/display-name fields |
| Categories / Units | Unique names; referenced product deletion restricted |
| Products | Unique normalized uppercase SKU, category/unit FKs, decimal prices/stock/cost, Active, SQL rowversion; nonnegative SQL check |
| Partners | Supplier or Customer discriminator, contacts/notes/status; one shared shape avoids duplicated schemas |
| Documents | Purchase or Sale, Draft/Completed/Cancelled/Reversed, unique reference number, optional partner FK, actor FK, UTC dates, totals and tax check |
| DocumentItems | Document/product FKs, unique product per document, immutable historical name/SKU/category/price/cost/quantity snapshots after processing |
| Payments | Document FK, positive amount, refund flag/method/reason/actor/time; append-only settlement records |
| Movements | Product and optional source document FK; before/change/after arithmetic and nonnegative SQL checks; append-only |
| Activities | Actor/action/entity ID/time/safe summary; restricted reads, no update/delete routes |
| Operations | Unique actor+UUID key, command SHA-256 fingerprint, durable result ID; committed with the business action |
| Settings | Constrained singleton ID, business name and configured currency; code freezes currency once any document exists |

Decimals use SQL `decimal(19,6)` storage to support 3-decimal quantities and 6-decimal moving costs. Money inputs and document values are validated to 2 decimal places. Money and per-line quantity limits are enforced in services; storage checks additionally enforce nonnegative and arithmetic constraints. UTC `DateTime` values map to `datetime2`; reports explicitly interpret date input as UTC. Preserve the UTC convention for any future import.

Indexes cover SKU, category/status, partner kind/name, document kind/status/date, partner history, invoice number, product movement date, audit date and operation uniqueness. Restrict delete behavior preserves business and identity attribution. Products/partners are archived; there are no transaction delete endpoints.

Offline migration inspection (never connects to SQL):

```sh
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project src/StockPilot.Infrastructure --startup-project src/StockPilot.Web -- --design-only
dotnet ef migrations script --idempotent --project src/StockPilot.Infrastructure --startup-project src/StockPilot.Web --output artifacts/schema.sql -- --design-only
```

`artifacts/` is ignored. For applying migrations, set the explicit authorized `ConnectionStrings__StockPilot` and omit `--design-only`. Review generated SQL, target and permissions first; never reset/drop a database to solve migration problems. Back up existing deployments and test their upgrade path separately; CI establishes new-schema creation, not arbitrary legacy database upgrades.
