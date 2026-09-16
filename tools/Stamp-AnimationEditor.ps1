param(
    [Parameter(Mandatory = $true)][ValidateSet('Prepare', 'Commit')][string]$Phase,
    [Parameter(Mandatory = $true)][string]$MetadataPath,
    [Parameter(Mandatory = $true)][string]$StampPath
)
$ErrorActionPreference = 'Stop'
$metadata = Get-Content -LiteralPath $MetadataPath -Raw | ConvertFrom-Json
if ($metadata.version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid project version; expected major.minor.patch.' }
$previousDate = [datetime]::ParseExact($metadata.generatedDate, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)

if ($Phase -eq 'Prepare') {
    $today = (Get-Date).Date
    if ($today -lt $previousDate) { throw 'The system date precedes the last build date. Correct the clock before building.' }
    $parts = $metadata.version.Split('.') | ForEach-Object { [int]$_ }
    if ($today -eq $previousDate) { $parts[2]++ }
    else { $parts[1]++; $parts[2] = 0 }
    if (($parts | Where-Object { $_ -ge 65535 }).Count -gt 0) { throw 'Version exceeds the assembly version range.' }
    $nextVersion = $parts -join '.'
    $date = $today.ToString('yyyy-MM-dd')
    $parent = Split-Path -Parent $StampPath
    [IO.Directory]::CreateDirectory($parent) | Out-Null
    $stamp = @"
<Project><PropertyGroup>
  <GeneratedVersion>$nextVersion</GeneratedVersion>
  <GeneratedDate>$date</GeneratedDate>
  <PreviousVersion>$($metadata.version)</PreviousVersion>
  <PreviousDate>$($metadata.generatedDate)</PreviousDate>
</PropertyGroup></Project>
"@
    [IO.File]::WriteAllText($StampPath, $stamp)
    Write-Output "Animation Editor build: $nextVersion ($date)"
}
else {
    [xml]$stamp = Get-Content -LiteralPath $StampPath -Raw
    $values = $stamp.Project.PropertyGroup
    if ($metadata.version -ne $values.PreviousVersion -or $metadata.generatedDate -ne $values.PreviousDate) {
        throw 'Another build changed the version record. Run editor builds sequentially.'
    }
    $metadata.version = [string]$values.GeneratedVersion
    $metadata.generatedDate = [string]$values.GeneratedDate
    $metadata.notes = 'Updated automatically after a successful Animation Editor build.'
    $temporaryPath = $MetadataPath + '.' + [guid]::NewGuid().ToString('N') + '.tmp'
    $backupPath = $temporaryPath + '.previous'
    try {
        [IO.File]::WriteAllText($temporaryPath, ($metadata | ConvertTo-Json -Depth 10) + [Environment]::NewLine)
        [IO.File]::Replace($temporaryPath, $MetadataPath, $backupPath)
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) { Remove-Item -LiteralPath $temporaryPath }
        if (Test-Path -LiteralPath $backupPath) { Remove-Item -LiteralPath $backupPath }
    }
}
