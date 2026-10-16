# Verified delivery record

## Repository and initial state

Exact existing repository: https://github.com/rifatur17/StockPilot-Inventory-Sales-Management.git. Git Credential Manager supplied an existing credential; GitHub API verified authenticated login **rifatur17**, exact repository, default **main**, and write permission. GitHub attribution of the foundation commit was independently rifatur17. No new/forked/replacement repository was created. A checkout was cloned only after finding none in the workspace/Documents directories.

Initial tree was clean with only README.md, one original commit `45f5e2d2d68c3e29e931f8e8eb4d4d1e2e3cb9a6`, and no application/schema/tests/build. The original commit remains an ancestor. Every new commit uses repository-local **Rifatur Rahman <rifaturrahman1131@gmail.com>**; global Git identity was not changed. No force-push, reset, rebase, amend, branch deletion or history rewriting was used. Other projects and their databases were not modified.

## Implemented and exercised

Layered Core/Infrastructure/API/Web solution; real SQL mappings and two migrations; Identity/role/owner/CSRF/lockout boundaries; catalog/lookups/partners and archival; audited inventory adjustments and movement history; costed purchase drafts/full receiving; server-priced POS/sale confirmation and historic invoices; payments/partial monetary refunds/full physical returns; persisted dashboard/reports; Admin user access and immutable-after-use ledger currency settings. See API_DOCUMENTATION.md, BUSINESS_RULES.md, PERMISSIONS.md and FINANCIAL_CALCULATIONS.md for actual contracts and limits.

SQL mutations use SERIALIZABLE transactions and a database-scoped transaction-owned Exclusive application lock before fresh account/role/balance reads. Rowversion, uniqueness, nonnegative/arithmetic constraints and durable actor/key/payload idempotency add defenses. All balance/cost/movement/status/settlement/audit/idempotency changes commit together. Business errors roll back; safe whole-command retries retain the original key.

## Executed verification

Verified code commit: `5ccdf6933ce01643686b3608843133f1051eb221`.

- [CI 38876400119](https://github.com/rifatur17/StockPilot-Inventory-Sales-Management/actions/runs/38876400119): **success**. Locked restore, Release build (0 warnings/errors), formatting, offline migration-model consistency, unit/workflow tests, JavaScript syntax and native SQL/Chromium job all passed.
- [Security 38876400163](https://github.com/rifatur17/StockPilot-Inventory-Sales-Management/actions/runs/38876400163): **success**, all-severity transitive NuGet auditing and full-history checksum-verified Gitleaks, with no suppression.
- Unit tests: **19 passed, 0 failed, 0 skipped** locally and in CI.
- Latest local Release integration run: **26 passed, 0 failed, 4 skipped**. Skips: three SQL concurrency facts (no local SQL Server) and Chromium opt-in (not installed locally).
- Native SQL Server/HTTP/Chromium suite: downloaded TRX confirms **30 executed, 30 passed, 0 failed, 0 skipped**. Includes limited-stock competing sales, duplicate receiving/confirmation/payment, duplicate return/competing refunds, transaction rollback, cost snapshots, ownership, CSRF, rendered edit actions/submitted values, account lockout/session revocation, settings freeze, invalid date boundaries, gzip static assets and the real purchase/POS/payment/mobile journey.
- `node --check` for app.js/cart.js passed. `dotnet format whitespace --verify-no-changes --no-restore` passed.
- `dotnet ef migrations has-pending-model-changes ... -- --design-only` reported no model changes; idempotent migration SQL was generated offline and reviewed. Neither command connected to a local SQL instance.
- Release `dotnet publish src/StockPilot.Web -c Release --no-build` succeeded. Publish output includes local CSS/JS plus gzip/Brotli assets. It was not deployed.
- NuGet `--vulnerable --include-transitive` reported no vulnerable packages across all six projects. Local staged/source/history Gitleaks checks and GitHub Security are used before delivery. No credentials/database/runtime/build artifacts are committed.
- Local Markdown links and UTF-8 source text were checked. Screenshots in docs/screenshots are genuine CI captures from `f213059` / run 38875954372, using persisted isolated test data and XTS currency; there is no fabricated production data or screenshot.

Reproducible commands and isolated-database safeguards are in TESTING.md; setup is in README.md. Both migrations were applied only to newly created uniquely named `_test` databases on disposable CI SQL Server instances. Test code does not reset/drop an existing database. The job removes only its own disposable container.

## Failures found and corrected

The first SQL CI run rejected inconsistent Identity PK/FK lengths before business assertions ran. A follow-up commit corrected the unapplied initial migration/model widths and added a metadata regression; later native SQL runs passed. Browser testing then found unresolved compressed test-host assets and real mobile invoice-header overflow; dedicated gzip tests, explicit Testing asset-manifest activation and a responsive header corrected them without weakening assertions. Review also corrected zero-value/draft settlement states and invalid date-filter overflow with regression tests. Earlier failed workflow runs remain visible in GitHub history.

One local Debug integration launch was blocked by Windows Code Integrity (0x800711C7), confirmed by read-only logs. No policy was changed or bypassed; subsequent legitimate Release builds/tests ran successfully. Verification branches allowed CI validation before fast-forward integration into main.

## Environment and boundaries

Windows 11 with an existing portable .NET SDK 10.0.402 / runtimes 10.0.13, Git, Node and Python. SQL Server, sqlcmd and Docker were unavailable locally. No SQL Server installation, Windows service, global/user PATH, registry, firewall or system-wide setting was changed. Runtime environment changes were process-scoped. Local SQL application startup/migration is therefore pending an authorized SQL setup. No production database was used.

CI evidence does not establish human usability/accessibility, every browser, production load/penetration, disaster recovery or arbitrary existing-schema upgrades. No manual UI testing or production deployment is claimed. Partial receipts/physical returns, supplier returns, multi-warehouse, uploads, real gateways/email delivery, currency conversion and fiscal sequential numbering are documented future scope. Gross profit remains an estimate under the documented tax/costing rules. Configure TLS, protected shared Data Protection keys, least-privilege SQL credentials, backups and operational controls before production use.

## Commit record

All following new commits have actual author **Rifatur Rahman <rifaturrahman1131@gmail.com>**, verified from Git. The documentation-only delivery commit follows this code record; its hash is available in Git history and the delivery response. No authored commit was replaced.

| Actual commit | Focus |
|---|---|
| [69a08e51bf732b1d0a0d8e147396405000047387](https://github.com/rifatur17/StockPilot-Inventory-Sales-Management/commit/69a08e51bf732b1d0a0d8e147396405000047387) | feat: establish domain rules and SQL Server persistence foundation |
| [4c63704a8d323c69c698bde9673d51d808025d17](https://github.com/rifatur17/StockPilot-Inventory-Sales-Management/commit/4c63704a8d323c69c698bde9673d51d808025d17) | feat: add Identity access controls and secure staff administration |
| [5feff9cbde8cbdb3c567715e5a93293593fb7344](https://github.com/rifatur17/StockPilot-Inventory-Sales-Management/commit/5feff9cbde8cbdb3c567715e5a93293593fb7344) | feat: add validated catalog and partner management workflows |
| [836f7a8ce6cfe51b8dd90ff5c54306622d52afc3](https://github.com/rifatur17/StockPilot-Inventory-Sales-Management/commit/836f7a8ce6cfe51b8dd90ff5c54306622d52afc3) | feat: add idempotent inventory adjustments and immutable stock audit |
| [46662c1bf96bf01f7beb6f29efd539e737d0d378](https://github.com/rifatur17/StockPilot-Inventory-Sales-Management/commit/46662c1bf96bf01f7beb6f29efd539e737d0d378) | feat: implement purchase drafts and atomic weighted-cost receiving |
| [813bdf16d6f3f1cc301bd50f395dea9dd6d1846d](https://github.com/rifatur17/StockPilot-Inventory-Sales-Management/commit/813bdf16d6f3f1cc301bd50f395dea9dd6d1846d) | feat: add stock-safe sales invoices payments and traceable returns |
| [ee15b8f03fc092687e64c009b9d3c210b8355bc8](https://github.com/rifatur17/StockPilot-Inventory-Sales-Management/commit/ee15b8f03fc092687e64c009b9d3c210b8355bc8) | feat: add persisted sales dashboard and financial cohort reports |
| [ccc89f9dc2c35d512193beddf5fb751e0fe90e30](https://github.com/rifatur17/StockPilot-Inventory-Sales-Management/commit/ccc89f9dc2c35d512193beddf5fb751e0fe90e30) | test: verify HTTP journeys and SQL Server ledger concurrency in CI |
| [3196519c93848ce07296b57b80c7ef42d460f00f](https://github.com/rifatur17/StockPilot-Inventory-Sales-Management/commit/3196519c93848ce07296b57b80c7ef42d460f00f) | fix: align Identity foreign key lengths for SQL Server migrations |
| [9e8e42ea786935702fb713e54b70010aa47fde57](https://github.com/rifatur17/StockPilot-Inventory-Sales-Management/commit/9e8e42ea786935702fb713e54b70010aa47fde57) | feat: configure business currency and freeze it after ledger activity |
| [1ddf9531c7da284b98cb083b89e88d7d54dbcca2](https://github.com/rifatur17/StockPilot-Inventory-Sales-Management/commit/1ddf9531c7da284b98cb083b89e88d7d54dbcca2) | test: exercise purchase POS and mobile invoices in CI Chromium |
| [0bb4537e1d995ed8c175a355ed05a778086d8e19](https://github.com/rifatur17/StockPilot-Inventory-Sales-Management/commit/0bb4537e1d995ed8c175a355ed05a778086d8e19) | fix: resolve compressed static assets in the isolated test host |
| [7ae0e0d9fbcc5b3ba05343624d916afde989c035](https://github.com/rifatur17/StockPilot-Inventory-Sales-Management/commit/7ae0e0d9fbcc5b3ba05343624d916afde989c035) | fix: derive correct settlement states for drafts and zero-value invoices |
| [fb42d39b0e35e2e195bd3ac41e77f0ed73bb75a8](https://github.com/rifatur17/StockPilot-Inventory-Sales-Management/commit/fb42d39b0e35e2e195bd3ac41e77f0ed73bb75a8) | fix: keep long invoice headers within narrow mobile viewports |
| [82408caeb937a26b455c78102c4cace3dacacb31](https://github.com/rifatur17/StockPilot-Inventory-Sales-Management/commit/82408caeb937a26b455c78102c4cace3dacacb31) | style: format C# services controllers and regression tests consistently |
| [3dbe81e46dc378c1eb8a915a5c18d64ba778d1bc](https://github.com/rifatur17/StockPilot-Inventory-Sales-Management/commit/3dbe81e46dc378c1eb8a915a5c18d64ba778d1bc) | fix: display a visible chart marker for a single sales day |
| [f213059c4496c4d740260585186a8f39427e4c08](https://github.com/rifatur17/StockPilot-Inventory-Sales-Management/commit/f213059c4496c4d740260585186a8f39427e4c08) | ci: enforce formatting and migration model consistency |
| [5ccdf6933ce01643686b3608843133f1051eb221](https://github.com/rifatur17/StockPilot-Inventory-Sales-Management/commit/5ccdf6933ce01643686b3608843133f1051eb221) | fix: validate UTC date filters before computing query bounds |
