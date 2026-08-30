using System;
using System.Collections.Generic;

namespace VelvetRope;

[Serializable]
public sealed class TransferEnvelope
{
    public int Schema { get; set; } = 1;
    public string Kind { get; set; } = string.Empty;
    public VenueProfile? Venue { get; set; }
    public List<PersonEntry> People { get; set; } = new();
    public List<VipCategory> Categories { get; set; } = new();
}
