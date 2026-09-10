<#
.SYNOPSIS
    Turns Unity test results into a run report and README badges.

.DESCRIPTION
    Reads the NUnit XML files game-ci/unity-test-runner writes per test mode, appends a
    Markdown report to the workflow run summary and writes one shields.io endpoint JSON
    per badge. Exits with 1 when a test failed or no result could be read, so a red run
    is never reported as green.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $ResultsDir,
    [Parameter(Mandatory)][string] $BadgesDir,
    [string] $SummaryPath = $env:GITHUB_STEP_SUMMARY,
    [string] $Commit = $env:GITHUB_SHA
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$invariant = [cultureinfo]::InvariantCulture

$modes = @(
    [pscustomobject]@{ Name = 'EditMode'; File = 'editmode-results.xml'; Badge = 'editmode.json' }
    [pscustomobject]@{ Name = 'PlayMode'; File = 'playmode-results.xml'; Badge = 'playmode.json' }
)

$icons = @{ Passed = '✅'; Failed = '❌'; Skipped = '⏭️' }

function ConvertTo-Seconds([string] $value) {
    $seconds = 0.0
    [void][double]::TryParse($value, [Globalization.NumberStyles]::Float, $invariant, [ref] $seconds)
    $seconds
}

function Format-Duration([double] $seconds) {
    if ($seconds -lt 1) { return [string]::Format($invariant, '{0:0} ms', $seconds * 1000) }
    [string]::Format($invariant, '{0:0.0} s', $seconds)
}

function Format-Count([int] $count, [string] $noun) {
    if ($count -eq 1) { "1 $noun" } else { "$count ${noun}s" }
}

function Get-InnerText($node, [string] $xpath) {
    $child = $node.SelectSingleNode($xpath)
    if ($null -eq $child) { return '' }
    # Only strip line breaks: NUnit indents "Expected:" to line up with "But was:".
    $child.InnerText.Trim([char[]] "`r`n")
}

# Skipped, Ignored and Inconclusive all mean "did not pass, did not fail".
function Get-Outcome([string] $result) {
    switch ($result) {
        'Passed' { 'Passed' }
        'Failed' { 'Failed' }
        default { 'Skipped' }
    }
}

function ConvertTo-HtmlText([string] $text) { [Net.WebUtility]::HtmlEncode($text) }

function ConvertTo-TableCell([string] $text) {
    (ConvertTo-HtmlText $text) -replace '\|', '\|' -replace '\r?\n', ' '
}

# The fence has to be longer than any backtick run inside, or a message quoting code breaks out of it.
function Format-CodeBlock([string] $text) {
    $longest = ([regex]::Matches($text, '`+') | ForEach-Object Length | Measure-Object -Maximum).Maximum
    $fence = '`' * [Math]::Max(3, [int] $longest + 1)
    "$fence`n$text`n$fence"
}

function Read-TestRun($mode) {
    $run = [pscustomobject]@{
        Mode = $mode.Name; File = $mode.File; Badge = $mode.Badge
        Status = 'Missing'; Error = ''
        Total = 0; Passed = 0; Failed = 0; Skipped = 0; Duration = 0.0; Cases = @()
    }

    $path = Join-Path $ResultsDir $mode.File
    if (-not (Test-Path -LiteralPath $path)) { return $run }

    try {
        $xml = [xml]::new()
        $xml.Load((Resolve-Path -LiteralPath $path).ProviderPath)
        $testRun = $xml.SelectSingleNode('/test-run')
        if ($null -eq $testRun) { throw 'The file has no <test-run> element.' }

        $run.Cases = @($xml.SelectNodes('//test-case') | ForEach-Object {
                [pscustomobject]@{
                    Fixture    = $_.GetAttribute('classname')
                    Name       = $_.GetAttribute('name')
                    Outcome    = Get-Outcome $_.GetAttribute('result')
                    Duration   = ConvertTo-Seconds $_.GetAttribute('duration')
                    Message    = Get-InnerText $_ 'failure/message'
                    StackTrace = Get-InnerText $_ 'failure/stack-trace'
                }
            })
        $run.Duration = ConvertTo-Seconds $testRun.GetAttribute('duration')
    }
    catch {
        $run.Status = 'Unreadable'
        $run.Error = $_.Exception.Message
        return $run
    }

    $run.Status = 'Read'
    $run.Total = $run.Cases.Count
    $run.Passed = @($run.Cases | Where-Object Outcome -EQ 'Passed').Count
    $run.Failed = @($run.Cases | Where-Object Outcome -EQ 'Failed').Count
    $run.Skipped = @($run.Cases | Where-Object Outcome -EQ 'Skipped').Count
    $run
}

function Write-Badge([string] $file, [string] $label, [string] $message, [string] $color) {
    [ordered]@{ schemaVersion = 1; label = $label; message = $message; color = $color } |
        ConvertTo-Json |
        Set-Content -LiteralPath (Join-Path $BadgesDir $file) -Encoding utf8
}

$runs = @($modes | ForEach-Object { Read-TestRun $_ })
$read = @($runs | Where-Object Status -EQ 'Read')
$unreadable = @($runs | Where-Object Status -EQ 'Unreadable')

function Get-Sum([string] $property) {
    $sum = 0.0
    foreach ($run in $read) { $sum += $run.$property }
    $sum
}

$total = [int] (Get-Sum Total)
$passed = [int] (Get-Sum Passed)
$failed = [int] (Get-Sum Failed)
$skipped = [int] (Get-Sum Skipped)
$duration = Get-Sum Duration
$noResults = $read.Count -eq 0

foreach ($run in $runs) {
    switch ($run.Status) {
        'Missing' { Write-Host "::warning::$($run.Mode): no result file ($($run.File))." }
        'Unreadable' { Write-Host "::error::$($run.Mode): $($run.File) could not be read: $($run.Error)" }
        'Read' { Write-Host "$($run.Mode): $($run.Total) total, $($run.Passed) passed, $($run.Failed) failed, $($run.Skipped) skipped." }
    }
}
if (-not $noResults -and $total -eq 0) { Write-Host '::warning::No tests ran.' }

# --- Run summary ---

$md = [Collections.Generic.List[string]]::new()

$headline =
    if ($failed -gt 0) { "## ❌ Tests: $failed of $total failed" }
    elseif ($unreadable) { '## ❌ Tests: results could not be read' }
    elseif ($noResults) { '## ⚠️ Tests: no results' }
    elseif ($total -eq 0) { '## ⚠️ Tests: no tests ran' }
    elseif ($skipped -gt 0) { "## ✅ Tests: $passed/$total passed, $skipped skipped" }
    else { "## ✅ Tests: $passed/$total passed" }
$md.Add($headline)
$md.Add('')

$meta = @()
if ($Commit) { $meta += "Commit ``$($Commit.Substring(0, [Math]::Min(7, $Commit.Length)))``" }
if (-not $noResults) { $meta += Format-Duration $duration }
if ($meta) { $md.Add($meta -join ' · '); $md.Add('') }

foreach ($run in $unreadable) {
    $md.Add("> ❌ ``$($run.File)`` could not be read: $(ConvertTo-HtmlText $run.Error)")
    $md.Add('')
}

$md.Add('| Mode | Tests | ✅ Passed | ❌ Failed | ⏭️ Skipped | Duration |')
$md.Add('|---|--:|--:|--:|--:|--:|')
foreach ($run in $runs) {
    $md.Add($(switch ($run.Status) {
                'Read' { "| $($run.Mode) | $($run.Total) | $($run.Passed) | $($run.Failed) | $($run.Skipped) | $(Format-Duration $run.Duration) |" }
                'Missing' { "| $($run.Mode) | – | – | – | – | _no result file_ |" }
                'Unreadable' { "| $($run.Mode) | – | – | – | – | _unreadable_ |" }
            }))
}
$md.Add('')

$failures = @($read | ForEach-Object { $mode = $_.Mode; $_.Cases | Where-Object Outcome -EQ 'Failed' | Select-Object *, @{ n = 'Mode'; e = { $mode } } })
if ($failures) {
    $md.Add('### ❌ Failed tests')
    $md.Add('')
    foreach ($case in $failures) {
        $md.Add("<details open><summary><code>$(ConvertTo-HtmlText "$($case.Fixture).$($case.Name)")</code> ($($case.Mode))</summary>")
        $md.Add('')
        if ($case.Message) { $md.Add((Format-CodeBlock $case.Message)); $md.Add('') }
        if ($case.StackTrace) { $md.Add((Format-CodeBlock $case.StackTrace)); $md.Add('') }
        $md.Add('</details>')
        $md.Add('')
    }
}

foreach ($run in $read | Where-Object Total -GT 0) {
    $md.Add("### $($run.Mode) · $(Format-Count $run.Total 'test')")
    $md.Add('')
    foreach ($fixture in $run.Cases | Group-Object Fixture | Sort-Object Name) {
        $cases = @($fixture.Group)
        $fixtureFailed = @($cases | Where-Object Outcome -EQ 'Failed').Count
        $fixtureSkipped = @($cases | Where-Object Outcome -EQ 'Skipped').Count
        $shortName = if ($fixture.Name) { ($fixture.Name -split '\.')[-1] } else { '(no fixture)' }

        $counts = Format-Count $cases.Count 'test'
        if ($fixtureFailed) { $counts += " · $fixtureFailed ❌" }
        if ($fixtureSkipped) { $counts += " · $fixtureSkipped ⏭️" }
        $open = if ($fixtureFailed) { ' open' } else { '' }

        $md.Add("<details$open><summary title=""$(ConvertTo-HtmlText $fixture.Name)""><b>$(ConvertTo-HtmlText $shortName)</b> · $counts</summary>")
        $md.Add('')
        $md.Add('| | Test | Duration |')
        $md.Add('|:-:|---|--:|')
        foreach ($case in $cases) {
            $md.Add("| $($icons[$case.Outcome]) | $(ConvertTo-TableCell $case.Name) | $(Format-Duration $case.Duration) |")
        }
        $md.Add('')
        $md.Add('</details>')
        $md.Add('')
    }
}

$report = $md -join "`n"
if ($SummaryPath) { Add-Content -LiteralPath $SummaryPath -Value $report -Encoding utf8 }
else { Write-Output $report }

# --- Badges ---

New-Item -ItemType Directory -Force -Path $BadgesDir | Out-Null

foreach ($run in $runs) {
    switch ($run.Status) {
        'Read' { Write-Badge $run.Badge "$($run.Mode) tests" "$($run.Total)" $(if ($run.Total -gt 0) { 'blue' } else { 'lightgrey' }) }
        'Missing' { Write-Badge $run.Badge "$($run.Mode) tests" 'n/a' 'lightgrey' }
        'Unreadable' { Write-Badge $run.Badge "$($run.Mode) tests" 'error' 'red' }
    }
}

if ($failed -gt 0) { Write-Badge 'ci.json' 'CI tests' "$failed failed" 'red' }
elseif ($unreadable) { Write-Badge 'ci.json' 'CI tests' 'error' 'red' }
elseif ($noResults) { Write-Badge 'ci.json' 'CI tests' 'no results' 'red' }
elseif ($total -eq 0) { Write-Badge 'ci.json' 'CI tests' 'no tests' 'lightgrey' }
elseif ($skipped -gt 0) { Write-Badge 'ci.json' 'CI tests' "$passed/$total passing" 'yellowgreen' }
else { Write-Badge 'ci.json' 'CI tests' "$passed/$total passing" 'brightgreen' }

if ($failed -gt 0 -or $noResults -or $unreadable) { exit 1 }
