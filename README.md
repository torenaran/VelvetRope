# Velvet Rope 0.3.10

Velvet Rope is a privacy-first FFXIV venue door, VIP, and staff-management plugin for Dalamud.

## Install from the custom repository

```text
https://raw.githubusercontent.com/torenaran/VelvetRope/main/repo.json
```

Then open `/xlplugins`, search for **Velvet Rope**, and install it.

## Highlights

- Unlimited venue profiles with venue-specific VIP/staff relationships.
- Add Target and player right-click workflows.
- Nightly, Monthly, Yearly, and Lifetime VIP tiers.
- Tier benefits and prepared `/tell` copy.
- Native nameplate markers: tier-colored diamonds/stars for VIPs and neutral category glyphs for non-tiered staff/roles.
- Optional VIP tiering per relationship, so staff/DJs/owners can use category markers without expiration.
- Personalized arrival shout variants and Silent VIPs.
- Privacy-first anonymous attendance counting and aggregate shift reports.
- Branded `.vrui` UI packs with logos and header artwork.
- Venue/profile import and export.

## Commands

- `/velvetrope` or `/vr` — open Velvet Rope
- `/vr start` — start the selected venue shift
- `/vr end` — end the active shift and save its aggregate report
- `/vr reset` — reseed VIP presence without ending the shift

## Privacy

Velvet Rope only persists identities that the user explicitly adds/imports. Ordinary guests are deduplicated during the current shift with randomized in-memory identifiers; saved reports contain aggregate attendance totals only. See `PRIVACY.md`.

## Building

This repository follows the current `goatcorp/SamplePlugin` layout and targets `Dalamud.NET.Sdk/15.0.0` / .NET 10.

Open `VelvetRope.slnx` in Visual Studio and build **Release**. The Dalamud SDK/DalamudPackager should produce a distribution folder under:

```text
VelvetRope\bin\x64\Release\VelvetRope\
```

Upload that folder's `latest.zip` to the matching GitHub release.

For first-time publishing, read **START-HERE.md**.

## License

MIT
