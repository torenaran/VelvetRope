using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace VelvetRope;

public sealed class VenueSession
{
    private readonly byte[] salt = RandomNumberGenerator.GetBytes(32);
    private readonly HashSet<string> uniqueGuestHashes = new(StringComparer.Ordinal);
    private readonly Dictionary<DateTime, int> hourlyNewGuests = new();

    public VenueSession(Guid venueId, string venueName, DateTime startedAtUtc)
    {
        VenueId = venueId;
        VenueName = venueName;
        StartedAtUtc = startedAtUtc;
    }

    public Guid VenueId { get; }
    public string VenueName { get; }
    public DateTime StartedAtUtc { get; }

    public int VipArrivals { get; set; }
    public int PeakVisibleGuests { get; private set; }
    public int UniqueGuests => uniqueGuestHashes.Count;

    // VIP names are intentionally session-only and are never serialized into reports.
    public List<VipArrivalRecord> RecentVipArrivals { get; } = new();

    public TimeSpan Elapsed(DateTime nowUtc) => nowUtc - StartedAtUtc;

    public bool ObserveGuest(string name, string world, DateTime nowUtc)
    {
        var hash = HashIdentity(name, world);
        if (!uniqueGuestHashes.Add(hash))
            return false;

        var hour = new DateTime(
            nowUtc.Year,
            nowUtc.Month,
            nowUtc.Day,
            nowUtc.Hour,
            0,
            0,
            DateTimeKind.Utc);

        hourlyNewGuests.TryGetValue(hour, out var count);
        hourlyNewGuests[hour] = count + 1;
        return true;
    }

    public void ObserveVisibleGuestCount(int count)
    {
        if (count > PeakVisibleGuests)
            PeakVisibleGuests = count;
    }

    public void RecordVipArrival(VipArrivalRecord arrival)
    {
        VipArrivals++;
        RecentVipArrivals.Insert(0, arrival);
        if (RecentVipArrivals.Count > 25)
            RecentVipArrivals.RemoveRange(25, RecentVipArrivals.Count - 25);
    }

    public SessionReport ToReport(DateTime endedAtUtc) => new()
    {
        VenueId = VenueId,
        VenueName = VenueName,
        StartedAtUtc = StartedAtUtc,
        EndedAtUtc = endedAtUtc,
        UniqueGuests = UniqueGuests,
        VipArrivals = VipArrivals,
        PeakVisibleGuests = PeakVisibleGuests,
        HourlyAttendance = hourlyNewGuests
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new HourlyAttendanceBucket
            {
                HourStartUtc = kvp.Key,
                NewUniqueGuests = kvp.Value
            })
            .ToList()
    };

    private string HashIdentity(string name, string world)
    {
        var normalized = $"{name.Trim().ToUpperInvariant()}@{world.Trim().ToUpperInvariant()}";
        var identityBytes = Encoding.UTF8.GetBytes(normalized);
        var payload = new byte[salt.Length + identityBytes.Length];

        Buffer.BlockCopy(salt, 0, payload, 0, salt.Length);
        Buffer.BlockCopy(identityBytes, 0, payload, salt.Length, identityBytes.Length);

        return Convert.ToHexString(SHA256.HashData(payload));
    }
}
