# Security Policy

## Supported versions

OMP is pre-1.0 and under active development. Only the latest release gets security fixes —
please update before reporting an issue if you're not already on the newest version.

## Reporting a vulnerability

Please **do not** open a public GitHub Issue for security vulnerabilities. Report privately instead:

- Preferred: use GitHub's [private vulnerability reporting](../../security/advisories/new) for
  this repository.
- Alternative: email RsTGear@gmail.com.

Include what you can: affected version/platform, steps to reproduce, and (if applicable) a sample
file that triggers the issue.

This is a solo-maintained project, so there's no formal SLA, but reports will be acknowledged and
addressed as quickly as possible. Please allow time for a fix to ship before any public disclosure.

## Scope

OMP bundles FFmpeg to decode media files, and those files can come from anywhere the user chooses
to open — including untrusted sources. Memory-safety bugs in a codec's parsing of a malformed or
maliciously crafted file (crashes, or worse) are valid, in-scope reports, not just issues in OMP's
own C# code. If you find that a specific file can crash or otherwise misbehave the app, that's
useful to know even if the underlying bug turns out to live in FFmpeg itself rather than in OMP —
we may need to update the bundled build (or, on macOS, note a required Homebrew `ffmpeg@7` update).
