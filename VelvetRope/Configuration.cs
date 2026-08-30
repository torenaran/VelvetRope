using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Configuration;

namespace VelvetRope;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 9;

    public int ReentryGraceSeconds { get; set; } = 45;
    public bool NativeToastEnabled { get; set; } = true;
    public bool AlertSoundEnabled { get; set; } = false;

    // In-world VIP tools are local-only helpers. Crowns are rendered only on the
    // staff member's client during an active shift; context-menu actions use the
    // currently selected venue.
    public bool VipCrownBadgesEnabled { get; set; } = true;
    public float VipCrownSize { get; set; } = 18f;
    public float VipCrownMaxDistance { get; set; } = 45f;
    public bool PlayerContextMenuEnabled { get; set; } = true;

    public Guid SelectedVenueId { get; set; } = Guid.Empty;

    public UiTheme UiTheme { get; set; } = global::VelvetRope.UiTheme.CreateDefault();

    public List<PersonEntry> People { get; set; } = new();
    public List<VipCategory> Categories { get; set; } = new();
    public List<VenueProfile> Venues { get; set; } = new();
    public List<SessionReport> Reports { get; set; } = new();

    // Kept only so existing 0.1 configs can be migrated forward.
    public List<LegacyVipEntry> Vips { get; set; } = new();

    public bool EnsureDefaultsAndMigrate()
    {
        var changed = false;

        if (Categories.Count == 0)
        {
            Categories = VipCategory.CreateDefaults();
            changed = true;
        }
        else
        {
            changed |= EnsureBuiltInCategories();
        }

        if (Version < 2 || (Vips.Count > 0 && People.Count == 0 && Venues.Count == 0))
        {
            MigrateFromV1();
            changed = true;
        }

        // 0.2.3 adds venue-specific VIP durations. Existing relationships are
        // intentionally migrated as Lifetime so an upgrade never expires old VIPs.
        if (Version < 3)
        {
            foreach (var venue in Venues)
            foreach (var link in venue.Vips)
            {
                link.Duration = VipDuration.Lifetime;
                if (link.AddedAtUtc == default)
                    link.AddedAtUtc = DateTime.UtcNow;
            }

            changed = true;
        }

        // 0.2.4 introduces portable UI packs. 0.2.5 extends them with optional
        // local image assets while preserving older color-only configurations.
        if (UiTheme is null)
        {
            UiTheme = global::VelvetRope.UiTheme.CreateDefault();
            changed = true;
        }

        UiTheme.Sanitize();

        // 0.3.0 added silent/internal VIP recognition. Existing VIPs keep the
        // behavior they had before the upgrade: a public shout is prepared.
        if (Version < 7)
        {
            foreach (var venue in Venues)
            foreach (var link in venue.Vips)
                link.PreparePublicShout = true;

            changed = true;
        }

        // 0.3.3 added VIP marker preferences and player context-menu helpers. 0.3.7 moved markers to native nameplates; 0.3.8 used tintable text glyphs; 0.3.9 uses diamonds for Nightly/Monthly/Yearly and a star for Lifetime.
        // Existing users receive the normal defaults and the new schema is saved once.
        if (Version < 8)
        {
            if (VipCrownSize < 10f)
                VipCrownSize = 18f;
            if (VipCrownMaxDistance < 5f)
                VipCrownMaxDistance = 45f;
            changed = true;
        }


        // 0.3.10 makes VIP tiering optional per venue relationship. Existing
        // relationships keep their old tier behavior; staff can explicitly turn
        // tiering off, at which point the relationship becomes non-expiring and
        // uses its category glyph as a neutral nameplate marker.
        if (Version < 9)
        {
            foreach (var venue in Venues)
            foreach (var link in venue.Vips)
                link.UseVipTiering = true;

            changed = true;
        }

        VipCrownSize = Math.Clamp(VipCrownSize, 10f, 32f);
        VipCrownMaxDistance = Math.Clamp(VipCrownMaxDistance, 5f, 100f);

        if (Venues.Count == 0)
        {
            var venue = VenueProfile.CreateDefault("My Venue");
            Venues.Add(venue);
            SelectedVenueId = venue.Id;
            changed = true;
        }

        if (SelectedVenueId == Guid.Empty || Venues.All(v => v.Id != SelectedVenueId))
        {
            SelectedVenueId = Venues[0].Id;
            changed = true;
        }

        Version = 9;
        return changed;
    }

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }

    private bool EnsureBuiltInCategories()
    {
        var changed = false;
        foreach (var required in VipCategory.CreateDefaults())
        {
            if (Categories.Any(c => string.Equals(c.Name, required.Name, StringComparison.OrdinalIgnoreCase)))
                continue;

            Categories.Add(required);
            changed = true;
        }

        return changed;
    }

    private void MigrateFromV1()
    {
        var venue = Venues.FirstOrDefault() ?? VenueProfile.CreateDefault("My Venue");
        if (!Venues.Contains(venue))
            Venues.Add(venue);

        var vipCategory = Categories.First(c => string.Equals(c.Name, "VIP", StringComparison.OrdinalIgnoreCase));

        foreach (var oldVip in Vips.Where(v => !string.IsNullOrWhiteSpace(v.Name)))
        {
            var person = People.FirstOrDefault(p =>
                string.Equals(p.Name.Trim(), oldVip.Name.Trim(), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.World.Trim(), oldVip.World.Trim(), StringComparison.OrdinalIgnoreCase));

            if (person is null)
            {
                person = new PersonEntry
                {
                    Name = oldVip.Name.Trim(),
                    World = oldVip.World.Trim(),
                    Enabled = oldVip.Enabled
                };
                People.Add(person);
            }

            if (venue.Vips.Any(v => v.PersonId == person.Id))
                continue;

            var link = new VenueVipEntry
            {
                PersonId = person.Id,
                Enabled = oldVip.Enabled,
                CategoryId = vipCategory.Id
            };

            if (!string.IsNullOrWhiteSpace(oldVip.ShoutMessage))
                link.ShoutVariants.Add(oldVip.ShoutMessage.Trim());

            venue.Vips.Add(link);
        }

        Vips.Clear();
        SelectedVenueId = venue.Id;
    }
}

[Serializable]
public sealed class LegacyVipEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;
    public string ShoutMessage { get; set; } = string.Empty;
}
