$ErrorActionPreference = 'Stop'
function Measure-Values($values) {
    $ordered = @($values | Sort-Object)
    if ($ordered.Count -eq 0) { return $null }
    return [ordered]@{count=$ordered.Count;p95=$ordered[[math]::Ceiling($ordered.Count*0.95)-1];p99=$ordered[[math]::Ceiling($ordered.Count*0.99)-1];max=$ordered[-1]}
}
$phases = @('idle','panHover','polylineView','buildUndoFrames','upgradeFrames','deleteUndoFrames','cancellationFrames')
$operations = @('builds','undos','redos','upgrades','deletes')
$fields = @('inputPreparationMs','workerQueueMs','domainMs','presentationPrepareMs','resumeMs','preflightMs','referenceCommitMs','presentationCommitMs','publishToDrawMs','inputToDrawMs')
$rows = @()
$responseRows = @()
$stageRows = @()
$cancellationRows = @()
$fixtures = @()
foreach ($cell in @(25,50,100,200)) {
    $run = Get-Content -LiteralPath (Join-Path $PSScriptRoot "render-$cell.json") -Raw | ConvertFrom-Json
    if (-not $run.passed -or $run.smoke) { throw "Not a complete formal baseline: $cell" }
    $fixtures += $run.fixture
    foreach ($phase in $phases) {
        $sample = $run.$phase
        $rows += [pscustomobject]@{cell=$cell;phase=$phase;seconds=$sample.durationSeconds;count=$sample.summary.count;p95=$sample.summary.p95Ms;p99=$sample.summary.p99Ms;max=$sample.summary.maxMs;overBudget=$sample.summary.over144BudgetCount;ratio=$sample.summary.over144BudgetRatio;visibility=$sample.visibility;focused=$sample.focusedDraws;unfocused=$sample.unfocusedDraws;minimized=$sample.minimizedDraws;p95Pass=($sample.summary.p95Ms -le 1000.0/144)}
    }
    foreach ($operation in $operations) {
        $sample = $run.responseSummary.$operation
        $responseRows += [pscustomobject]@{cell=$cell;operation=$operation;count=$sample.count;p95=$sample.p95Ms;p99=$sample.p99Ms;max=$sample.maxMs;over100=$sample.over100MsCount;over300=$sample.over300MsCount}
        foreach ($field in $fields) {
            $values = @($run.$operation | ForEach-Object { $_.$field })
            $stats = Measure-Values $values
            $stageRows += [pscustomobject]@{cell=$cell;operation=$operation;field=$field;p95=$stats.p95;p99=$stats.p99;max=$stats.max}
        }
    }
    $cancellationRows += [pscustomobject]@{cell=$cell;samples=$run.cancellations}
}
$summary = [ordered]@{frames=$rows;responses=$responseRows;stages=$stageRows;cancellations=$cancellationRows;fixtures=$fixtures}
$summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'summary.json') -Encoding utf8
$frameTable = @('| 格长 / 场景 | 样本数 | P95 ms | P99 ms | 最大 ms | 超6.9444ms帧 | 焦点 |','| --- | ---: | ---: | ---: | ---: | ---: | --- |')
foreach ($row in $rows) {
    $frameTable += '| {0} / {1} | {2} | {3:F3} | {4:F3} | {5:F3} | {6} | {7} |' -f $row.cell,$row.phase,$row.count,$row.p95,$row.p99,$row.max,$row.overBudget,$row.visibility
}
$frameTable += @('','| 格长 / 操作 | 次数 | P95 ms | P99 ms | 最大 ms | >100 / >300 ms |','| --- | ---: | ---: | ---: | ---: | --- |')
foreach ($row in $responseRows) {
    $frameTable += '| {0} / {1} | {2} | {3:F3} | {4:F3} | {5:F3} | {6} / {7} |' -f $row.cell,$row.operation,$row.count,$row.p95,$row.p99,$row.max,$row.over100,$row.over300
}
$frameTable | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'tables.md') -Encoding utf8
[pscustomobject]@{frames=($rows | Measure-Object count -Sum).Sum;overBudget=($rows | Measure-Object overBudget -Sum).Sum;maxFrame=($rows | Measure-Object max -Maximum).Maximum;responses=($responseRows | Measure-Object count -Sum).Sum;maxResponse=($responseRows | Measure-Object max -Maximum).Maximum;over300=($responseRows | Measure-Object over300 -Sum).Sum;p95FrameFailures=@($rows | Where-Object {-not $_.p95Pass}).Count;backgroundPhases=@($rows | Where-Object {$_.visibility -ne 'foreground'}).Count} | ConvertTo-Json
