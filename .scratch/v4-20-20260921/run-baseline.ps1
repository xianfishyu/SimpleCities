param([int[]]$Cells = @(25,50,100,200), [switch]$Smoke)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$outputDirectory = if ($Smoke) { Join-Path $PSScriptRoot 'smoke' } else { $PSScriptRoot }
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$env:V4_QA_OUTPUT = if ($Smoke) { 'res://.scratch/v4-20-20260921/smoke' } else { 'res://.scratch/v4-20-20260921' }
$env:V4_PERF_SMOKE = if ($Smoke) { '1' } else { '0' }
$runs = @()
foreach ($cell in $Cells) {
    if ($cell -notin @(25,50,100,200)) { throw 'Invalid cell size' }
    $env:V4_PERF_CELL = [string]$cell
    $started = [DateTime]::UtcNow
    $process = Start-Process -FilePath 'D:/Program Files/Godot/godot_console.exe' -WorkingDirectory $repo -ArgumentList @(
        '--path', $repo, '--rendering-method', 'forward_plus', '--rendering-driver', 'vulkan',
        '--audio-driver', 'Dummy', '--resolution', '1600x900', '--script', 'res://tests/godot/v4_performance_baseline.gd'
    ) -WindowStyle Hidden -RedirectStandardOutput (Join-Path $outputDirectory "render-$cell.stdout.log") -RedirectStandardError (Join-Path $outputDirectory "render-$cell.stderr.log") -PassThru
    while (-not $process.WaitForExit(1000)) {
        if (([DateTime]::UtcNow - $started).TotalSeconds -gt 240) {
            $process.Kill()
            throw "Timed out in cell $cell; stopped test PID $($process.Id)"
        }
    }
    $resultPath = Join-Path $outputDirectory "render-$cell.json"
    $result = if (Test-Path -LiteralPath $resultPath) { Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json } else { $null }
    $record = [pscustomobject]@{ cell=$cell; pid=$process.Id; startUtc=$started.ToString('o'); endUtc=[DateTime]::UtcNow.ToString('o'); exitCode=$process.ExitCode; passed=($null -ne $result -and $result.passed); smoke=[bool]$Smoke }
    $runs += $record
    $runs | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $outputDirectory 'runs.json') -Encoding utf8
    $record | ConvertTo-Json -Compress
    if ($process.ExitCode -ne 0 -or -not $record.passed) { throw "Baseline integrity failed in cell $cell; see saved logs" }
}
