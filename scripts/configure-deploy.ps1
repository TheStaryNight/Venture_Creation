param([Parameter(Mandatory=$true)][string]$BackendUrl)
$ErrorActionPreference = 'Stop'
$uri = [uri]$BackendUrl
if ($uri.Scheme -ne 'https' -or $uri.IsLoopback -or $uri.Query -or $uri.UserInfo -or $uri.AbsolutePath -ne '/') { throw 'Gunakan origin HTTPS Railway, tanpa path, password, atau query.' }
$repo = Split-Path $PSScriptRoot -Parent
$config = @{
    framework = 'vite'
    buildCommand = 'npm run build'
    outputDirectory = 'dist'
    rewrites = @(@{ source='/api/:path*'; destination=($uri.GetLeftPart([UriPartial]::Authority) + '/api/:path*') })
    headers = @(@{ source='/api/:path*'; headers=@(@{ key='Cache-Control'; value='private, no-store' }) })
}
[IO.File]::WriteAllText((Join-Path $repo 'frontend/vercel.json'), ($config | ConvertTo-Json -Depth 10), (New-Object Text.UTF8Encoding($false)))
Write-Output 'frontend/vercel.json siap. Daftarkan callback OAuth pada domain frontend: https://DOMAIN_FRONTEND/api/google/callback.'
