[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ManifestPath,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[^/]+/[^/]+$')]
    [string] $Repository,

    [Parameter(Mandatory = $true)]
    [string] $OutputPath,

    [string] $SourceBranch = 'main',

    [string] $IconPath = 'Chatterbox/images/Chatterbox.png',

    [long] $LastUpdate = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
)

$ErrorActionPreference = 'Stop'

$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
$owner, $repositoryName = $Repository.Split('/', 2)
$repositoryUrl = "https://github.com/$Repository"
$releaseUrl = "$repositoryUrl/releases/latest/download/latest.zip"
$testingUrl = "$repositoryUrl/releases/download/testing/latest.zip"
$normalizedIconPath = $IconPath.Replace('\', '/')
$iconUrl = "https://raw.githubusercontent.com/$Repository/$SourceBranch/$normalizedIconPath"

$tags = @($manifest.Tags)
if ($tags.Count -eq 1 -and $tags[0] -is [string]) {
    $tags = @($tags[0].Split(';', [StringSplitOptions]::RemoveEmptyEntries))
}

$entry = [ordered]@{
    Author = if ([string]::IsNullOrWhiteSpace($manifest.Author)) { $owner } else { $manifest.Author }
    Name = $manifest.Name
    InternalName = $manifest.InternalName
    AssemblyVersion = $manifest.AssemblyVersion
    Description = $manifest.Description
    ApplicableVersion = if ([string]::IsNullOrWhiteSpace($manifest.ApplicableVersion)) { 'any' } else { $manifest.ApplicableVersion }
    Tags = $tags
    DalamudApiLevel = [int]$manifest.DalamudApiLevel
    LoadRequiredState = [int]$manifest.LoadRequiredState
    LoadSync = [bool]$manifest.LoadSync
    CanUnloadAsync = [bool]$manifest.CanUnloadAsync
    LoadPriority = [int]$manifest.LoadPriority
    IconUrl = $iconUrl
    Punchline = $manifest.Punchline
    AcceptsFeedback = if ($null -eq $manifest.AcceptsFeedback) { $true } else { [bool]$manifest.AcceptsFeedback }
    RepoUrl = $repositoryUrl
    DownloadLinkInstall = $releaseUrl
    DownloadLinkUpdate = $releaseUrl
    DownloadLinkTesting = $testingUrl
    LastUpdate = $LastUpdate
}

$outputDirectory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

ConvertTo-Json -InputObject @($entry) -Depth 8 -Compress | Set-Content -LiteralPath $OutputPath -Encoding utf8NoBOM

$generated = Get-Content -LiteralPath $OutputPath -Raw | ConvertFrom-Json
if ($generated.Count -ne 1 -or $generated[0].InternalName -ne $manifest.InternalName) {
    throw "Generated repository manifest failed validation: $OutputPath"
}

Write-Output "Generated $OutputPath for $repositoryName $($manifest.AssemblyVersion)."
