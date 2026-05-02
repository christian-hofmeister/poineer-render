Push-Location "$PSScriptRoot/../src/POIneer.Render"

try {
    Write-Host "Starting POIneer.Render (Production)..."

    $env:DOTNET_ENVIRONMENT = "Production"

    dotnet build -c Release
    dotnet run -c Release --no-build

    if ($LASTEXITCODE -ne 0) {
        throw "Run failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}