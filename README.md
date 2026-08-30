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

## License

Velvet Rope is licensed under the MIT License.

See `LICENSE` for details.
