[CmdletBinding()]
param(
    [string]$OutputPath,
    [string]$WorkingDirectory,
    [switch]$Preflight
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$mtmtVersion = '7.3.51110.1'
$manifestSha256 = '0821d1af5083eb9c4c3aca9101e3e01f32a5f6ae327420de47e316f76e853530'
$payloadBaseUri = 'https://tmtdist.azurewebsites.net/Application%20Files/TMT7_7_3_51110_1'
$manifestUri = "$payloadBaseUri/TMT7.exe.manifest"

if (-not $Preflight -and [Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'The MTMT differential requires Windows, .NET Framework 4.8, and WPF.'
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $PSScriptRoot 'root-scope.mtmt.json'
}

if ([string]::IsNullOrWhiteSpace($WorkingDirectory)) {
    $WorkingDirectory = Join-Path ([IO.Path]::GetTempPath()) ("tmforge-mtmt-root-scope-" + [Guid]::NewGuid().ToString('N'))
}

$OutputPath = [IO.Path]::GetFullPath($OutputPath)
$WorkingDirectory = [IO.Path]::GetFullPath($WorkingDirectory)

if (Test-Path -LiteralPath $WorkingDirectory) {
    throw "The working directory must not already exist: '$WorkingDirectory'."
}

$isSta = [Threading.Thread]::CurrentThread.ApartmentState -eq [Threading.ApartmentState]::STA
if (-not $Preflight -and ([IntPtr]::Size -ne 4 -or -not $isSta)) {
    $x86PowerShell = Join-Path $env:WINDIR 'SysWOW64\WindowsPowerShell\v1.0\powershell.exe'
    if (-not (Test-Path -LiteralPath $x86PowerShell)) {
        throw '32-bit Windows PowerShell is required because MTMT 7.3.51110.1 is x86.'
    }

    & $x86PowerShell -NoProfile -Sta -ExecutionPolicy Bypass -File $PSCommandPath `
        -OutputPath $OutputPath -WorkingDirectory $WorkingDirectory
    exit $LASTEXITCODE
}

New-Item -ItemType Directory -Path $WorkingDirectory | Out-Null
$payloadDirectory = Join-Path $WorkingDirectory "TMT7_$($mtmtVersion.Replace('.', '_'))"
$manifestPath = Join-Path $payloadDirectory 'TMT7.exe.manifest'
$modelPath = Join-Path $WorkingDirectory 'root-scope.tm7'

function Get-Sha256Bytes {
    param([Parameter(Mandatory = $true)][string]$Path)

    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return $algorithm.ComputeHash([IO.File]::ReadAllBytes($Path))
    }
    finally {
        $algorithm.Dispose()
    }
}

function Get-Sha256Hex {
    param([Parameter(Mandatory = $true)][string]$Path)

    return ([BitConverter]::ToString((Get-Sha256Bytes -Path $Path))).Replace('-', '').ToLowerInvariant()
}

function Assert-Sha256Hex {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Expected
    )

    $actual = Get-Sha256Hex -Path $Path
    if ($actual -cne $Expected.ToLowerInvariant()) {
        throw "SHA-256 mismatch for '$Path': expected $Expected, found $actual."
    }
}

function Assert-ManifestDigest {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$ExpectedBase64
    )

    $actual = [Convert]::ToBase64String((Get-Sha256Bytes -Path $Path))
    if ($actual -cne $ExpectedBase64.Trim()) {
        throw "ClickOnce digest mismatch for '$Path'."
    }
}

function Assert-NotReparsePoint {
    param([Parameter(Mandatory = $true)][string]$Path)

    if ((Test-Path -LiteralPath $Path) -and
        ((Get-Item -LiteralPath $Path -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Reparse points are not allowed in the verified payload path: '$Path'."
    }
}

function Get-ConfinedPayloadPath {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    if ([string]::IsNullOrWhiteSpace($RelativePath) -or
        [IO.Path]::IsPathRooted($RelativePath) -or
        $RelativePath.Contains(':')) {
        throw "The ClickOnce manifest contains an unsafe payload path: '$RelativePath'."
    }

    $segments = @($RelativePath -split '[\\/]')
    if ($segments.Count -eq 0 -or
        @($segments | Where-Object { [string]::IsNullOrWhiteSpace($_) -or $_ -eq '.' -or $_ -eq '..' }).Count -ne 0) {
        throw "The ClickOnce manifest contains an unsafe payload path: '$RelativePath'."
    }

    $normalized = $RelativePath.Replace('\', [IO.Path]::DirectorySeparatorChar)
    $destination = [IO.Path]::GetFullPath((Join-Path $payloadDirectory $normalized))
    $payloadPrefix = $payloadDirectory.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $destination.StartsWith($payloadPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "The ClickOnce payload path escapes the working directory: '$RelativePath'."
    }

    return $destination
}

function Read-VerifiedManifest {
    param([Parameter(Mandatory = $true)][string]$Path)

    $settings = New-Object Xml.XmlReaderSettings
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [Xml.XmlReader]::Create($Path, $settings)
    try {
        $document = New-Object Xml.XmlDocument
        $document.XmlResolver = $null
        $document.Load($reader)
        return $document
    }
    finally {
        $reader.Dispose()
    }
}

function Save-VerifiedDownload {
    param(
        [Parameter(Mandatory = $true)][string]$Uri,
        [Parameter(Mandatory = $true)][string]$Destination,
        [string]$ExpectedHex,
        [string]$ExpectedBase64
    )

    $parent = Split-Path -Parent $Destination
    if (-not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }

    Assert-NotReparsePoint -Path $payloadDirectory
    Assert-NotReparsePoint -Path $parent
    $temporaryPath = $Destination + '.download-' + [Guid]::NewGuid().ToString('N')
    try {
        Invoke-WebRequest -Uri $Uri -OutFile $temporaryPath -UseBasicParsing
        if (-not [string]::IsNullOrWhiteSpace($ExpectedHex)) {
            Assert-Sha256Hex -Path $temporaryPath -Expected $ExpectedHex
        }
        elseif (-not [string]::IsNullOrWhiteSpace($ExpectedBase64)) {
            Assert-ManifestDigest -Path $temporaryPath -ExpectedBase64 $ExpectedBase64
        }
        else {
            throw "No expected digest was supplied for '$Uri'."
        }

        Move-Item -LiteralPath $temporaryPath -Destination $Destination
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
    }
}

function Assert-VerifiedPayloadSet {
    param([Parameter(Mandatory = $true)][object[]]$Records)

    foreach ($record in $Records) {
        Assert-NotReparsePoint -Path $record.Path
        Assert-ManifestDigest -Path $record.Path -ExpectedBase64 $record.Digest
    }
}

New-Item -ItemType Directory -Path $payloadDirectory | Out-Null
Save-VerifiedDownload -Uri $manifestUri -Destination $manifestPath -ExpectedHex $manifestSha256
$manifest = Read-VerifiedManifest -Path $manifestPath
$payloadNodes = @()
$payloadNodes += @($manifest.SelectNodes("//*[local-name()='dependentAssembly' and @dependencyType='install' and @codebase]"))
$payloadNodes += @($manifest.SelectNodes("//*[local-name()='file' and (@name='KnowledgeBase\Default.tb7' or @name='TMT7.exe.config')]"))
$payloadRecords = @()
$seenPayloads = @{}

foreach ($node in $payloadNodes) {
    $relativePath = if ($node.HasAttribute('codebase')) {
        $node.GetAttribute('codebase')
    }
    else {
        $node.GetAttribute('name')
    }

    if ($seenPayloads.ContainsKey($relativePath)) {
        throw "The ClickOnce manifest contains a duplicate payload path: '$relativePath'."
    }

    $digestMethod = $node.SelectSingleNode(".//*[local-name()='DigestMethod']")
    $digestNode = $node.SelectSingleNode(".//*[local-name()='DigestValue']")
    if ($null -eq $digestMethod -or
        $digestMethod.GetAttribute('Algorithm') -cne 'http://www.w3.org/2000/09/xmldsig#sha256' -or
        $null -eq $digestNode -or
        [string]::IsNullOrWhiteSpace($digestNode.InnerText)) {
        throw "The ClickOnce manifest has no SHA-256 digest for '$relativePath'."
    }

    $destination = Get-ConfinedPayloadPath -RelativePath $relativePath
    $record = [pscustomobject][ordered]@{
        RelativePath = $relativePath
        Path = $destination
        Digest = $digestNode.InnerText.Trim()
    }
    $payloadRecords += $record
    $seenPayloads[$relativePath] = $true

    $uriRelativePath = $relativePath.Replace('\', '/')
    $escapedSegments = @($uriRelativePath.Split('/') | ForEach-Object { [Uri]::EscapeDataString($_) })
    $downloadUri = "$payloadBaseUri/$($escapedSegments -join '/').deploy"
    Save-VerifiedDownload -Uri $downloadUri -Destination $destination -ExpectedBase64 $record.Digest
}

Assert-Sha256Hex -Path $manifestPath -Expected $manifestSha256
Assert-VerifiedPayloadSet -Records $payloadRecords
if ($Preflight) {
    [pscustomobject][ordered]@{
        mtmtVersion = $mtmtVersion
        manifestSha256 = $manifestSha256
        verifiedPayloads = $payloadRecords.Count
        workingDirectory = $WorkingDirectory
    } | ConvertTo-Json
    return
}

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$fixturePath = Join-Path $PSScriptRoot 'root-scope.tmforge.json'
$fixtureSha256 = Get-Sha256Hex -Path $fixturePath
$cliProject = Join-Path $repoRoot 'src\ThreatModelForge.Cli\ThreatModelForge.Cli.csproj'
$cliAssembly = Join-Path $repoRoot 'out\Debug-x64\ThreatModelForge.Cli\net10.0\tmforge.dll'
$knowledgeBasePath = Get-ConfinedPayloadPath -RelativePath 'KnowledgeBase\Default.tb7'

& dotnet build $cliProject -p:Platform=x64 --nologo
if ($LASTEXITCODE -ne 0) {
    throw "tmforge CLI build failed with exit code $LASTEXITCODE."
}

& dotnet $cliAssembly convert $fixturePath --to tm7 --knowledge-base $knowledgeBasePath --out $modelPath
if ($LASTEXITCODE -ne 0) {
    throw "tmforge fixture conversion failed with exit code $LASTEXITCODE."
}

if ((Get-Sha256Hex -Path $fixturePath) -cne $fixtureSha256) {
    throw 'The source fixture changed while the differential model was being generated.'
}

$modelSha256 = Get-Sha256Hex -Path $modelPath
Assert-Sha256Hex -Path $manifestPath -Expected $manifestSha256
Assert-VerifiedPayloadSet -Records $payloadRecords
Assert-Sha256Hex -Path $modelPath -Expected $modelSha256

$assemblyRecords = @{}
foreach ($record in $payloadRecords) {
    if ($record.Path.EndsWith('.dll', [StringComparison]::OrdinalIgnoreCase)) {
        $assemblyName = [IO.Path]::GetFileNameWithoutExtension($record.Path)
        if ($assemblyRecords.ContainsKey($assemblyName)) {
            throw "The ClickOnce manifest contains duplicate assembly names: '$assemblyName'."
        }

        $assemblyRecords[$assemblyName] = $record
    }
}

foreach ($dll in Get-ChildItem -LiteralPath $payloadDirectory -Filter '*.dll' -Recurse) {
    if (-not $assemblyRecords.ContainsKey([IO.Path]::GetFileNameWithoutExtension($dll.Name))) {
        throw "An unverified assembly is present in the payload directory: '$($dll.FullName)'."
    }
}

$assemblyResolver = [ResolveEventHandler] {
    param($sender, $eventArgs)

    $assemblyName = New-Object Reflection.AssemblyName($eventArgs.Name)
    if ($assemblyRecords.ContainsKey($assemblyName.Name)) {
        $record = $assemblyRecords[$assemblyName.Name]
        Assert-ManifestDigest -Path $record.Path -ExpectedBase64 $record.Digest
        return [Reflection.Assembly]::LoadFrom($record.Path)
    }

    return $null
}.GetNewClosure()

[AppDomain]::CurrentDomain.add_AssemblyResolve($assemblyResolver)
$previousDirectory = Get-Location
try {
    Assert-VerifiedPayloadSet -Records $payloadRecords
    Assert-Sha256Hex -Path $modelPath -Expected $modelSha256
    Set-Location -LiteralPath $payloadDirectory
    $abstractsRecord = $assemblyRecords['ThreatModeling.ExternalStorage.Abstracts']
    $localRecord = $assemblyRecords['ThreatModeling.ExternalStorage.Local']
    $modelRecord = $assemblyRecords['ThreatModeling.Model']
    Assert-ManifestDigest -Path $abstractsRecord.Path -ExpectedBase64 $abstractsRecord.Digest
    Assert-ManifestDigest -Path $localRecord.Path -ExpectedBase64 $localRecord.Digest
    Assert-ManifestDigest -Path $modelRecord.Path -ExpectedBase64 $modelRecord.Digest
    $abstractsAssembly = [Reflection.Assembly]::LoadFrom($abstractsRecord.Path)
    $localAssembly = [Reflection.Assembly]::LoadFrom($localRecord.Path)
    $modelAssembly = [Reflection.Assembly]::LoadFrom($modelRecord.Path)

    $storageFileType = $abstractsAssembly.GetType('ThreatModeling.ExternalStorage.Abstracts.StorageFile', $true)
    $localFileType = $localAssembly.GetType('ThreatModeling.ExternalStorage.Local.LocalFile', $true)
    $objectModelType = $modelAssembly.GetType('ThreatModeling.Model.ObjectModel', $true)
    $localFile = [Activator]::CreateInstance($localFileType, [object[]]@($modelPath))
    $constructor = $objectModelType.GetConstructor([Type[]]@($storageFileType, [bool]))
    if ($null -eq $constructor) {
        throw 'MTMT no longer exposes ObjectModel(StorageFile, bool).'
    }

    $model = $constructor.Invoke([object[]]@($localFile, $false))
    $model.ConfigureThreatGeneration($true)
    $generated = @($model.GenerateThreats())
}
finally {
    Set-Location -LiteralPath $previousDirectory
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($assemblyResolver)
}

$rootTypeByTitle = @{
    'Spoofing (v3)' = 'SU'
    'Tampering (v3)' = 'TU'
    'Repudiation (v3)' = 'RU'
    'Information Disclosure (v3)' = 'IU'
    'Denial Of Service (v3)' = 'DU'
    'Elevation Of Privilege (v3)' = 'EU'
}

$rootThreats = @()
foreach ($threat in $generated) {
    $title = [string]$threat.Title
    if (-not $rootTypeByTitle.ContainsKey($title)) {
        continue
    }

    $interactionKey = [string]$threat.InteractionKey
    $parts = if ([string]::IsNullOrWhiteSpace($interactionKey)) { @() } else { @($interactionKey.Split(':')) }
    $rootThreats += [pscustomobject][ordered]@{
        typeId = $rootTypeByTitle[$title]
        title = $title
        diagram = [string]$threat.Diagram
        sourceGuid = if ($parts.Count -gt 0) { $parts[0] } else { $null }
        flowGuid = if ($parts.Count -gt 1) { $parts[1] } else { $null }
        targetGuid = if ($parts.Count -gt 2) { $parts[2] } else { $null }
        interactionKey = $interactionKey
        interactionString = [string]$threat.InteractionString
        key = [string]$threat.Key
    }
}

$rootThreats = @($rootThreats | Sort-Object -Property diagram, typeId)
$diagramGroups = @($rootThreats | Group-Object -Property diagram | Sort-Object -Property Name)
$diagramCounts = @($diagramGroups | ForEach-Object {
    [pscustomobject][ordered]@{ diagram = $_.Name; count = $_.Count }
})
$expectedRootTypes = @('DU', 'EU', 'IU', 'RU', 'SU', 'TU')
$testRootTypeMultiplicity = {
    param([object[]]$Threats, [int]$Multiplicity)

    if ($Threats.Count -ne $expectedRootTypes.Count * $Multiplicity) {
        return $false
    }

    foreach ($typeId in $expectedRootTypes) {
        if (@($Threats | Where-Object { $_.typeId -ceq $typeId }).Count -ne $Multiplicity) {
            return $false
        }
    }

    return $true
}
$hasExactGlobalSet = & $testRootTypeMultiplicity $rootThreats 1
$groupsByDiagram = @{}
foreach ($group in $diagramGroups) {
    $groupsByDiagram[$group.Name] = $group
}

$hasExpectedDiagrams = $groupsByDiagram.Count -eq 2 -and
    $groupsByDiagram.ContainsKey('Diagram A') -and
    $groupsByDiagram.ContainsKey('Diagram B')
$hasExactPerDiagramSet = $hasExpectedDiagrams -and
    (& $testRootTypeMultiplicity @($groupsByDiagram['Diagram A'].Group) 1) -and
    (& $testRootTypeMultiplicity @($groupsByDiagram['Diagram B'].Group) 1)
$hasExactPerInteractionSet = $hasExpectedDiagrams -and
    (& $testRootTypeMultiplicity @($groupsByDiagram['Diagram A'].Group) 1) -and
    (& $testRootTypeMultiplicity @($groupsByDiagram['Diagram B'].Group) 2)
$interpretation = if ($hasExactPerDiagramSet) {
    'per-diagram'
}
elseif ($hasExactPerInteractionSet) {
    'per-interaction'
}
elseif ($hasExactGlobalSet) {
    'model-wide'
}
elseif ($rootThreats.Count -eq 0) {
    'not-generated'
}
else {
    'unexpected'
}

$result = [pscustomobject][ordered]@{
    schema = 'tmforge-mtmt-differential'
    version = 1
    mtmt = [pscustomobject][ordered]@{
        version = $mtmtVersion
        manifestUri = $manifestUri
        manifestSha256 = $manifestSha256
        defaultKnowledgeBaseSha256 = Get-Sha256Hex -Path $knowledgeBasePath
    }
    fixture = [pscustomobject][ordered]@{
        source = 'root-scope.tmforge.json'
        sourceSha256 = $fixtureSha256
        generatedModelSha256 = $modelSha256
        diagrams = 2
        interactions = 3
    }
    generatedThreatCount = $generated.Count
    rootThreatCount = $rootThreats.Count
    interpretation = $interpretation
    rootThreatsByDiagram = $diagramCounts
    rootThreats = $rootThreats
}

$json = $result | ConvertTo-Json -Depth 8
$outputDirectory = Split-Path -Parent $OutputPath
if (-not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

$utf8 = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllText($OutputPath, $json + [Environment]::NewLine, $utf8)
Write-Output $json
