# StockPilot — Inventory & Sales Management

ASP.NET Core 10 MVC + versioned API, EF Core SQL Server and Identity. Existing repository history is preserved. Implementation and verification are incremental; see [roadmap](docs/DEVELOPMENT_ROADMAP.md).

Projects: Core (domain/contracts), Infrastructure (SQL/Identity/services), Api (REST controllers), Web (MVC host and Razor), and xUnit unit/integration tests. MVC and API share one cookie boundary and underlying services.

Requires .NET SDK 10.0.402 and SQL Server 2022 or newer. Restore with `dotnet restore StockPilot.sln`; build with `dotnet build StockPilot.sln`; test with `dotnet test StockPilot.sln`. Runtime connection strings must be supplied through environment variables/User Secrets; never commit credentials. SQL execution is not yet verified at this milestone.
