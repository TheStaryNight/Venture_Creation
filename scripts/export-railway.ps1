$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$config = Get-Content (Join-Path $repo 'backend/AgentHub.Api/appsettings.Local.json') -Raw | ConvertFrom-Json
$connection = New-Object System.Data.Common.DbConnectionStringBuilder
$connection.set_ConnectionString($config.ConnectionStrings.Orbit)
$connection.set_Item('Root Certificate', '/app/out/Certificates/supabase-ca.crt')
$variables = [ordered]@{
    PORT = '8080'
    Deployment__Public = 'true'
    Deployment__FrontendUrl = 'https://orbitworkspaces.vercel.app'
    ConnectionStrings__Orbit = $connection.get_ConnectionString()
    Security__EncryptionKey = $config.Security.EncryptionKey
    AI__Provider = 'Gemini'
}
$output = Join-Path $repo '.local'
New-Item -ItemType Directory -Path $output -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $output 'railway-variables.json'), ($variables | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))
Write-Output 'Saved .local/railway-variables.json. Contains secrets: paste only into Railway Variables, never chat or Git.'
Write-Output 'Also configure Google__ClientId, Google__ClientSecret, Gemini__ApiKey and Gemini__Model in Railway.'
