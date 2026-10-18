param([string]$HostName, [string]$DatabaseUser, [int]$Port = 5432)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$path = Join-Path $repo 'backend/AgentHub.Api/appsettings.Local.json'
if (-not $HostName) { $HostName = Read-Host 'Supabase Session Pooler host (tanpa https)' }
if (-not $DatabaseUser) { $DatabaseUser = Read-Host 'Database user (postgres.PROJECT_REF)' }
if ($HostName -notmatch '^[a-zA-Z0-9.-]+$' -or $DatabaseUser -notmatch '^[a-zA-Z0-9_.-]+$') { throw 'Host/user tidak valid.' }
$securePassword = Read-Host 'Password database (tidak akan ditampilkan)' -AsSecureString
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
try {
    $password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    $builder = New-Object System.Data.Common.DbConnectionStringBuilder
    $builder['Host'] = $HostName; $builder['Port'] = $Port; $builder['Database'] = 'postgres'
    $builder['Username'] = $DatabaseUser; $builder['Password'] = $password; $builder['SSL Mode'] = 'VerifyFull'
    $config = if (Test-Path -LiteralPath $path) { Get-Content -LiteralPath $path -Raw | ConvertFrom-Json } else { [pscustomobject]@{} }
    if ($config.ConnectionStrings.Orbit) {
        $previous = New-Object System.Data.Common.DbConnectionStringBuilder
        $previous.set_ConnectionString($config.ConnectionStrings.Orbit)
        if ($previous.ContainsKey('Root Certificate')) { $builder.set_Item('Root Certificate', $previous.get_Item('Root Certificate')) }
    }
    $config | Add-Member -NotePropertyName ConnectionStrings -NotePropertyValue @{ Orbit = $builder.ConnectionString } -Force
    if (-not $config.Security.EncryptionKey) {
        $bytes = New-Object byte[] 32
        $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
        try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
        $config | Add-Member -NotePropertyName Security -NotePropertyValue @{ EncryptionKey = [Convert]::ToBase64String($bytes) } -Force
    }
    [IO.File]::WriteAllText($path, ($config | ConvertTo-Json -Depth 10), (New-Object Text.UTF8Encoding($false)))
    Write-Output 'Konfigurasi tersimpan di backend/AgentHub.Api/appsettings.Local.json (diabaikan Git). Jangan bagikan file ini.'
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    $password = $null; $securePassword.Dispose()
}
