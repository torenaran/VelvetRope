# Velvet Rope Changelog

## 0.3.10 - Optional Tiering & Role Markers

- Added a per-venue-relationship **Use VIP tiering** checkbox.
- Tiering can be disabled for Staff, DJs, Venue Owners, Partners, Regulars, or custom categories.
- Role-only relationships do not expire and do not show Nightly/Monthly/Yearly/Lifetime controls.
- Role-only nameplates use the category glyph in a neutral color; tiered VIP markers keep their tier colors.
- Right-click menus show the category/role instead of a VIP tier when tiering is disabled.
- Add Target, manual add, and reuse-existing-person workflows all support tiering on/off.

## 0.3.9 - Diamond & Lifetime Star Markers

- Replaced the unsupported crown text glyph that could render as an equals-like symbol on FFXIV nameplates.
- Nightly, Monthly, and Yearly VIPs now use a diamond marker (◆) with their existing black/charcoal, copper, and silver tier colors.
- Lifetime VIPs now use a distinct gold star marker (★).
- Added migration cleanup for 0.3.8 crown-text markers so old symbols do not remain beside the new marker.
- Updated the in-world tools UI and preview action to describe the new marker system.

## 0.3.8 - Tier-Colored Native Crowns

- Fixed VIP crowns always appearing gold regardless of Nightly / Monthly / Yearly / Lifetime status.
- Replaced the baked-gold Mentor bitmap with a tintable crown text glyph prepended to the native FFXIV player-name text.
- Tier colors remain Nightly black/charcoal, Monthly copper, Yearly silver, and Lifetime gold.
- Added cleanup for legacy 0.3.7 bitmap crowns during nameplate redraws.


## 0.3.7 - Native Nameplate Crowns

- Replaced the screen-space ImGui crown overlay with Dalamud's native FFXIV nameplate API.
- VIP crowns now live in the nameplate status area, so they follow the actual nameplate and coexist with Honorific/title changes.
- Preserves existing game status icons and other nameplate-prefix content instead of replacing it.
- Keeps tier colors: Nightly black, Monthly copper, Yearly silver, Lifetime gold.
- The crown preview now requests a native nameplate redraw and still ignores shift/VIP rules for testing.
- Crown size now follows the player's FFXIV nameplate scale; Velvet Rope retains only the display-distance control.
- Retains the 0.3.6 duplicate context-menu guard and visible-VIP diagnostics.

## 0.3.6 - Crown Diagnostics & Context Menu Fix

- Moves VIP crown overlays to the foreground draw layer so they remain visible above the game HUD/nameplates and plugin windows.
- Adds **Preview Gold Crown on Target** for a 10-second rendering/placement test independent of shift and VIP matching.
- Shows the current number of visible VIPs matched for crown display in Settings.
- Suppresses duplicate right-click menu injection when Dalamud reports the same menu open more than once.
- Slightly raises the crown anchor for cleaner nameplate placement.

# Changelog

## 0.3.5 - Divider Compatibility Fix

- Fixed the Control Desk resize divider failing to compile on the current ImGui/Dalamud bindings because `ImGuiMouseCursor.ResizeEW` is unavailable.
- The divider remains draggable; only the unsupported cosmetic mouse-cursor override was removed.

## 0.3.4 - Responsive Control Desk

- Redesigned the Control Desk sidebar so navigation remains the focus as Velvet Rope grows.
- Added a draggable vertical resize handle between the sidebar and main workspace.
- Expanded the supported sidebar width range to 220–420 px and raised the default to 232 px.
- The sidebar now responsively preserves usable main-workspace width on smaller plugin windows.
- Consolidated the current venue, shift state, and VIP tier reference into one compact section.
- Simplified UI-pack help to a compact hover beneath the import button.
- Removed the duplicate privacy paragraph from the sidebar; the persistent privacy footer remains on every page.
- Added a Reset Width action under Settings → Appearance.

## 0.3.3 - In-World VIP Tools

- Added local tier crowns above visible VIPs during active shifts.
  - Nightly: black crown with a light outline.
  - Monthly: copper crown.
  - Yearly: silver crown.
  - Lifetime: gold crown.
- Added Settings controls for crown visibility, size, and draw distance.
- Added player right-click context-menu integration.
  - Existing VIPs show `VIP Tier: <tier>` with Tier Benefits, Copy Benefits Tell, and View in Velvet Rope.
  - Non-VIPs show `Velvet Rope: Add VIP...` and open the existing add/update popup.
- Kept the VIP Directory Add Target workflow unchanged.
- Bumped configuration schema to 8 for in-world tool preferences.

# Velvet Rope 0.3.2 — VIP Tier Quick Reference

- Added an always-available **VIP Tier Reference** to the Control Desk for the currently selected venue.
- Shows how many of the four venue-specific benefit tiers are configured and opens the existing tier reference from any tab.
- Renamed the VIP Directory action from **VIP Benefits** to **Tier Reference** for clearer staff-facing language.
- Added a **view benefits** hover beside each VIP's current status so staff can instantly see what that member's tier includes.
- Updated the tier popup title and copy to emphasize its use as a quick staff reference.
- Bumped Velvet Rope to 0.3.2.

# Velvet Rope 0.3.1 — Free Release Candidate

- Velvet Rope is now presented as one fully free edition; removed the Free/Pro preview and product-tier language from the UI.
- Removed the three-venue cap. Venue creation and venue-pack import now support unlimited profiles.
- Removed unused licensing/product-tier scaffolding so the release source matches the product users actually receive.
- Preserved Silent VIPs, Add Target, tier benefits, privacy-first reports, UI packs, and all existing 0.3.0 functionality.
- Updated version labels and build scripts to 0.3.1.

# Velvet Rope 0.3.0 — Free Baseline

- Established the first explicit Velvet Rope Free product baseline.
- Added centralized `FeatureAccess` / product-tier scaffolding for future Pro and Venue editions.
- Free supports up to 3 venue profiles; existing profiles are not deleted if a tester already has more.
- Venue-pack import respects the Free venue-profile cap before importing any data.
- Added a Settings plan overview showing what Free includes and which management features are reserved for Pro.
- Added a first-run Quick Start card when the selected venue has no VIPs yet.
- Added Silent VIPs: staff still receive the arrival alert and the VIP still counts in shift metrics, but Velvet Rope does not prepare a public `/sh` line.
- Updated the arrival popup and dashboard to clearly distinguish Silent VIP notices from public-announcement arrivals.
- Preserved the existing privacy-first reporting, target quick-add, tier benefits, UI packs, and venue pack workflows as Free features.

# Changelog

## 0.2.11 — VIP Tier Benefits

- Added venue-specific benefit descriptions for Nightly, Monthly, Yearly, and Lifetime VIP statuses.
- Added **Edit VIP Benefits** to venue profiles.
- Added a **VIP Benefits** showcase from the VIP Directory.
- Added **Copy Tell to Target** to prepare tier-benefit messages for the currently targeted player.
- Added tier-benefit previews to the Add Target popup.
- Venue pack export/import now carries VIP benefit definitions.
- Split the UI-pack help prompt onto two lines to prevent clipping in the sidebar.
- Bumped Velvet Rope to 0.2.11.

# Velvet Rope Changelog

## 0.2.10 — Target Quick Add

- Added **ADD TARGET** to the VIP Directory for fast door-side VIP entry.
- Reads the currently targeted player character's name and home world automatically.
- Opens a confirmation popup before saving, with venue, category, and Nightly / Monthly / Yearly / Lifetime status selection.
- Defaults quick-added targets to Nightly status for fast event-night use.
- If the targeted player is already a VIP at the selected venue, the popup switches to **Update & Renew** instead of creating a duplicate.
- Target information is not saved until the user confirms the popup.
- Bumped Velvet Rope to 0.2.10.

## 0.2.9 — UI Pack Help

- Added a hoverable "What are UI packs?" explanation beneath UI pack import controls.
- Explains that packs can include colors, layout, logos, branding, and header artwork.
- Clarifies that UI packs never alter venue/VIP data, shout messages, attendance totals, or reports.
- Bumped Velvet Rope to 0.2.9.

## 0.2.8 — Report Hourly Table

- Replaced the report hourly progress bars with a compact three-column table.
- Hourly attendance now shows Hour, New Guests, and Running Total with no clipped value labels.
- Reduced report detail height to match the more compact tabular layout.
- Bumped Velvet Rope to 0.2.8.

## 0.2.7 — Persistent Privacy Footer
- Removed the large privacy callout cards from individual pages.
- Added a compact privacy footer that stays visible at the bottom of every tab.
- Hovering the footer explains the privacy model in plain language.
- VIP storage is clearly distinguished from anonymous general attendance counting.
- Bumped Velvet Rope to 0.2.7.


## 0.2.6 — Report & Privacy Polish
- Added more internal padding and responsive height to expanded shift report cards.
- Added extra spacing around report metrics, hourly charts, and report actions.
- Rewrote the in-app privacy callout in plain language for venue staff and owners.
- Clarified that general guest names are not saved while explicitly configured VIPs remain stored for alerts.
- Bumped Velvet Rope to 0.2.6.

## 0.2.5 — Branded UI Packs
- Upgraded `.vrui` packs to schema 2 archive packages that can include image assets.
- Added venue logo support in the main header and Control Desk sidebar.
- Added header artwork support in the main window and VIP arrival popup.
- Added live UI controls for logo/header visibility, sizing, opacity, header height, and overlay strength.
- Added Choose Logo and Choose Header Art file pickers.
- Exported UI packs now bundle their active image assets into one portable `.vrui` file.
- Schema-1 color-only UI packs from 0.2.4 remain importable.
- Added path/type/size validation for imported UI assets.
- Bumped Velvet Rope to 0.2.5.

## 0.2.4 — UI Packs
- Added portable `.vrui` appearance packs.
- Added live appearance editor and UI pack import/export.
- Recent VIP Arrivals changed to Name / World / Status / Time / Date columns.
