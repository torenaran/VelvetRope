namespace VelvetRope;

public sealed record ArrivalAlert(
    string CharacterName,
    string HomeWorld,
    string VenueName,
    string CategoryName,
    string CategoryIcon,
    float AccentR,
    float AccentG,
    float AccentB,
    float AccentA,
    string Message,
    string CopyText,
    bool PublicAnnouncementEnabled)
{
    public string CharacterDisplay => string.IsNullOrWhiteSpace(HomeWorld)
        ? CharacterName
        : $"{CharacterName} @ {HomeWorld}";
}
