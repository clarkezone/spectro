[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('win-x64', 'win-arm64')][string]$Runtime,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')][string]$PackageVersion = '1.0.0.0'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$projectDirectory = Join-Path $root 'src\Spectro.App'
$project = Join-Path $projectDirectory 'Spectro.App.csproj'
$platform = if ($Runtime -eq 'win-arm64') { 'ARM64' } else { 'x64' }
$properties = & dotnet msbuild $project -nologo -p:Configuration=Release "-p:Platform=$platform" `
    "-p:RuntimeIdentifier=$Runtime" '-getProperty:TargetDir,PublishDir'
if ($LASTEXITCODE -ne 0) { throw 'Could not resolve the Release build output paths.' }
$properties = ($properties -join "`n" | ConvertFrom-Json).Properties
$publish = (Resolve-Path -LiteralPath (Join-Path $projectDirectory $properties.PublishDir)).Path
$build = $properties.TargetDir
$output = [IO.Path]::GetFullPath($OutputDirectory)
$architecture = $Runtime.Substring(4)
$version = [version]$PackageVersion
if (@($version.Major, $version.Minor, $version.Build, $version.Revision).Where({ $_ -gt 65535 }).Count) {
    throw 'Each MSIX version component must be between 0 and 65535.'
}
foreach ($required in @('Spectro.App.exe', 'resources.pri')) {
    if (-not (Test-Path -LiteralPath (Join-Path $publish $required))) {
        throw "The Native AOT publish output is missing $required."
    }
}
if (Test-Path -LiteralPath (Join-Path $publish 'Spectro.App.dll')) {
    throw 'Expected Native AOT output, not a managed build directory.'
}
$manifestPath = Join-Path $build 'AppxManifest.xml'
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw 'The generated build AppxManifest.xml is required alongside the publish directory.'
}
[xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
if ($manifest.Package.Identity.ProcessorArchitecture -ne $architecture) {
    throw 'The generated manifest architecture does not match the requested runtime.'
}
$manifest.Package.Identity.Version = $PackageVersion
$packageName = "Spectro-$PackageVersion-$architecture"
$packagePath = Join-Path $output "$packageName.msix"
$certificatePath = Join-Path $output "$packageName.cer"
if ((Test-Path -LiteralPath $packagePath) -or (Test-Path -LiteralPath $certificatePath)) {
    throw 'The output package or certificate already exists. Use a new version or output directory.'
}

$work = Join-Path ([IO.Path]::GetTempPath()) ('spectro-msix-' + [guid]::NewGuid().ToString('N'))
$layout = Join-Path $work 'layout'
$key = Join-Path $work 'test-signing.pfx'
try {
    $null = New-Item -ItemType Directory -Path $layout, $output -Force
    Get-ChildItem -LiteralPath $publish | Where-Object Extension -ne '.pdb' |
        Copy-Item -Destination $layout -Recurse
    Get-ChildItem -LiteralPath $build -Filter '*.xbf' | Copy-Item -Destination $layout
    $assets = Join-Path $layout 'Assets'
    $null = New-Item -ItemType Directory -Path $assets
    Get-ChildItem -LiteralPath (Join-Path $root 'src\Spectro.App\Assets') -File |
        Copy-Item -Destination $assets
    $stagedManifest = Join-Path $layout 'AppxManifest.xml'
    $manifest.Save($stagedManifest)

    & winapp cert generate --manifest $stagedManifest --output $key --export-cer --valid-days 90 --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Test signing certificate generation failed.' }
    # The .NET project bundles its pinned Windows App SDK using WindowsAppSDKSelfContained.
    & winapp package $layout --cert $key --output $packagePath --quiet
    if ($LASTEXITCODE -ne 0) { throw 'MSIX packaging failed.' }
    Copy-Item -LiteralPath ([IO.Path]::ChangeExtension($key, '.cer')) -Destination $certificatePath
    $certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($certificatePath)
    try {
        if ((Get-AuthenticodeSignature -LiteralPath $packagePath).SignerCertificate.Thumbprint -ne
            $certificate.Thumbprint) {
            throw 'The signed MSIX does not match its public test certificate.'
        }
    }
    finally { $certificate.Dispose() }

    $archive = [IO.Compression.ZipFile]::OpenRead($packagePath)
    try {
        foreach ($required in @('AppxManifest.xml', 'AppxSignature.p7x', 'Spectro.App.exe',
                'resources.pri', 'Microsoft.ui.xaml.dll', 'MainPage.xbf', 'Assets/StoreLogo.png')) {
            if (-not $archive.GetEntry($required)) { throw "The MSIX is missing $required." }
        }
        if ($archive.Entries.FullName -match '(^|/)(Spectro\.App\.dll|coreclr\.dll|.*\.pfx)$|^Assets/Demo/') {
            throw 'The MSIX contains managed runtime, private signing material, or demo content.'
        }
        $reader = [IO.StreamReader]::new($archive.GetEntry('AppxManifest.xml').Open())
        try { [xml]$packagedManifest = $reader.ReadToEnd() }
        finally { $reader.Dispose() }
        if ($packagedManifest.Package.Dependencies.PackageDependency) {
            throw 'Expected self-contained MSIX output without external framework package dependencies.'
        }
    }
    finally { $archive.Dispose() }
    Copy-Item -LiteralPath (Join-Path $root 'docs\installing-test-builds.md') `
        -Destination (Join-Path $output 'INSTALL.md')

    [pscustomobject]@{
        package = $packagePath
        certificate = $certificatePath
        version = $PackageVersion
        architecture = $architecture
        signing = 'Self-signed test certificate; explicitly trust the public CER before installing.'
    } | ConvertTo-Json
}
finally {
    if (Test-Path -LiteralPath $work) {
        Remove-Item -LiteralPath $work -Recurse -Force
    }
}
