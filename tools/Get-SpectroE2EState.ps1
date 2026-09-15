param([string]$StoryHash)
$ErrorActionPreference = 'Stop'
if ($StoryHash -and $StoryHash -notmatch '^\d+:[A-Za-z0-9_-]+$') {
    throw 'Invalid fixture story hash.'
}
$package = Get-AppxPackage -Name A3C06F23-C3A9-4304-A172-22F2D2F7A78C
if (-not $package) { throw 'Spectro is not installed for this Windows user.' }
$database = Join-Path $env:LOCALAPPDATA "Packages\$($package.PackageFamilyName)\LocalState\spectro.db"
if (-not (Test-Path -LiteralPath $database)) { throw 'The production-mode test database does not exist.' }
$native = Get-ChildItem -LiteralPath $package.InstallLocation -Filter e_sqlite3.dll -Recurse | Select-Object -First 1
if (-not $native) { throw 'The app SQLite library was not found.' }
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class SpectroE2ESqlite {
    [DllImport("kernel32", CharSet=CharSet.Unicode)]
    public static extern IntPtr LoadLibrary(string path);
    [DllImport("e_sqlite3", CallingConvention=CallingConvention.Cdecl)]
    public static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out IntPtr db, int flags, IntPtr vfs);
    [DllImport("e_sqlite3", CallingConvention=CallingConvention.Cdecl)]
    public static extern int sqlite3_prepare_v2(IntPtr db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, int bytes, out IntPtr statement, IntPtr tail);
    [DllImport("e_sqlite3", CallingConvention=CallingConvention.Cdecl)]
    public static extern int sqlite3_step(IntPtr statement);
    [DllImport("e_sqlite3", CallingConvention=CallingConvention.Cdecl)]
    public static extern IntPtr sqlite3_column_text(IntPtr statement, int column);
    [DllImport("e_sqlite3", CallingConvention=CallingConvention.Cdecl)]
    public static extern int sqlite3_finalize(IntPtr statement);
    [DllImport("e_sqlite3", CallingConvention=CallingConvention.Cdecl)]
    public static extern int sqlite3_close(IntPtr db);
}
'@
if ([SpectroE2ESqlite]::LoadLibrary($native.FullName) -eq [IntPtr]::Zero) { throw 'SQLite could not load.' }
$handle = [IntPtr]::Zero
$statement = [IntPtr]::Zero
try {
    if ([SpectroE2ESqlite]::sqlite3_open_v2($database, [ref]$handle, 1, [IntPtr]::Zero) -ne 0) {
        throw 'Cannot open the test database read-only.'
    }
    $query = @"
SELECT json_object(
  'storyCount', (SELECT COUNT(*) FROM story),
  'unreadCount', (SELECT COUNT(*) FROM story WHERE is_read=0),
  'pendingCount', (SELECT COUNT(*) FROM pending_mutation),
  'uploadedCount', (SELECT COUNT(*) FROM uploaded_mutation),
  'lastSync', (SELECT value FROM sync_checkpoint WHERE name='last-successful-sync'),
  'fixtureRead', (SELECT is_read FROM story WHERE story_hash='$StoryHash'),
  'fixtureSaved', (SELECT is_saved FROM story WHERE story_hash='$StoryHash')
);
"@
    if ([SpectroE2ESqlite]::sqlite3_prepare_v2($handle, $query, -1, [ref]$statement, [IntPtr]::Zero) -ne 0) {
        throw 'The E2E state query could not be prepared.'
    }
    if ([SpectroE2ESqlite]::sqlite3_step($statement) -ne 100) { throw 'The E2E state query returned no row.' }
    [Runtime.InteropServices.Marshal]::PtrToStringAnsi([SpectroE2ESqlite]::sqlite3_column_text($statement, 0))
}
finally {
    if ($statement -ne [IntPtr]::Zero) { [void][SpectroE2ESqlite]::sqlite3_finalize($statement) }
    if ($handle -ne [IntPtr]::Zero) { [void][SpectroE2ESqlite]::sqlite3_close($handle) }
}
