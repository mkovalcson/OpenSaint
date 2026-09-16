<#
.SYNOPSIS
Copies changed/new ServoAnimator project and configuration files from the
live folders into the local GitHub working tree.

.DESCRIPTION
The update is non-destructive: destination-only files are retained. Project
build output (.vs, bin, obj), .slnx files, and EditorRecovery.json are excluded
by default. Use -WhatIf to preview and -IncludeRecoverySnapshot only when the
transient recovery file is intentionally wanted in the working tree.
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [Parameter()]
    [string] $LiveProject = 'X:\Johnny5\Software\ServoAnimator',

    [Parameter()]
    [string] $GitHubProject = 'X:\Johnny5\OpenSaint\ServoAnimator',

    [Parameter()]
    [string] $LiveConfig = 'X:\Johnny5\Software\Config',

    [Parameter()]
    [string] $GitHubConfig = 'X:\Johnny5\OpenSaint\animatorConfig',

    [Parameter()]
    [switch] $IncludeRecoverySnapshot,

    [Parameter()]
    [switch] $SkipGitStatus
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-ExistingDirectory {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string] $Description
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw "$Description does not exist: $Path"
    }

    return [IO.Path]::GetFullPath($Path).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
}

function Test-ProjectFileIncluded {
    param(
        [Parameter(Mandatory)]
        [string] $RelativePath
    )

    if ([IO.Path]::GetExtension($RelativePath) -ieq '.slnx') {
        return $false
    }
    if ($RelativePath -like '*_wpftmp.csproj') {
        return $false
    }

    return $RelativePath -notmatch '(^|\\)(bin|obj|\.vs)(\\|$)'
}

function Get-RelativeChildPath {
    param(
        [Parameter(Mandatory)]
        [string] $ParentPath,

        [Parameter(Mandatory)]
        [string] $ChildPath
    )

    # Compatible with both Windows PowerShell 5.1 and PowerShell 7.
    $parentPrefix = $ParentPath.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $fullChildPath = [IO.Path]::GetFullPath($ChildPath)
    if (-not $fullChildPath.StartsWith($parentPrefix,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is not inside $ParentPath`: $fullChildPath"
    }
    return $fullChildPath.Substring($parentPrefix.Length)
}

function Test-ConfigFileIncluded {
    param(
        [Parameter(Mandatory)]
        [string] $RelativePath
    )

    if (-not $IncludeRecoverySnapshot -and
        $RelativePath -ieq 'EditorRecovery.json') {
        return $false
    }

    return $true
}

function Test-FilesEqual {
    param(
        [Parameter(Mandatory)]
        [IO.FileInfo] $SourceFile,

        [Parameter(Mandatory)]
        [string] $TargetFile
    )

    if (-not (Test-Path -LiteralPath $TargetFile -PathType Leaf)) {
        return $false
    }

    $targetInfo = Get-Item -LiteralPath $TargetFile
    if ($SourceFile.Length -ne $targetInfo.Length) {
        return $false
    }

    $sourceHash = Get-Sha256 -Path $SourceFile.FullName
    $targetHash = Get-Sha256 -Path $TargetFile
    return $sourceHash -eq $targetHash
}

function Get-Sha256 {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    # Avoid requiring the Microsoft.PowerShell.Utility Get-FileHash cmdlet,
    # which is unavailable in some locked-down Windows PowerShell hosts.
    $algorithm = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::OpenRead($Path)
    try {
        return [BitConverter]::ToString(
            $algorithm.ComputeHash($stream)).Replace('-', '')
    }
    finally {
        $stream.Dispose()
        $algorithm.Dispose()
    }
}

function Sync-ChangedFiles {
    param(
        [Parameter(Mandatory)]
        [string] $SourceRoot,

        [Parameter(Mandatory)]
        [string] $TargetRoot,

        [Parameter(Mandatory)]
        [string] $Label,

        [Parameter(Mandatory)]
        [scriptblock] $Include
    )

    $changed = [Collections.Generic.List[string]]::new()
    $copiedCount = 0
    $unchanged = 0
    $excluded = 0
    $targetPrefix = $TargetRoot + [IO.Path]::DirectorySeparatorChar

    foreach ($sourceFile in Get-ChildItem -LiteralPath $SourceRoot -Recurse -File) {
        $relativePath = Get-RelativeChildPath `
            -ParentPath $SourceRoot `
            -ChildPath $sourceFile.FullName
        if (-not (& $Include $relativePath)) {
            $excluded++
            continue
        }

        $targetFile = [IO.Path]::GetFullPath((Join-Path $TargetRoot $relativePath))
        if (-not $targetFile.StartsWith($targetPrefix,
                [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing a target outside $TargetRoot`: $targetFile"
        }

        if (Test-FilesEqual -SourceFile $sourceFile -TargetFile $targetFile) {
            $unchanged++
            continue
        }

        $changed.Add($relativePath)
        if ($PSCmdlet.ShouldProcess($targetFile, "Copy from $($sourceFile.FullName)")) {
            $targetDirectory = Split-Path -Parent $targetFile
            if (-not (Test-Path -LiteralPath $targetDirectory -PathType Container)) {
                New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
            }

            Copy-Item -LiteralPath $sourceFile.FullName -Destination $targetFile -Force
            $copiedCount++
        }
    }

    [PSCustomObject]@{
        Label = $Label
        Changed = $changed
        CopiedCount = $copiedCount
        Unchanged = $unchanged
        Excluded = $excluded
    }
}

$resolvedLiveProject = Resolve-ExistingDirectory $LiveProject 'Live project folder'
$resolvedGitHubProject = Resolve-ExistingDirectory $GitHubProject 'GitHub project folder'
$resolvedLiveConfig = Resolve-ExistingDirectory $LiveConfig 'Live configuration folder'
$resolvedGitHubConfig = Resolve-ExistingDirectory $GitHubConfig 'GitHub configuration folder'

$projectResult = Sync-ChangedFiles `
    -SourceRoot $resolvedLiveProject `
    -TargetRoot $resolvedGitHubProject `
    -Label 'Project' `
    -Include ${function:Test-ProjectFileIncluded}

$configResult = Sync-ChangedFiles `
    -SourceRoot $resolvedLiveConfig `
    -TargetRoot $resolvedGitHubConfig `
    -Label 'Configuration' `
    -Include ${function:Test-ConfigFileIncluded}

Write-Host ''
foreach ($result in @($projectResult, $configResult)) {
    $changeDescription = if ($WhatIfPreference) {
        "$($result.Changed.Count) would copy"
    }
    else {
        "$($result.CopiedCount) copied"
    }
    Write-Host "$($result.Label): $changeDescription, $($result.Unchanged) unchanged, $($result.Excluded) excluded"
    foreach ($relativePath in $result.Changed) {
        Write-Host "  $relativePath"
    }
}

$totalCopied = $projectResult.CopiedCount + $configResult.CopiedCount
Write-Host ''
if ($WhatIfPreference) {
    Write-Host 'Preview complete. No files were changed because -WhatIf was used.'
}
else {
    Write-Host "Update complete. $totalCopied changed/new files copied; no destination files deleted."
}

if (-not $SkipGitStatus) {
    $gitRoot = [IO.Path]::GetFullPath((Join-Path $resolvedGitHubProject '..'))
    $gitCommand = Get-Command git -ErrorAction SilentlyContinue
    if ($null -ne $gitCommand -and
        (Test-Path -LiteralPath (Join-Path $gitRoot '.git'))) {
        Write-Host ''
        Write-Host "Git status for $gitRoot`:"
        $safeGitRoot = $gitRoot.Replace('\', '/')
        & git -c "safe.directory=$safeGitRoot" -C $gitRoot status --short -- ServoAnimator animatorConfig
        if ($LASTEXITCODE -ne 0) {
            throw "git status failed with exit code $LASTEXITCODE"
        }
    }
}
