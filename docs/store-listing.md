# Microsoft Store listing

**Status: draft, not submitted.** The current CI artifacts are sideload test
builds. Partner Center identity, submission version, and certification results
must be confirmed before creating the Store release candidate.

## Product name

Spectro

## Short description

A calm, offline-first NewsBlur reader built for Windows.

## Description

Spectro is a focused Windows client for NewsBlur that keeps reading fast,
comfortable, and available offline.

- Read synchronized articles even when the network is unavailable.
- Browse all stories, unread stories, saved stories, folders, and individual
  feeds.
- Mark stories read, unread, saved, or unsaved with durable local-first state.
- Enjoy a responsive Windows interface designed for keyboard, mouse, touch,
  light and dark themes, and accessible navigation.
- Protect your saved NewsBlur session with Windows Credential Locker.

Spectro requires an existing NewsBlur account. Account creation, subscription
management, and NewsBlur service billing are handled by NewsBlur.

## Store metadata

- Category: News & weather
- Pricing: Free (proposed; confirm in the submission)
- Age rating: Not assigned; complete the Partner Center age-rating questionnaire
- Support URL: <https://github.com/clarkezone/spectro/issues>
- Privacy URL: <https://github.com/clarkezone/spectro/blob/master/PRIVACY.md>
- Website: <https://github.com/clarkezone/spectro>

The proposed privacy URL above returned HTTP 404 during the 2026-09-17
publishing-readiness check. Publish the policy at a stable public URL and verify
it without signing in before entering it in Partner Center. Do not submit the
draft URL while it is unavailable.

## Release notes

Initial release:

- Native Windows NewsBlur reading experience
- Offline feed and article cache
- Read and saved-state synchronization
- Responsive article reader
- x64 and ARM64 support

## Values required from Partner Center

First establish whether this is a new listing or an update to an existing
Spectro listing. For an update, use the existing product rather than reserving a
replacement: changing package identity can break the existing upgrade and
application-data path.

Copy these values from **Product management > Product identity**:

| Value | Use |
| --- | --- |
| Package/Identity/Name | Exact package name in the manifest |
| Package/Identity/Publisher | Exact publisher subject, including `CN=` |
| Package/Properties/PublisherDisplayName | Exact publisher display name |
| Highest previously uploaded package version | Select an appropriate newer submission version |

The current manifest still contains a temporary package name and
`Publisher="CN=AppPublisher"`. Do not invent or derive replacement values from
the product's display name.

Use a four-part Store submission version with the final component set to zero.
The CI sideload version, such as `1.0.52.1`, is not the Store versioning scheme.
Choose the submission version after checking any existing uploads.

## Submission preparation

1. Apply the confirmed Store identity and version to the release packages.
   Keep the existing self-signed sideload artifacts separate from Store output.
   Microsoft re-signs accepted MSIX packages; a purchased signing certificate
   is not required for an MSIX Store submission.
2. Create the x64 and ARM64 submission packages, preferably a bundle/upload
   artifact for both architectures. Partner Center supports MSIX packages and
   bundles; an upload file can also include public symbols for crash analysis.
3. Run Windows App Certification Kit against the final release candidate and
   complete the outstanding device, installation, upgrade, and privacy checks
   in [the release checklist](release-checklist.md).
4. Finalize screenshots, the public privacy URL, support information, age
   ratings, markets, and pricing. Provide a working dedicated NewsBlur test
   account and sign-in instructions directly in Partner Center's certification
   notes, never in the repository or public PR.
5. Review the declared `runFullTrust` capability and explain the packaged WinUI
   desktop application where Partner Center requests a justification.
   Explicitly choose the audience and publishing hold before submitting:
   Partner Center defaults to public availability and release as soon as
   possible. Preparing or certifying a build is not approval to publish it.

Official references:

- [MSIX package requirements, signing, and versioning](https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/app-package-requirements)
- [Partner Center submission checklist](https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/create-app-submission)
