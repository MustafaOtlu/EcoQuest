param([string]$AssetId, [string]$Provider, [string]$EnvPath)
$ErrorActionPreference = 'Stop'
try {
    $assetSecrets = @{}
    Get-Content -LiteralPath $EnvPath | ForEach-Object {
        if ($_ -match '^\s*(LEONARDO_API_KEY|MESHY_API_KEY)\s*=\s*(.+)$') {
            $assetSecrets[$matches[1]] = $matches[2].Trim().Trim('"').Trim("'")
        }
    }
    $assetManifest = Get-Content -Raw -LiteralPath "$PSScriptRoot\manifest.json" | ConvertFrom-Json
    $assetRecord = $assetManifest.assets | Where-Object { $_.id -eq $AssetId }
    if ($Provider -eq 'leonardo') {
        $assetEndpoint = 'https://cloud.leonardo.ai/api/rest/v1/generations/' + $assetRecord.reference.request_id
        $assetToken = $assetSecrets['LEONARDO_API_KEY']
    } else {
        $assetEndpoint = 'https://api.meshy.ai/openapi/v1/image-to-3d/' + $assetRecord.model.request_id
        $assetToken = $assetSecrets['MESHY_API_KEY']
    }
    $assetResponse = Invoke-RestMethod -Uri $assetEndpoint -Headers @{Authorization="Bearer $assetToken";Accept='application/json'} -UserAgent 'Mozilla/5.0' -TimeoutSec 45
    $assetStatusPath = Join-Path $PSScriptRoot ($assetRecord.category + '/' + $AssetId + '/' + $Provider + '-fetched.json')
    $assetResponse | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $assetStatusPath -Encoding UTF8
    if ($Provider -eq 'leonardo') { Write-Output $assetResponse.generations_by_pk.status } else { Write-Output $assetResponse.status }
} catch {
    Write-Output ('Status fetch failed: ' + $_.Exception.GetType().Name)
    exit 1
}
