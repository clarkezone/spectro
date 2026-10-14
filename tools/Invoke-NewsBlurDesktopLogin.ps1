[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$VMName,
    [Parameter(Mandatory)][string]$WindowHandle,
    [Parameter(Mandatory)][string]$HyperloopPath,
    [string]$VMUser = 'AdminUser',
    [ValidateSet('Button', 'UsernameEnter', 'PasswordEnter')][string]$SubmitMode = 'Button'
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($env:NEWSBLUR_TEST_USERNAME) -or
    [string]::IsNullOrEmpty($env:NEWSBLUR_TEST_PASSWORD)) {
    throw 'NEWSBLUR_TEST_USERNAME and NEWSBLUR_TEST_PASSWORD must be set.'
}
if ($WindowHandle -notmatch '^0x[0-9a-fA-F]+$') {
    throw 'WindowHandle must be a hexadecimal HWND.'
}
Import-Module IXPTools
$vmPassword = Get-IxpDeviceLocalAdminPassword -ForDevice $VMName -AsSecureString
$vmCredential = [pscredential]::new($VMUser, $vmPassword)
$testCredential = [pscredential]::new(
    $env:NEWSBLUR_TEST_USERNAME,
    (ConvertTo-SecureString $env:NEWSBLUR_TEST_PASSWORD -AsPlainText -Force))
$session = New-PSSession -VMName $VMName -Credential $vmCredential
$pipeName = 'SpectroE2E-' + [guid]::NewGuid().ToString('N')
$job = $null
try {
    # PSDirect carries the credential; the interactive UI worker receives it only in memory.
    $job = Invoke-Command -Session $session -AsJob -ArgumentList $pipeName, $testCredential -ScriptBlock {
        param($name, $credential)
        $security = [IO.Pipes.PipeSecurity]::new()
        $security.SetAccessRuleProtection($true, $false)
        $security.AddAccessRule([IO.Pipes.PipeAccessRule]::new(
            [Security.Principal.WindowsIdentity]::GetCurrent().User,
            [IO.Pipes.PipeAccessRights]::FullControl,
            [Security.AccessControl.AccessControlType]::Allow))
        $pipe = [IO.Pipes.NamedPipeServerStream]::new(
            $name, [IO.Pipes.PipeDirection]::Out, 1,
            [IO.Pipes.PipeTransmissionMode]::Byte, [IO.Pipes.PipeOptions]::Asynchronous,
            0, 0, $security)
        try {
            if (-not $pipe.WaitForConnectionAsync().Wait(30000)) {
                throw 'Credential pipe connection timed out.'
            }
            $writer = [IO.StreamWriter]::new($pipe)
            try {
                $writer.WriteLine((@{
                    username = $credential.UserName
                    password = $credential.GetNetworkCredential().Password
                } | ConvertTo-Json -Compress))
                $writer.Flush()
            }
            finally { $writer.Dispose() }
            'Credential delivered in memory.'
        }
        finally { $pipe.Dispose() }
    }
    $script = @'
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$root = [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]::new([Convert]::ToInt64('__HWND__',16)))
if ((Get-Process -Id $root.Current.ProcessId).ProcessName -ne 'Spectro.App') {
    throw 'The supplied window does not belong to Spectro.'
}
function Find-Control([string]$id) {
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    $element = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
    if ($null -eq $element) { throw 'Required Spectro login control was not found.' }
    return $element
}
$username = Find-Control 'Username'
$password = Find-Control 'Password'
$submit = Find-Control 'SignIn'
$pipe = [IO.Pipes.NamedPipeClientStream]::new('.', '__PIPE__', [IO.Pipes.PipeDirection]::In)
try {
    $pipe.Connect(20000)
    $reader = [IO.StreamReader]::new($pipe)
    try { $credential = $reader.ReadLine() | ConvertFrom-Json }
    finally { $reader.Dispose() }
    ([System.Windows.Automation.ValuePattern]$username.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).SetValue($credential.username)
    ([System.Windows.Automation.ValuePattern]$password.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).SetValue($credential.password)
    Start-Sleep -Milliseconds 500
    $enteredUsername = ([System.Windows.Automation.ValuePattern]$username.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).Current.Value
    if ($enteredUsername -ne $credential.username) { throw 'Username entry did not reach the control.' }
    if ('__SUBMIT__' -eq 'UsernameEnter') { $username.SetFocus() }
    elseif ('__SUBMIT__' -eq 'PasswordEnter') { $password.SetFocus() }
    '{"credentialsEntered":true}'
}
catch { throw 'Desktop credential entry failed; sensitive diagnostics suppressed.' }
finally {
    $credential = $null
    $pipe.Dispose()
}
'@
    $script = $script.Replace('__HWND__', $WindowHandle.Substring(2)).Replace('__PIPE__', $pipeName).
        Replace('__SUBMIT__', $SubmitMode)
    & $HyperloopPath context $VMName --title 'Signing into Spectro with the dedicated test account'
    $result = & $HyperloopPath shell $VMName --name 'Enter test credentials through an in-memory pipe' --script $script --timeout 30 |
        ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or -not $result.success) { throw 'Desktop login worker failed.' }
    if (-not ($result.stdout | ConvertFrom-Json).credentialsEntered) { throw 'Desktop credentials were not entered.' }
    $null = Wait-Job $job -Timeout 10
    if ($job.State -ne 'Completed') { throw 'Credential delivery did not complete.' }
    Receive-Job $job
    if ($SubmitMode -eq 'Button') {
        $submitted = & $HyperloopPath uia-invoke $VMName --hwnd $WindowHandle --id SignIn --pattern invoke |
            ConvertFrom-Json
        if ($LASTEXITCODE -ne 0 -or -not $submitted.invoked) { throw 'Desktop sign-in submission failed.' }
    }
    else {
        & $HyperloopPath hotkey $VMName --hwnd $WindowHandle --combo Enter
        if ($LASTEXITCODE -ne 0) { throw 'Desktop Enter submission failed.' }
    }
    '{"submitted":true}'
}
finally {
    if ($null -ne $job) {
        if ($job.State -eq 'Running') { Stop-Job $job }
        Remove-Job $job -Force
    }
    Remove-PSSession $session
}
