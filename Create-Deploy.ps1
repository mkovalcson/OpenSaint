<#
.SYNOPSIS
Creates or refreshes a complete, self-contained J5_Animator deployment.

.DESCRIPTION
Publishes the WPF application for 64-bit Windows directly into J5_Animator,
copies the current animatorConfig folder as J5_Animator\Config, validates the
staged deployment, and then replaces the existing J5_Animator folder. The
application automatically discovers the child Config folder.

The existing deployment is retained until the new one is fully staged and
validated. EditorRecovery.json is excluded by default because it is transient
crash-recovery data. Use -IncludeRecoverySnapshot to include it intentionally.

.EXAMPLE
& 'X:\Johnny5\Software\Create-Deploy.ps1'

.EXAMPLE
& 'X:\Johnny5\Software\Create-Deploy.ps1' -WhatIf
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [Parameter()]
    [ValidateSet('Release', 'Debug')]
    [string] $Configuration = 'Release',

    [Parameter()]
    [ValidateSet('win-x64', 'win-arm64')]
    [string] $RuntimeIdentifier = 'win-x64',

    [Parameter()]
    [switch] $IncludeRecoverySnapshot,

    [Parameter()]
    [switch] $KeepPreviousDeployment
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$softwareRoot = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar)
$projectFolder = Join-Path $softwareRoot 'ServoAnimator'
$projectFile = Join-Path $projectFolder 'ServoAnimator.csproj'
$configFolder = Join-Path $softwareRoot 'Config'
if (-not (Test-Path -LiteralPath $configFolder -PathType Container)) {
    $configFolder = Join-Path $softwareRoot 'animatorConfig'
}
$deployFolder = Join-Path $softwareRoot 'J5_Animator'
$operationId = [Guid]::NewGuid().ToString('N')
$stagingFolder = Join-Path $softwareRoot ".J5_Animator.staging-$operationId"
$backupFolder = Join-Path $softwareRoot ".J5_Animator.previous-$operationId"

function Assert-RequiredPath {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string] $Description,

        [Parameter()]
        [switch] $Directory
    )

    $pathType = if ($Directory) { 'Container' } else { 'Leaf' }
    if (-not (Test-Path -LiteralPath $Path -PathType $pathType)) {
        throw "$Description was not found: $Path"
    }
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

function Assert-SafeGeneratedPath {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string] $ExpectedNamePrefix
    )

    $fullPath = [IO.Path]::GetFullPath($Path).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
    $expectedParent = $softwareRoot + [IO.Path]::DirectorySeparatorChar
    $leafName = Split-Path -Leaf $fullPath

    if (-not $fullPath.StartsWith($expectedParent,
            [StringComparison]::OrdinalIgnoreCase) -or
        -not $leafName.StartsWith($ExpectedNamePrefix,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing an unsafe deployment path: $fullPath"
    }
}

function Copy-ConfigurationTree {
    param(
        [Parameter(Mandatory)]
        [string] $SourceRoot,

        [Parameter(Mandatory)]
        [string] $TargetRoot
    )

    New-Item -ItemType Directory -Path $TargetRoot -Force | Out-Null

    # Preserve empty configuration/library folders as well as files.
    foreach ($sourceDirectory in Get-ChildItem -LiteralPath $SourceRoot -Recurse -Directory) {
        $relativeDirectory = Get-RelativeChildPath `
            -ParentPath $SourceRoot `
            -ChildPath $sourceDirectory.FullName
        New-Item -ItemType Directory `
            -Path (Join-Path $TargetRoot $relativeDirectory) `
            -Force | Out-Null
    }

    foreach ($sourceFile in Get-ChildItem -LiteralPath $SourceRoot -Recurse -File) {
        $relativeFile = Get-RelativeChildPath `
            -ParentPath $SourceRoot `
            -ChildPath $sourceFile.FullName
        if (-not $IncludeRecoverySnapshot -and
            $relativeFile -ieq 'EditorRecovery.json') {
            continue
        }

        $targetFile = Join-Path $TargetRoot $relativeFile
        $targetDirectory = Split-Path -Parent $targetFile
        if (-not (Test-Path -LiteralPath $targetDirectory -PathType Container)) {
            New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
        }
        Copy-Item -LiteralPath $sourceFile.FullName -Destination $targetFile -Force
    }
}

Assert-RequiredPath -Path $projectFile -Description 'ServoAnimator project file'
Assert-RequiredPath -Path $configFolder -Description 'Live configuration folder' -Directory
Assert-SafeGeneratedPath -Path $deployFolder -ExpectedNamePrefix 'J5_Animator'
Assert-SafeGeneratedPath -Path $stagingFolder -ExpectedNamePrefix '.J5_Animator.staging-'
Assert-SafeGeneratedPath -Path $backupFolder -ExpectedNamePrefix '.J5_Animator.previous-'

$description = "Publish $Configuration/$RuntimeIdentifier and copy the current animatorConfig"
if (-not $PSCmdlet.ShouldProcess($deployFolder, $description)) {
    Write-Host ''
    Write-Host 'Preview complete. No build or file changes were made.'
    Write-Host "Deployment target: $deployFolder"
    Write-Host "Application target: $deployFolder"
    Write-Host "Configuration target: $(Join-Path $deployFolder 'Config')"
    return
}

$stagedApp = $stagingFolder
$stagedConfig = Join-Path $stagingFolder 'Config'
$backupCreated = $false
$deploymentInstalled = $false

try {
    New-Item -ItemType Directory -Path $stagedApp -Force | Out-Null

    Write-Host "Publishing ServoAnimator ($Configuration, $RuntimeIdentifier, self-contained)..."
    & dotnet publish $projectFile `
        --configuration $Configuration `
        --runtime $RuntimeIdentifier `
        --self-contained true `
        --output $stagedApp `
        -p:PublishSingleFile=false `
        -p:PublishReadyToRun=true

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    # A deployment must discover its own child Config folder. Never carry a
    # developer-machine Paths.json/Folder.json into the portable package.
    foreach ($settingsName in @('Paths.json', 'Folder.json')) {
        $publishedSettings = Join-Path $stagedApp $settingsName
        if (Test-Path -LiteralPath $publishedSettings -PathType Leaf) {
            Remove-Item -LiteralPath $publishedSettings -Force
        }
    }

    Write-Host 'Copying the current animatorConfig as Config...'
    Copy-ConfigurationTree -SourceRoot $configFolder -TargetRoot $stagedConfig

    # Validate the content needed for a runnable deployment before touching
    # the currently installed J5_Animator folder.
    Assert-RequiredPath `
        -Path (Join-Path $stagedApp 'AnimationEditorPlayer.exe') `
        -Description 'Published application executable'
    Assert-RequiredPath `
        -Path (Join-Path $stagedApp 'Help') `
        -Description 'Published Help folder' `
        -Directory
    Assert-RequiredPath `
        -Path (Join-Path $stagedApp 'Models\johnny5_head.urdf') `
        -Description 'Published URDF model'
    Assert-RequiredPath `
        -Path (Join-Path $stagedApp 'Models\Meshes') `
        -Description 'Published model meshes' `
        -Directory
    Assert-RequiredPath `
        -Path (Join-Path $stagedConfig 'ServoConfig.json') `
        -Description 'Deployed servo configuration'
    Assert-RequiredPath `
        -Path (Join-Path $stagedConfig 'URDFconfig.json') `
        -Description 'Deployed URDF configuration'
    Assert-RequiredPath `
        -Path (Join-Path $stagedConfig 'TIC\ticcmd.exe') `
        -Description 'Deployed Tic controller utility'

    if (Test-Path -LiteralPath $deployFolder) {
        Write-Host 'Preserving the previous deployment until installation succeeds...'
        Move-Item -LiteralPath $deployFolder -Destination $backupFolder
        $backupCreated = $true
    }

    try {
        Move-Item -LiteralPath $stagingFolder -Destination $deployFolder
        $deploymentInstalled = $true
    }
    catch {
        if ($backupCreated -and
            -not (Test-Path -LiteralPath $deployFolder) -and
            (Test-Path -LiteralPath $backupFolder)) {
            Move-Item -LiteralPath $backupFolder -Destination $deployFolder
            $backupCreated = $false
        }
        throw
    }

    if ($backupCreated) {
        if ($KeepPreviousDeployment) {
            $keptBackup = Join-Path $softwareRoot 'J5_Animator.previous'
            Assert-SafeGeneratedPath -Path $keptBackup -ExpectedNamePrefix 'J5_Animator.previous'
            if (Test-Path -LiteralPath $keptBackup) {
                Remove-Item -LiteralPath $keptBackup -Recurse -Force
            }
            Move-Item -LiteralPath $backupFolder -Destination $keptBackup
            Write-Host "Previous deployment retained at: $keptBackup"
        }
        else {
            Remove-Item -LiteralPath $backupFolder -Recurse -Force
        }
        $backupCreated = $false
    }

    $deployedExe = Join-Path $deployFolder 'AnimationEditorPlayer.exe'
    $fileCount = (Get-ChildItem -LiteralPath $deployFolder -Recurse -File).Count
    $totalBytes = (Get-ChildItem -LiteralPath $deployFolder -Recurse -File |
        Measure-Object -Property Length -Sum).Sum
    $sizeMb = [Math]::Round($totalBytes / 1MB, 1)

    Write-Host ''
    Write-Host 'Deployment completed successfully.'
    Write-Host "Executable: $deployedExe"
    Write-Host "Files: $fileCount"
    Write-Host "Size: $sizeMb MB"
}
finally {
    # Only generated, validated child paths of Software are ever removed.
    if (Test-Path -LiteralPath $stagingFolder) {
        Remove-Item -LiteralPath $stagingFolder -Recurse -Force
    }

    if (-not $deploymentInstalled -and $backupCreated -and
        -not (Test-Path -LiteralPath $deployFolder) -and
        (Test-Path -LiteralPath $backupFolder)) {
        Move-Item -LiteralPath $backupFolder -Destination $deployFolder
        $backupCreated = $false
    }
}
