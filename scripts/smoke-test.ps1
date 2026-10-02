$ErrorActionPreference = 'Stop'
$baseUrl = 'http://127.0.0.1:5080/api'
$connections = Invoke-RestMethod "$baseUrl/connections"
if ($connections.ai) { throw 'Run this deterministic smoke test with OpenAI credentials unset.' }
function Post-Json($path, $payload) {
    Invoke-RestMethod -Uri "$baseUrl$path" -Method Post -ContentType 'application/json' -Body ($payload | ConvertTo-Json -Compress)
}
$workflow = Post-Json '/workflows/plan' @{ prompt = 'Siapkan proyek kelompok'; mode = 'demo' }
if ($workflow.source -ne 'template' -or $workflow.steps.Count -ne 3 -or $workflow.status -ne 'planned') { throw 'Invalid demo plan.' }
$null = Post-Json "/workflows/$($workflow.id)/execute" @{}
$conflict = $false
try { $null = Post-Json "/workflows/$($workflow.id)/execute" @{} } catch {
    if ([int]$_.Exception.Response.StatusCode -eq 409) { $conflict = $true } else { throw }
}
if (-not $conflict) { throw 'Duplicate execution was not rejected.' }
$deadline = (Get-Date).AddSeconds(15)
do {
    Start-Sleep -Milliseconds 400
    $result = Invoke-RestMethod "$baseUrl/workflows/$($workflow.id)"
} while ($result.status -in @('queued', 'running') -and (Get-Date) -lt $deadline)
if ($result.status -ne 'completed' -or @($result.steps | Where-Object status -ne 'completed').Count -gt 0) { throw 'Demo did not complete.' }
$liveRejected = $false
try { $null = Post-Json '/workflows/plan' @{ prompt = 'Send a message'; mode = 'live' } } catch {
    if ([int]$_.Exception.Response.StatusCode -eq 400) { $liveRejected = $true } else { throw }
}
if (-not $liveRejected) { throw 'Unconfigured live execution was not rejected.' }
$invalidRejected = $false
try { $null = Post-Json '/workflows/plan' @{ prompt = ''; mode = 'demo' } } catch {
    if ([int]$_.Exception.Response.StatusCode -eq 400) { $invalidRejected = $true } else { throw }
}
if (-not $invalidRejected) { throw 'Empty instruction was not rejected.' }
Write-Output 'PASS: planning, demo completion, duplicate protection, live configuration guard, input validation.'
