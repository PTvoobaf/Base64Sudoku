#Requires -Version 7
<#
.SYNOPSIS
    M4 timing baseline — 10-seed generation and validation.
.DESCRIPTION
    Builds DLX64S and B64Sud in Release, generates one grid per seed,
    validates each with the B64Sud solver, and prints a summary table
    with median, p95, and max elapsed times.

    Run from the repo root:
        .\DLX64S.Tests\m4-timing.ps1

    dotnet test ignores this file (not a .cs or .csx source).
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
$dlxProj  = Join-Path $repoRoot 'DLX64S\DLX64S.csproj'
$b64Proj  = Join-Path $repoRoot 'B64Sud\B64Sud.csproj'

# 10 seeds: nerdy, mathematical, and one edge case.
$seeds = [ordered]@{
    '42'                  = 'the answer'
    '1337'                = 'leet'
    '0xc0ffee'            = 'coffee'
    '0xdeadbeef8badf00d'  = 'dead beef / bad food'
    '0xbadc0ffee0ddf00d'  = 'bad coffee / odd food'
    '0xfeedface'          = 'Mach-O magic'
    '314159265358979323'  = 'pi digits'
    '2718281828459045235' = 'e digits'
    '1000000007'          = 'beloved CP prime'
    '0'                   = 'zero'
}

Write-Host 'Building Release binaries...' -ForegroundColor Cyan
dotnet build $dlxProj -c Release -v q --nologo 2>&1 | Out-Null
dotnet build $b64Proj -c Release -v q --nologo 2>&1 | Out-Null
Write-Host "Ready.`n" -ForegroundColor Cyan

$rows = [System.Collections.Generic.List[pscustomobject]]::new()
$i    = 0

foreach ($kv in $seeds.GetEnumerator()) {
    $i++
    $seed  = $kv.Key
    $label = $kv.Value
    $grid  = Join-Path $env:TEMP "dlx64s_m4_$($seed -replace '[^a-zA-Z0-9]','_').txt"

    Write-Host ('[{0,2}/10] {1,-26} ({2}) ... ' -f $i, $seed, $label) -NoNewline

    # Generate
    $sw     = [Diagnostics.Stopwatch]::StartNew()
    $output = dotnet run --project $dlxProj -c Release --no-build -- `
                  --seed $seed --out $grid 2>&1
    $sw.Stop()
    $sec      = [math]::Round($sw.Elapsed.TotalSeconds, 1)
    $outputStr = ($output -join "`n")
    $attempts = if ($outputStr -match 'Done:.+,\s*(\d+)\s*attempt') { [int]$Matches[1] } else { -1 }

    # Validate
    $verdict = dotnet run --project $b64Proj -c Release --no-build -- --solve $grid 2>&1
    $ok      = $null -ne ($verdict | Select-String 'Solved')

    $tag   = if ($ok) { 'OK  ' } else { 'FAIL' }
    $color = if ($ok) { 'Green' } else { 'Red' }
    Write-Host ('{0}  {1,5:F1} s  {2} attempt(s)' -f $tag, $sec, $attempts) -ForegroundColor $color

    $rows.Add([pscustomobject]@{
        Seed     = $seed
        Label    = $label
        Sec      = $sec
        Attempts = $attempts
        Valid    = if ($ok) { 'OK' } else { 'FAIL' }
    })
}

# Summary table
Write-Host "`n=== M4 Timing Summary ===" -ForegroundColor Cyan
$rows | Format-Table Seed, Label, Sec, Attempts, Valid -AutoSize

$sorted = @($rows | Sort-Object Sec | Select-Object -ExpandProperty Sec)
$n      = $sorted.Count
$median = if ($n % 2 -eq 1) {
              $sorted[$n / 2]
          } else {
              ($sorted[$n / 2 - 1] + $sorted[$n / 2]) / 2.0
          }
$p95    = $sorted[[int][math]::Ceiling($n * 0.95) - 1]
$max    = ($rows | Measure-Object Sec -Maximum).Maximum
$fails  = @($rows | Where-Object { $_.Valid -ne 'OK' }).Count

Write-Host ('Median {0} s  |  p95 {1} s  |  Max {2} s  |  Failures {3}/{4}' -f `
    $median, $p95, $max, $fails, $n) -ForegroundColor Cyan
