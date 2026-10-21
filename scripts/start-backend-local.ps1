param([switch]$LiveIntegrations)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$backend = Join-Path $repositoryRoot 'backend/AgentHub.Api'

# Use local workspace storage, avoiding the production database and worker lease.
$overrides = @{
    Local__WorkflowDirectory = (Join-Path $repositoryRoot '.local/workflows')
    ConnectionStrings__Orbit = ' '
    Deployment__Public = 'false'
    Deployment__FrontendUrl = 'http://127.0.0.1:5173'
    PORT = $null
    ASPNETCORE_ENVIRONMENT = 'Development'
}
if (-not $LiveIntegrations) {
    $overrides.AI__Provider = 'OpenAI'
    $overrides.OpenAI__ApiKey = ' '
    $overrides.Gemini__ApiKey = ' '
    $overrides.Google__ClientId = ' '
    $overrides.Google__ClientSecret = ' '
    $overrides.Discord__WebhookUrl = ' '
}
$previousValues = @{}
try {
    foreach ($name in $overrides.Keys) {
        $previousValues[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, $overrides[$name], 'Process')
    }
    Write-Host 'Backend lokal: http://127.0.0.1:5080. Database production tidak digunakan.'
    if (-not $LiveIntegrations) { Write-Host 'Mode demo: tanpa panggilan AI, Google, atau Discord. Rencana memakai template contoh.' }
    Push-Location $backend
    try {
        dotnet build --property:UseSharedCompilation=false --property:UseAppHost=false
        if ($LASTEXITCODE -ne 0) { throw "Backend build failed." }
        dotnet .\bin\Debug\net8.0\AgentHub.Api.dll --urls http://127.0.0.1:5080
        if ($LASTEXITCODE -ne 0) { throw "Backend exited with code $LASTEXITCODE." }
    } finally { Pop-Location }
} finally {
    foreach ($name in $previousValues.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previousValues[$name], 'Process')
    }
}
