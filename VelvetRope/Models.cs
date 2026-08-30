using System;
using System.Collections.Generic;

namespace VelvetRope;

public enum VipDuration
{
    Nightly,
    Monthly,
    Yearly,
    Lifetime
}

[Serializable]
public sealed class PersonEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = string.Empty;

    // Blank means match this character name on any home world.
    public string World { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public string DisplayName => string.IsNullOrWhiteSpace(World)
        ? Name
        : $"{Name} @ {World}";
}

[Serializable]
public sealed class VipCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "VIP";
    public string Icon { get; set; } = "★";
    public string DefaultShoutTemplate { get; set; } = string.Empty;
    public float AccentR { get; set; } = 0.85f;
    public float AccentG { get; set; } = 0.45f;
    public float AccentB { get; set; } = 0.80f;
    public float AccentA { get; set; } = 1.00f;
    public bool BuiltIn { get; set; }

    public static List<VipCategory> CreateDefaults() =>
    [
        new VipCategory
        {
            Name = "VIP",
            Icon = "★",
            DefaultShoutTemplate = "Everyone give a warm welcome to {name}! ♥",
            AccentR = 0.88f,
            AccentG = 0.52f,
            AccentB = 0.83f,
            BuiltIn = true
        },
        new VipCategory
        {
            Name = "DJ",
            Icon = "♫",
            DefaultShoutTemplate = "Make some noise for {name}! We're happy to have them with us tonight! ♫",
            AccentR = 0.45f,
            AccentG = 0.72f,
            AccentB = 0.96f,
            BuiltIn = true
        },
        new VipCategory
        {
            Name = "Venue Owner",
            Icon = "♛",
            DefaultShoutTemplate = "Please welcome {name} to {venue}! ♥",
            AccentR = 0.95f,
            AccentG = 0.72f,
            AccentB = 0.30f,
            BuiltIn = true
        },
        new VipCategory
        {
            Name = "Partner",
            Icon = "◆",
            DefaultShoutTemplate = "Please welcome our friend {name} to {venue}! ♥",
            AccentR = 0.50f,
            AccentG = 0.82f,
            AccentB = 0.72f,
            BuiltIn = true
        },
        new VipCategory
        {
            Name = "Staff",
            Icon = "●",
            DefaultShoutTemplate = "Welcome {name} to {venue}! ♥",
            AccentR = 0.72f,
            AccentG = 0.72f,
            AccentB = 0.78f,
            BuiltIn = true
        },
        new VipCategory
        {
            Name = "Regular",
            Icon = "♥",
            DefaultShoutTemplate = "A familiar face just arrived. Welcome back, {name}! ♥",
            AccentR = 0.90f,
            AccentG = 0.44f,
            AccentB = 0.54f,
            BuiltIn = true
        }
    ];
}

[Serializable]
public sealed class VenueProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "My Venue";
    public bool Enabled { get; set; } = true;
    public string DefaultShoutTemplate { get; set; } = "Everyone give a warm welcome to {name}! ♥";

    // Benefits are venue-specific because the meaning of a Nightly, Monthly,
    // Yearly, or Lifetime VIP can differ completely between workplaces.
    public string NightlyBenefits { get; set; } = string.Empty;
    public string MonthlyBenefits { get; set; } = string.Empty;
    public string YearlyBenefits { get; set; } = string.Empty;
    public string LifetimeBenefits { get; set; } = string.Empty;

    public List<VenueVipEntry> Vips { get; set; } = new();

    public string GetTierBenefits(VipDuration duration) => duration switch
    {
        VipDuration.Nightly => NightlyBenefits,
        VipDuration.Monthly => MonthlyBenefits,
        VipDuration.Yearly => YearlyBenefits,
        VipDuration.Lifetime => LifetimeBenefits,
        _ => string.Empty
    };

    public void SetTierBenefits(VipDuration duration, string value)
    {
        value ??= string.Empty;
        switch (duration)
        {
            case VipDuration.Nightly:
                NightlyBenefits = value;
                break;
            case VipDuration.Monthly:
                MonthlyBenefits = value;
                break;
            case VipDuration.Yearly:
                YearlyBenefits = value;
                break;
            case VipDuration.Lifetime:
                LifetimeBenefits = value;
                break;
        }
    }

    public static VenueProfile CreateDefault(string name) => new()
    {
        Name = name,
        DefaultShoutTemplate = "Everyone give a warm welcome to {name}! ♥"
    };
}

[Serializable]
public sealed class VenueVipEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PersonId { get; set; }
    public bool Enabled { get; set; } = true;
    public Guid CategoryId { get; set; }

    // A silent VIP is still recognized and counted as a VIP arrival, but staff
    // are not given a public /sh line for them.
    public bool PreparePublicShout { get; set; } = true;

    // Some venue relationships are membership tiers (VIP), while others are
    // role/category recognition only (Staff, DJ, Owner, Partner, Regular, etc.).
    // When false, the relationship has no VIP expiration window and its neutral
    // category glyph is used on the nameplate instead of a colored tier marker.
    public bool UseVipTiering { get; set; } = true;

    // VIP duration belongs to the venue relationship, not the global person.
    public VipDuration Duration { get; set; } = VipDuration.Lifetime;
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;

    public List<string> ShoutVariants { get; set; } = new();

    public DateTime? GetExpirationUtc()
    {
        if (!UseVipTiering)
            return null;

        return Duration switch
        {
            VipDuration.Nightly => AddedAtUtc.ToLocalTime().Date.AddDays(1).ToUniversalTime(),
            VipDuration.Monthly => AddedAtUtc.AddMonths(1),
            VipDuration.Yearly => AddedAtUtc.AddYears(1),
            VipDuration.Lifetime => null,
            _ => null
        };
    }

    public bool IsExpired(DateTime nowUtc)
    {
        var expiration = GetExpirationUtc();
        return expiration.HasValue && nowUtc >= expiration.Value;
    }
}

[Serializable]
public sealed class SessionReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VenueId { get; set; }
    public string VenueName { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime EndedAtUtc { get; set; }
    public int UniqueGuests { get; set; }
    public int VipArrivals { get; set; }
    public int PeakVisibleGuests { get; set; }
    public List<HourlyAttendanceBucket> HourlyAttendance { get; set; } = new();

    public TimeSpan Duration => EndedAtUtc > StartedAtUtc
        ? EndedAtUtc - StartedAtUtc
        : TimeSpan.Zero;
}

[Serializable]
public sealed class HourlyAttendanceBucket
{
    public DateTime HourStartUtc { get; set; }
    public int NewUniqueGuests { get; set; }
}

public sealed record VipArrivalRecord(
    DateTime TimestampUtc,
    string CharacterName,
    string HomeWorld,
    string CategoryName);
