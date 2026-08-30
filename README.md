# Velvet Rope 0.3.10

Velvet Rope is a privacy-first FFXIV venue door, VIP, staff, and attendance-management plugin for Dalamud.

Built for venue owners, managers, hosts, greeters, DJs, and staff who need a lightweight way to recognize important guests, manage venue-specific relationships, and track attendance without maintaining a permanent guest list.

---

## Installation

Velvet Rope is distributed through a custom Dalamud plugin repository.

### 1. Add the Velvet Rope repository

In FFXIV, open Dalamud settings:

```text
/xlsettings
```

Go to:

```text
Experimental → Custom Plugin Repositories
```

Add this URL:

```text
https://raw.githubusercontent.com/torenaran/VelvetRope/main/repo.json
```

Save your changes.

### 2. Install Velvet Rope

Open the Dalamud Plugin Installer:

```text
/xlplugins
```

Search for:

```text
Velvet Rope
```

Then click **Install**.

After installation, open Velvet Rope with:

```text
/velvetrope
```

or:

```text
/vr
```

---

## Highlights

- Unlimited venue profiles.
- Venue-specific VIP, staff, DJ, owner, partner, regular, and custom relationships.
- Add Target workflow for quickly adding the player you are currently targeting.
- Player right-click menu integration.
- Nightly, Monthly, Yearly, and Lifetime VIP tiers.
- Optional VIP tiering per relationship.
- Non-tiered staff and venue roles do not expire.
- Native FFXIV nameplate markers.
- Tier-colored VIP markers.
- Neutral category markers for non-tiered staff and venue roles.
- Venue-specific VIP tier benefits.
- Prepared `/tell` benefit messages copied to clipboard.
- Personalized arrival shout variants.
- Silent VIPs for staff-only recognition.
- Automatic expiration and renewal handling for timed VIP tiers.
- Privacy-first attendance counting.
- Aggregate shift reports.
- Hourly new-guest tracking.
- Peak visible guest count.
- Branded `.vrui` UI packs with custom colors, logos, and header artwork.
- Venue/profile import and export.

---

## Venue Profiles

Velvet Rope is designed for people who may work at more than one venue.

Each venue can maintain its own:

- VIP relationships
- staff and role assignments
- VIP tiers
- tier benefits
- arrival shout settings
- branding
- UI pack
- shift history

A person can have completely different roles or VIP status at different venues.

---

## VIP Tiers

Velvet Rope supports four optional VIP durations:

| Tier | Duration |
| --- | --- |
| Nightly | Until the next local calendar day |
| Monthly | One calendar month |
| Yearly | One calendar year |
| Lifetime | Never expires |

VIP tiering can be disabled for individual relationships.

For example, a staff member can simply be marked as **Staff** without having a Nightly, Monthly, Yearly, or Lifetime VIP status.

---

## Nameplate Markers

Velvet Rope can display local-only markers on recognized players' FFXIV nameplates.

VIP relationships use tier-colored markers:

```text
◆ Nightly
◆ Monthly
◆ Yearly
★ Lifetime
```

The marker colors distinguish the VIP tier.

Non-tiered venue roles use neutral-colored category glyphs instead.

For example, Staff, DJs, Venue Owners, Partners, Regulars, and other non-tiered relationships can be recognized without assigning them a VIP expiration period.

These markers are only visible to the local Velvet Rope user and do not modify what other players see.

---

## UI Packs

Velvet Rope supports importable `.vrui` UI packs.

UI packs can customize:

- colors
- panel styling
- sidebar appearance
- logos
- header artwork
- branding text
- layout presentation

To import one, open Velvet Rope and use:

```text
Settings → Import UI Pack
```

UI packs are cosmetic and do not contain guest or VIP data.

---

## Commands

| Command | Description |
| --- | --- |
| `/velvetrope` | Open Velvet Rope |
| `/vr` | Open Velvet Rope |
| `/vr start` | Start a shift for the selected venue |
| `/vr end` | End the active shift and save its aggregate report |
| `/vr reset` | Reseed VIP presence without ending the shift |

---

## Privacy

Velvet Rope is designed around a simple principle:

> **It remembers the people you explicitly tell it to remember. Everyone else is a number.**

Velvet Rope only persists identities that the user explicitly adds or imports.

Ordinary guests are counted during an active shift using randomized, salted identifiers held only in memory.

Those temporary identifiers are discarded when the shift ends or the plugin reloads.

Saved shift reports contain aggregate information such as:

- unique guest count
- VIP arrivals
- peak visible guests
- hourly new guests

Velvet Rope does **not** save a general guest attendance list.

VIPs, staff, DJs, owners, partners, regulars, and other explicitly created relationships are intentionally saved.

See `PRIVACY.md` for more information.

---

## Building from Source

Velvet Rope currently targets:

```text
.NET 10
Dalamud API 15
```

### Requirements

- Visual Studio 2022 or newer
- .NET 10 SDK
- Dalamud development environment

Clone the repository:

```bash
git clone https://github.com/torenaran/VelvetRope.git
```

Open:

```text
VelvetRope.slnx
```

in Visual Studio.

For local testing, select:

```text
Debug
```

For a distributable build, select:

```text
Release
```

Then use:

```text
Build → Build Solution
```

Release builds use the Dalamud SDK packager to create the plugin distribution archive.

---

## Development Builds

For local development, the compiled plugin can be loaded through Dalamud's Dev Plugin system.

Open:

```text
/xlsettings
```

Then use Dalamud's development plugin settings to load the compiled `VelvetRope.dll`.

Development builds are intended for testing only.

Normal users should install Velvet Rope through the custom repository:

```text
https://raw.githubusercontent.com/torenaran/VelvetRope/main/repo.json
```

---

## Repository JSON

The `repo.json` file at the root of this repository should contain:

```json
[
  {
    "Author": "Tori",
    "Name": "Velvet Rope",
    "Punchline": "Privacy-first venue, VIP, staff, and attendance management.",
    "Description": "Velvet Rope is a privacy-first FFXIV venue-management plugin for Dalamud. Manage venue-specific VIPs, staff, DJs, owners, partners and regulars; recognize important guests with local nameplate markers; prepare tier benefits and arrival shoutouts; track anonymous aggregate attendance; and personalize the interface with importable .vrui UI packs.",
    "InternalName": "VelvetRope",
    "AssemblyVersion": "0.3.10.0",
    "TestingAssemblyVersion": "0.3.10.0",
    "RepoUrl": "https://github.com/torenaran/VelvetRope",
    "ApplicableVersion": "any",
    "DalamudApiLevel": 15,
    "IsHide": false,
    "IsTestingExclusive": false,
    "DownloadLinkInstall": "https://github.com/torenaran/VelvetRope/releases/download/v0.3.10/latest.zip",
    "DownloadLinkUpdate": "https://github.com/torenaran/VelvetRope/releases/download/v0.3.10/latest.zip",
    "DownloadLinkTesting": "https://github.com/torenaran/VelvetRope/releases/download/v0.3.10/latest.zip"
  }
]
```

The public repository URL users add to Dalamud is:

```text
https://raw.githubusercontent.com/torenaran/VelvetRope/main/repo.json
```

---

## License

Velvet Rope is licensed under the MIT License.

See `LICENSE` for details.
