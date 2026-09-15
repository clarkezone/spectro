# Installing Spectro test builds

Download the `spectro-win-x64-msix` or `spectro-win-arm64-msix` artifact from
the PR's GitHub Actions run and **extract the ZIP first**. Select the architecture
that matches your Windows device. Each artifact contains a signed `.msix`, its
public `.cer` test certificate, and these instructions.

Requirements: Windows 11 and Microsoft Edge WebView2 Runtime. WebView2 normally
ships with Windows 11; if it has been removed, install the Evergreen Runtime
from <https://developer.microsoft.com/microsoft-edge/webview2/>.
The MSIX bundles the Windows App SDK and Native AOT application, so a separate
.NET or Windows App Runtime installation is not required.

## Trust and install

These are **self-signed test builds**, not Store packages or production-signed
releases. Trust only certificates downloaded from a CI run you trust. A new
test certificate is generated for each architecture/build; it expires after
90 days. The private signing key is never included in the artifact.

In an **Administrator PowerShell** window, change to the extracted folder and
import its public certificate into **Trusted People**, not Trusted Root:

```powershell
$certificate = Get-ChildItem .\Spectro-*.cer
if ($certificate.Count -ne 1) { throw 'Expected exactly one test certificate.' }
Import-Certificate -FilePath $certificate.FullName -CertStoreLocation Cert:\LocalMachine\TrustedPeople
```

Then double-click the `.msix` and choose **Install**, or run this in your normal
PowerShell window:

```powershell
$package = Get-ChildItem .\Spectro-*.msix
if ($package.Count -ne 1) { throw 'Expected exactly one MSIX package.' }
Add-AppxPackage -Path $package.FullName
```

Launch **Spectro from Start**. Do not run a loose `Spectro.App.exe`: this is a
packaged app and requires package identity. Real NewsBlur login is required;
Release packages do not contain the sample library or test credentials.

## Existing development registrations

A signed MSIX cannot replace a loose development registration with the same
identity (`0x80073CFB`). This is distinct from upgrading an installed MSIX.
First close Spectro and ensure synchronization has completed and all local
read/save changes have uploaded. Then unregister only the development package
while preserving its data:

```powershell
$development = Get-AppxPackage -Name A3C06F23-C3A9-4304-A172-22F2D2F7A78C |
    Where-Object IsDevelopmentMode
$development | ForEach-Object {
    Remove-AppxPackage -Package $_.PackageFullName -PreserveApplicationData
}
```

Retry the MSIX installation. Do not uninstall or reset an account with pending
offline changes. Builds use increasing CI package versions; installing an older
build over a newer one is intentionally not automated.

## Remove test trust

Uninstall Spectro through Windows Settings when finished. To remove the
specific test certificate, use Administrator PowerShell in the extracted
artifact folder:

```powershell
$certificate = Get-ChildItem .\Spectro-*.cer
if ($certificate.Count -ne 1) { throw 'Expected exactly one test certificate.' }
$thumbprint = ([Security.Cryptography.X509Certificates.X509Certificate2]::new(
    $certificate.FullName)).Thumbprint
Remove-Item -LiteralPath "Cert:\LocalMachine\TrustedPeople\$thumbprint"
```

Removing trust does not uninstall the app. Keep the public certificate if you
need to reinstall that build later.

## Build locally

Follow the `winui-packaging` playbook in
<https://github.com/microsoft/win-dev-skills>. With .NET, WinApp CLI 0.6.1, and the
target C++ toolchain available, publish Release from the appropriate Native
Tools command prompt, then package:

```powershell
dotnet publish src\Spectro.App\Spectro.App.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained true
.\tools\New-SpectroMsix.ps1 -Runtime win-x64 -OutputDirectory .\artifacts\msix\win-x64 -PackageVersion 1.0.1.0
```

Use `ARM64`/`win-arm64` for the ARM64 build. The project sets
`WindowsAppSDKSelfContained` for Release, bundling the exact NuGet runtime
version before `winapp package --cert` signs the Native AOT layout.
This is the .NET SDK-managed equivalent of the playbook's
`winapp package --self-contained`; it does not require a second `.winapp`
SDK workspace.

The packaging script resolves the generated manifest and publish paths from
MSBuild, includes compiled XAML and production artwork, validates the package
layout, and deletes its temporary private signing key in `finally`. It never
installs a certificate on the developer's machine. Existing package outputs
are not overwritten; use another version or output directory.

CI uses `microsoft/setup-WinAppCli`, produces both architectures, and runs
`tools\Test-SpectroMsix.ps1` on the x64 runner. That script is for an **isolated,
elevated test machine**: it refuses an existing Spectro installation, temporarily
trusts the supplied certificate, installs and activates the signed package,
checks for a stable app window, then removes the test installation and any
certificate trust it added. ARM64 installation/launch still requires an ARM64
device; cross-compilation and package creation alone are not that validation.
