# Velvet Rope Privacy Model

Velvet Rope is designed for venue operations without creating a general attendance log of named players.

## General guest attendance

While a shift is active, Velvet Rope observes player characters currently spawned on the local client. The local player's own character is excluded.

For each observed guest, Velvet Rope normalizes `Character Name@Home World`, combines it with a cryptographically random salt generated for that shift, and computes a SHA-256 hash. The resulting hash is stored only in memory so the same guest is not counted twice during that shift.

The shift salt and hash set are never written to the plugin configuration or report history. Ending the shift or reloading/unloading the plugin destroys that in-memory identity set.

Saved reports contain only aggregate information such as:

- Venue name
- Shift start and end time
- Unique guests observed
- VIP arrival count
- Peak visible guest count
- Aggregate new-unique-guest counts by hour

Saved reports do not contain the names or worlds of general guests.

## VIP data

VIPs are different because the user explicitly configures them for matching and greeting. Velvet Rope stores the character name and optional home world for configured VIPs, along with category and greeting information.

The live shift dashboard may display recent VIP arrivals by name. That live arrival list exists only in memory and is not added to saved reports.

## Export behavior

Venue packs and global people database exports contain explicitly configured VIP identities because that is necessary for sharing VIP configuration. Private notes are intentionally omitted from exports.

## Limitations

Velvet Rope can only observe characters spawned/loaded by the local game client. It is not server-side door-entry telemetry. Object culling, loading, zoning, or client visibility limitations can affect counts.
