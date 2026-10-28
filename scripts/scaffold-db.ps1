# Database-first scaffolding of UnilyDbContext + Models from the Unily database (db/Unily.sql).
# Prerequisites (once):
#   dotnet tool install --global dotnet-ef --version 8.0.10
#   dotnet user-secrets set "ConnectionStrings:Unily" "<your connection string>" --project src/dotnet.challenge.api
# The connection string is read by name from configuration, so it never appears in code or in this script.
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

dotnet ef dbcontext scaffold "Name=ConnectionStrings:Unily" Microsoft.EntityFrameworkCore.SqlServer `
    --project src/dotnet.challenge.data.sqlserver `
    --startup-project src/dotnet.challenge.api `
    --context UnilyDbContext `
    --context-dir . `
    --output-dir Models `
    --namespace dotnet.challenge.data.sqlserver.Models `
    --context-namespace dotnet.challenge.data.sqlserver `
    --table dbo.Products --table dbo.Reservations `
    --no-onconfiguring `
    --force
