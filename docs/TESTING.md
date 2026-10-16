# Automated verification

Prerequisites: pinned .NET SDK, Node for JavaScript syntax, and optional SQL Server/Chromium. No test uses a production database. Do not substitute an existing application's connection string for the test configuration.

```sh
dotnet restore StockPilot.sln --locked-mode
dotnet build StockPilot.sln -c Release --no-restore
dotnet test tests/StockPilot.UnitTests -c Release --no-build
dotnet test tests/StockPilot.IntegrationTests -c Release --no-build
node --check src/StockPilot.Web/wwwroot/assets/app.js
node --check src/StockPilot.Web/wwwroot/assets/cart.js
dotnet list StockPilot.sln package --vulnerable --include-transitive
```

Without `STOCKPILOT_TEST_SQLSERVER`, the integration fixture creates a unique file in the OS temporary directory named `stockpilot_<guid>_test.db`. SQLite relational checks exercise workflows, ownership, antiforgery, form actions, input preservation, receipts, confirmation, payments/refunds, audit, reports, currency freezing and session revocation. It does not prove SQL concurrency. SQL-only facts have an explicit skip reason, not a silent pass. Temporary test databases are not production data and are not automatically reset or deleted; their location/name is generated once per fixture. Dispose closes the test host. Operators may retain them for diagnosis.

## Native SQL Server

Use a dedicated disposable server with a principal authorized to create databases. Set `STOCKPILOT_TEST_SQLSERVER` privately to its connection string with `Database=master`. The fixture rejects any other initial catalog, generates `stockpilot_<random-guid>_test`, explicitly checks DB_ID is absent, creates that new empty database, applies only project migrations and uses it exclusively for test records. It never deletes/recreates an existing database or drops its new database. Use disposable server/container lifecycle cleanup, or retain it for diagnosis; manual database deletion requires separate operator approval. Never supply production server credentials even if InitialCatalog=master.

```powershell
# Set STOCKPILOT_TEST_SQLSERVER privately for your isolated test server first.
dotnet test tests/StockPilot.IntegrationTests -c Release
```

Native tests use independent EF scopes/connections to compete for limited stock, retry the same receipt/confirmation/payment, race full returns and competing monetary refunds. They assert one successful limited-stock sale, no negative/double deduction/restoration and no duplicate/excess refund. A metadata regression also checks all SQL string FK widths match their principal key widths.

## Browser journey

CI installs matching Chromium only on its disposable Linux runner. To opt in locally, approve/install the Playwright browser and platform prerequisites first; no browser software was installed on the Windows PC by this task.

```powershell
# After Release build:
pwsh tests/StockPilot.IntegrationTests/bin/Release/net10.0/playwright.ps1 install chromium
$env:STOCKPILOT_BROWSER = '1'
$env:STOCKPILOT_SCREENSHOT_DIR = Join-Path ([IO.Path]::GetTempPath()) 'stockpilot-screenshots'
dotnet test tests/StockPilot.IntegrationTests -c Release --no-build
```

Browser test uses real Kestrel on an ephemeral loopback port and real Identity cookies. It drives login, product/partner search, purchase cart/receipt, POS/cart/confirmation, payment and invoice, checks stock/payment state in persistence and mobile horizontal overflow, captures desktop/mobile screenshots and rejects JavaScript page errors. SQL CI runs the browser against the newly created test database. It is automated Chromium evidence, not human usability or broad cross-browser/a11y certification.

## GitHub workflows

CI: locked restore, Release build, formatting and offline migration-model consistency checks, unit/local workflow tests, JavaScript syntax, plus disposable SQL Server migration/workflow/concurrency/Chromium suite. SQL logs/TRX/screenshots are uploaded as `sql-server-results`; only the named job container is removed in cleanup. Security: all-severity transitive NuGet audit enforced at restore and full Git history Gitleaks (fetch-depth 0, `--all`, no invalid root-parent range). Gitleaks release SHA-256 is verified. Failures remain visible; no forced green statuses or advisory suppression.

Check the exact commit's run logs for pass/fail/skip counts. A new schema passing does not establish arbitrary existing production-schema upgrades, and these tests are not load, penetration, disaster-recovery or every-browser testing. See VERIFICATION.md for actual observed results and remaining boundaries.
