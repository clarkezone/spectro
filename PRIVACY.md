# Spectro privacy policy

Last updated: October 14, 2026

Spectro is a Windows client for the NewsBlur service. Spectro does not sell
personal information and does not include behavioral advertising or behavioral
analytics.

## Information Spectro processes

When you sign in, Spectro sends the NewsBlur username and password you provide
directly to NewsBlur over HTTPS. Spectro stores the resulting session credential
in Windows-protected credential storage. The credential is not written to
application logs or the local content database.
The database stores the account identifier and a one-way fingerprint of the
saved session to prevent another account from using the cached library or its
queued changes. The fingerprint cannot be used as the session cookie.

Spectro downloads account, feed, folder, story, read, and saved-state information
from NewsBlur. This content is stored locally in the app's private Windows
application-data folder so it remains available offline. Local actions such as
marking a story read or saved are retained until they can be synchronized with
NewsBlur.

When article-preview downloads are enabled, Spectro also requests preview images
from the HTTPS image hosts specified by your feeds. These hosts can receive your
IP address and the image request, but not your NewsBlur cookies or credentials.
You can disable new preview downloads in Settings. Downloaded images are cached
locally (up to 50 MiB) for offline viewing. Opening a cached article does not
load remote images, scripts, frames, or tracking resources in the reader.

NewsBlur's handling of information is governed by its own privacy policy and
terms. Spectro is an independent client and is not operated by NewsBlur.

## Diagnostics

Spectro does not transmit diagnostic or usage data by default. If diagnostic
export is enabled in a future release, it will be initiated by the user and will
exclude credentials, authentication cookies, and article response bodies.

## Data removal

Signing out removes the stored NewsBlur credential and account-specific local
content, pending local mutations, cached preview images, and embedded
article-reader browsing data.
This includes database backups preserved during recovery or an account change.
If cleanup fails, Spectro closes the reader and requires another cleanup attempt
before allowing sign-in.

When an existing library belongs to another account or lacks ownership metadata,
Spectro preserves it as a local database backup and creates a separate empty
library for the verified account. Explicit recovery of an unreadable database
also preserves a backup. Backups can contain article content and unsynced changes;
Spectro does not upload their changes automatically. They remain in the app's
private data folder until sign-out or removal of the app's local data.

Article links are opened by the Windows default browser rather than inside
Spectro. Uninstalling Spectro through Windows removes the app's private local
data according to Windows application-data behavior.

## Contact

Questions and support requests can be filed at
<https://github.com/clarkezone/spectro/issues>.
