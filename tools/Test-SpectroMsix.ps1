#Requires -RunAsAdministrator
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackagePath,
    [Parameter(Mandatory)][string]$CertificatePath
)

$ErrorActionPreference = 'Stop'
$packagePath = (Resolve-Path -LiteralPath $PackagePath).Path
$certificatePath = (Resolve-Path -LiteralPath $CertificatePath).Path
$archive = [IO.Compression.ZipFile]::OpenRead($packagePath)
try {
    $reader = [IO.StreamReader]::new($archive.GetEntry('AppxManifest.xml').Open())
    try { [xml]$manifest = $reader.ReadToEnd() }
    finally { $reader.Dispose() }
}
finally { $archive.Dispose() }
$identity = $manifest.Package.Identity
if ($identity.ProcessorArchitecture -ne [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()) {
    throw 'Install/launch testing must run on the package target architecture.'
}
if (Get-AppxPackage -Name $identity.Name) {
    throw 'Use an isolated runner without an existing installation; this test does not remove user data.'
}
$certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($certificatePath)
if ($certificate.Subject -ne $identity.Publisher -or
    (Get-AuthenticodeSignature -LiteralPath $packagePath).SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
    throw 'Package publisher, signature, and supplied test certificate must match.'
}

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class SpectroPackageActivation {
    [ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager {
        [PreserveSig]
        int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string appId,
            [MarshalAs(UnmanagedType.LPWStr)] string arguments, uint options, out uint processId);
        [PreserveSig]
        int ActivateForFile([MarshalAs(UnmanagedType.LPWStr)] string appId, IntPtr items,
            [MarshalAs(UnmanagedType.LPWStr)] string verb, out uint processId);
        [PreserveSig]
        int ActivateForProtocol([MarshalAs(UnmanagedType.LPWStr)] string appId, IntPtr items, out uint processId);
    }
    public static uint Activate(string appId) {
        var manager = (IApplicationActivationManager)Activator.CreateInstance(
            Type.GetTypeFromCLSID(new Guid("45ba127d-10a8-46ea-8ab7-56ea9078943c")));
        try {
            uint processId;
            Marshal.ThrowExceptionForHR(manager.ActivateApplication(appId, "", 0, out processId));
            return processId;
        }
        finally { Marshal.FinalReleaseComObject(manager); }
    }
}
'@

$store = [Security.Cryptography.X509Certificates.X509Store]::new('TrustedPeople', 'LocalMachine')
$trustAdded = $false
$installed = $null
$process = $null
try {
    $store.Open('ReadWrite')
    if (-not $store.Certificates.Find('FindByThumbprint', $certificate.Thumbprint, $false).Count) {
        $store.Add($certificate)
        $trustAdded = $true
    }
    if ((Get-AuthenticodeSignature -LiteralPath $packagePath).Status -ne 'Valid') {
        throw 'The MSIX signature is not valid after explicitly trusting its test certificate.'
    }
    Add-AppxPackage -Path $packagePath -ErrorAction Stop
    $installed = Get-AppxPackage -Name $identity.Name
    if (-not $installed -or $installed.IsDevelopmentMode -or
        $installed.Version.ToString() -ne $identity.Version -or $installed.Status -ne 'Ok') {
        throw 'Windows did not install the expected non-development MSIX.'
    }

    $appId = $manifest.Package.Applications.Application.Id
    $processId = [SpectroPackageActivation]::Activate("$($installed.PackageFamilyName)!$appId")
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(60)
    do {
        Start-Sleep -Milliseconds 500
        $process = Get-Process -Id $processId -ErrorAction Stop
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) { break }
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    Start-Sleep -Seconds 5
    $process.Refresh()
    if ($process.HasExited -or $process.MainWindowHandle -eq [IntPtr]::Zero -or
        -not $process.Responding -or $process.MainWindowTitle -ne 'Spectro') {
        throw 'The installed MSIX did not reach a stable, responding Spectro window.'
    }
    [pscustomobject]@{
        installed = $true
        launched = $true
        developmentMode = $false
        version = $installed.Version.ToString()
        architecture = $identity.ProcessorArchitecture
    } | ConvertTo-Json
}
finally {
    try {
        if ($process -and -not $process.HasExited) {
            $null = $process.CloseMainWindow()
            if (-not $process.WaitForExit(10000)) { Stop-Process -Id $process.Id -Force }
        }
        if ($installed) { Remove-AppxPackage -Package $installed.PackageFullName -ErrorAction Stop }
    }
    finally {
        if ($trustAdded) { $store.Remove($certificate) }
        $store.Dispose()
        $certificate.Dispose()
    }
}
