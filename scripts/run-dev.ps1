Push-Location "$PSScriptRoot/../src/POIneer.Render"

try {
    Write-Host "Starting POIneer.Render (Development)..."

    $env:DOTNET_ENVIRONMENT = "Development"

    dotnet build
    dotnet run --no-build

    if ($LASTEXITCODE -ne 0) {
        throw "Run failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}