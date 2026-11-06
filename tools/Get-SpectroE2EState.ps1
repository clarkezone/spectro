param(
    [string]$StoryHash,
    [switch]$Library,
    [switch]$Base64,
    [ValidateRange(1, 10000)][int]$StoryLimit = 500
)
$ErrorActionPreference = 'Stop'
if ($StoryHash -and $StoryHash -notmatch '^\d+:[A-Za-z0-9_-]+$') {
    throw 'Invalid fixture story hash.'
}
$package = Get-AppxPackage -Name A3C06F23-C3A9-4304-A172-22F2D2F7A78C
if (-not $package) { throw 'Spectro is not installed for this Windows user.' }
$database = Join-Path $env:LOCALAPPDATA "Packages\$($package.PackageFamilyName)\LocalState\spectro.db"
if (-not (Test-Path -LiteralPath $database)) { throw 'The production-mode test database does not exist.' }
# Windows' SQLite can load outside the protected MSIX installation directory.
if (-not ('SpectroE2ESqlite' -as [type])) { Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class SpectroE2ESqlite {
    [DllImport("winsqlite3", CallingConvention=CallingConvention.Cdecl)]
    public static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out IntPtr db, int flags, IntPtr vfs);
    [DllImport("winsqlite3", CallingConvention=CallingConvention.Cdecl)]
    public static extern int sqlite3_prepare_v2(IntPtr db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, int bytes, out IntPtr statement, IntPtr tail);
    [DllImport("winsqlite3", CallingConvention=CallingConvention.Cdecl)]
    public static extern int sqlite3_step(IntPtr statement);
    [DllImport("winsqlite3", CallingConvention=CallingConvention.Cdecl)]
    public static extern IntPtr sqlite3_column_text(IntPtr statement, int column);
    [DllImport("winsqlite3", CallingConvention=CallingConvention.Cdecl)]
    public static extern int sqlite3_column_bytes(IntPtr statement, int column);
    [DllImport("winsqlite3", CallingConvention=CallingConvention.Cdecl)]
    public static extern int sqlite3_finalize(IntPtr statement);
    [DllImport("winsqlite3", CallingConvention=CallingConvention.Cdecl)]
    public static extern int sqlite3_close(IntPtr db);
}
'@
}
$handle = [IntPtr]::Zero
$statement = [IntPtr]::Zero
try {
    if ([SpectroE2ESqlite]::sqlite3_open_v2($database, [ref]$handle, 1, [IntPtr]::Zero) -ne 0) {
        throw 'Cannot open the test database read-only.'
    }
    $libraryQuery = if ($Library) { @'
,
  'feeds', (SELECT json_group_array(json_object(
    'id', f.id, 'title', f.title,
    'total', (SELECT COUNT(*) FROM story s WHERE s.feed_id=f.id),
    'unread', (SELECT COUNT(*) FROM story s WHERE s.feed_id=f.id AND s.is_read=0),
    'saved', (SELECT COUNT(*) FROM story s WHERE s.feed_id=f.id AND s.is_saved=1)))
    FROM feed f WHERE f.is_active=1),
  'folders', (SELECT json_group_array(json_object(
    'id', f.id, 'title', f.title, 'feedIds',
    (SELECT json_group_array(ff.feed_id) FROM folder_feed ff
     JOIN feed d ON d.id=ff.feed_id WHERE ff.folder_id=f.id AND d.is_active=1)))
    FROM folder f),
  'stories', (SELECT json_group_array(json_object(
    'hash', story_hash, 'title', title, 'feedId', feed_id, 'read', is_read, 'saved', is_saved))
    FROM (SELECT story_hash,title,feed_id,is_read,is_saved FROM story
          ORDER BY published_utc DESC,story_hash LIMIT __STORY_LIMIT__))
'@ } else { '' }
    $libraryQuery = $libraryQuery.Replace('__STORY_LIMIT__', $StoryLimit.ToString([Globalization.CultureInfo]::InvariantCulture))
    $query = @"
SELECT json_object(
  'storyCount', (SELECT COUNT(*) FROM story),
  'unreadCount', (SELECT COUNT(*) FROM story WHERE is_read=0),
  'pendingCount', (SELECT COUNT(*) FROM pending_mutation),
  'uploadedCount', (SELECT COUNT(*) FROM uploaded_mutation),
  'lastSync', (SELECT value FROM sync_checkpoint WHERE name='last-successful-sync'),
  'fixtureRead', (SELECT is_read FROM story WHERE story_hash='$StoryHash'),
  'fixtureSaved', (SELECT is_saved FROM story WHERE story_hash='$StoryHash')
  $libraryQuery
);
"@
    if ([SpectroE2ESqlite]::sqlite3_prepare_v2($handle, $query, -1, [ref]$statement, [IntPtr]::Zero) -ne 0) {
        throw 'The E2E state query could not be prepared.'
    }
    if ([SpectroE2ESqlite]::sqlite3_step($statement) -ne 100) { throw 'The E2E state query returned no row.' }
    $bytes = New-Object byte[] ([SpectroE2ESqlite]::sqlite3_column_bytes($statement, 0))
    [Runtime.InteropServices.Marshal]::Copy(
        [SpectroE2ESqlite]::sqlite3_column_text($statement, 0), $bytes, 0, $bytes.Length)
    if ($Base64) { [Convert]::ToBase64String($bytes) }
    else { [Text.Encoding]::UTF8.GetString($bytes) }
}
finally {
    if ($statement -ne [IntPtr]::Zero) { [void][SpectroE2ESqlite]::sqlite3_finalize($statement) }
    if ($handle -ne [IntPtr]::Zero) { [void][SpectroE2ESqlite]::sqlite3_close($handle) }
}
