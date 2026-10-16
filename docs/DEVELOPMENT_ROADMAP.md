# Delivery roadmap
Initial assessment: existing repository contained only README.md at 45f5e2d. No application, tests or migrations existed; clean main. Existing history is retained.

1. Architecture, domain invariants, SQL mappings and initial migration.
2. Identity, secure cookie/API boundaries, administrator bootstrap, permissions.
3. Catalog and partner management with query pagination and archival.
4. Serialized inventory adjustments, audit and persistent idempotency.
5. Purchase drafts and atomic receiving with moving average costs.
6. POS, historical invoices, stock-safe confirmation, payments and reversals.
7. Persisted reports and responsive operational UI.
8. SQL Server concurrency, HTTP/browser verification, security and delivery docs.

Local SQL Server/Docker are unavailable. Local unit/SQLite tests do not establish SQL Server locking correctness. Native SQL tests must run in isolated CI or an explicitly configured test server. No database is automatically migrated on normal startup.
