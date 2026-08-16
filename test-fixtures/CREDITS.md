# Test fixture provenance

Every file under `test-fixtures/` is a re-encode of one of the two source works below, each
clearly licensed for redistribution and derivative works (transcoding/remuxing counts as a
derivative). Re-encoding does not change or waive the original license — the terms below still
apply to every derived file.

## Video (`video/sample.mp4`, `video/sample.mkv`, `video/sample.mov`, `video/sample.avi`, `video/sample.webm`, `video/sample.flv`,
`sample.wmv`)

- **Source**: *Big Buck Bunny* (2008), Blender Foundation — https://www.bigbuckbunny.org/
- **License**: Creative Commons Attribution 3.0 — https://creativecommons.org/licenses/by/3.0/
- **Obtained from**: https://archive.org/details/BigBuckBunny_328 (`BigBuckBunny_512kb.mp4`,
  official Blender Foundation upload, `licenseurl: creativecommons.org/licenses/by/3.0/us/`)
- **Derivation**: 12-second clip (00:00:40–00:00:52), re-encoded per target container/codec.
  Attribution: "Big Buck Bunny" © 2008, Blender Foundation, www.bigbuckbunny.org, CC BY 3.0.

## Audio (`audio/sample.mp3`, `audio/sample.flac`, `audio/sample.ogg`, `audio/sample.m4a`,
`audio/sample.aac`, `audio/sample.wav`)

- **Source**: "Alien Spaceship Atmosphere" by Kevin MacLeod — https://freepd.com/horror.php
- **License**: CC0 1.0 Universal (public domain dedication) —
  https://creativecommons.org/publicdomain/zero/1.0/
- **Obtained from**:
  https://commons.wikimedia.org/wiki/File:Kevin_MacLeod_-_Alien_Spaceship_Atmosphere_(cc0).ogg
- **Derivation**: 20-second clip (00:00:03–00:00:23), re-encoded per target format. No
  attribution required (CC0), credited here anyway for traceability.

## Regenerating

Both derivations were done with ffmpeg from the original downloads; re-running them from the
same source timestamps reproduces byte-similar output. No other files should be added to this
directory without recording their own source/license here first.
