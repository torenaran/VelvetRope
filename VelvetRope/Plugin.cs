using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using VelvetRope.Windows;

namespace VelvetRope;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/velvetrope";
    private const string ShortCommandName = "/vr";
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(1);

    private readonly MainWindow mainWindow;
    private readonly ArrivalWindow arrivalWindow;
    private readonly Queue<ArrivalAlert> alertQueue = new();
    private readonly HashSet<Guid> presentVipLinksLastScan = new();
    private readonly Dictionary<Guid, DateTime> absentVipLinksSince = new();
    private readonly Dictionary<Guid, int> lastVariantByLink = new();
    private readonly Dictionary<ulong, NameplateBadgeInfo> visibleVipBadges = new();
    private readonly record struct NameplateBadgeInfo(bool UseVipTiering, VipDuration Duration, Guid CategoryId);
    private readonly Random random = new();

    private DateTime nextScanUtc = DateTime.MinValue;
    private bool seedVipPresenceNextScan;

    // Context menus can occasionally report the same open more than once. Keep a
    // very short per-target guard so Velvet Rope never injects duplicate entries.
    private long lastContextMenuInjectionTick;
    private nint lastContextMenuAddonPtr;
    private nint lastContextMenuAgentPtr;
    private string lastContextMenuIdentity = string.Empty;

    // Crown preview is deliberately independent of shift/VIP matching so staff can
    // verify native nameplate crown rendering on the current target.
    private ulong previewCrownObjectId;
    private DateTime previewCrownUntilUtc = DateTime.MinValue;

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IContextMenu ContextMenu { get; private set; } = null!;
    [PluginService] internal static INamePlateGui NamePlateGui { get; private set; } = null!;
    [PluginService] internal static IToastGui ToastGui { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    public Configuration Configuration { get; }
    public WindowSystem WindowSystem { get; } = new("VelvetRope");

    public VenueSession? ActiveSession { get; private set; }
    public ArrivalAlert? CurrentAlert { get; private set; }
    public int PendingAlertCount => alertQueue.Count;
    public int VisibleVipBadgeCount => visibleVipBadges.Count;

    public bool PreviewCrownOnCurrentTarget()
    {
        if (TargetManager.Target is not IPlayerCharacter player)
            return false;

        previewCrownObjectId = player.GameObjectId;
        previewCrownUntilUtc = DateTime.UtcNow.AddSeconds(10);
        NamePlateGui.RequestRedraw();
        ShowNormalToast($"Velvet Rope: previewing the gold Lifetime star on {player.Name.TextValue.Trim()} for 10 seconds.");
        return true;
    }

    public VenueProfile SelectedVenue =>
        Configuration.Venues.FirstOrDefault(v => v.Id == Configuration.SelectedVenueId)
        ?? Configuration.Venues[0];

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (Configuration.EnsureDefaultsAndMigrate())
            Configuration.Save();

        mainWindow = new MainWindow(this);
        arrivalWindow = new ArrivalWindow(this);
        WindowSystem.AddWindow(mainWindow);
        WindowSystem.AddWindow(arrivalWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Velvet Rope. '/velvetrope start' starts a shift; '/velvetrope end' ends it."
        });
        CommandManager.AddHandler(ShortCommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Velvet Rope. Alias for /velvetrope."
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleMainUi;
        Framework.Update += OnFrameworkUpdate;
        ContextMenu.OnMenuOpened += OnContextMenuOpened;
        NamePlateGui.OnNamePlateUpdate += OnNamePlateUpdate;
        NamePlateGui.RequestRedraw();

        Log.Information("Velvet Rope 0.3.10 loaded.");
    }

    public void Dispose()
    {
        if (ActiveSession is not null)
            EndShift(showToast: false);

        Framework.Update -= OnFrameworkUpdate;
        ContextMenu.OnMenuOpened -= OnContextMenuOpened;
        NamePlateGui.OnNamePlateUpdate -= OnNamePlateUpdate;
        NamePlateGui.RequestRedraw();
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleMainUi;

        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(ShortCommandName);

        WindowSystem.RemoveAllWindows();
        mainWindow.Dispose();
        arrivalWindow.Dispose();
    }

    public void ToggleMainUi() => mainWindow.Toggle();

    public PersonEntry? GetPerson(Guid id) =>
        Configuration.People.FirstOrDefault(p => p.Id == id);

    public VipCategory GetCategory(Guid id) =>
        Configuration.Categories.FirstOrDefault(c => c.Id == id)
        ?? Configuration.Categories.First();

    public bool TryGetCurrentTargetPlayer(out string name, out string world)
    {
        name = string.Empty;
        world = string.Empty;

        if (TargetManager.Target is not IPlayerCharacter player)
            return false;

        name = player.Name.TextValue.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return false;

        world = player.HomeWorld.IsValid
            ? player.HomeWorld.Value.Name.ToString().Trim()
            : string.Empty;

        return true;
    }

    public string BuildVipBenefitsText(VenueProfile venue, VipDuration duration)
    {
        var benefits = NormalizeBenefitsForChat(venue.GetTierBenefits(duration));
        if (string.IsNullOrWhiteSpace(benefits))
            return string.Empty;

        return $"{venue.Name} {FormatVipDuration(duration)} VIP benefits: {benefits}";
    }

    public string BuildVipBenefitsTell(
        VenueProfile venue,
        VipDuration duration,
        string targetName,
        string targetWorld)
    {
        var message = BuildVipBenefitsText(venue, duration);
        if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(targetName))
            return string.Empty;

        var recipient = string.IsNullOrWhiteSpace(targetWorld)
            ? targetName.Trim()
            : $"{targetName.Trim()}@{targetWorld.Trim()}";

        return $"/tell {recipient} {message}";
    }

    public static string FormatVipDuration(VipDuration duration) => duration switch
    {
        VipDuration.Nightly => "Nightly",
        VipDuration.Monthly => "Monthly",
        VipDuration.Yearly => "Yearly",
        VipDuration.Lifetime => "Lifetime",
        _ => "VIP"
    };

    private static string NormalizeBenefitsForChat(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value
            .Replace("\r\n", " • ", StringComparison.Ordinal)
            .Replace("\n", " • ", StringComparison.Ordinal)
            .Replace("\r", " • ", StringComparison.Ordinal)
            .Trim();

        while (normalized.Contains("  ", StringComparison.Ordinal))
            normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);

        return normalized;
    }

    public VenueVipEntry? FindVipInVenue(VenueProfile venue, string name, string world)
    {
        foreach (var link in venue.Vips)
        {
            var person = GetPerson(link.PersonId);
            if (person is not null && Matches(person, name, world))
                return link;
        }

        return null;
    }

    public void UpdateVipStatus(VenueVipEntry link, Guid categoryId, bool useVipTiering, VipDuration duration)
    {
        link.CategoryId = categoryId == Guid.Empty ? Configuration.Categories[0].Id : categoryId;
        link.UseVipTiering = useVipTiering;
        link.Duration = duration;
        link.AddedAtUtc = DateTime.UtcNow;
        link.Enabled = true;

        var person = GetPerson(link.PersonId);
        if (person is not null)
            person.Enabled = true;

        Configuration.Save();

        ResetVipPresenceTracking();
        if (ActiveSession is not null)
            seedVipPresenceNextScan = true;
    }

    public void SelectVenue(Guid venueId)
    {
        if (ActiveSession is not null)
            return;

        if (Configuration.Venues.All(v => v.Id != venueId))
            return;

        Configuration.SelectedVenueId = venueId;
        Configuration.Save();
    }

    public bool StartShift()
    {
        if (ActiveSession is not null)
            return false;

        var venue = SelectedVenue;
        var nowUtc = DateTime.UtcNow;
        var expiredRemoved = CleanupExpiredVips(venue, nowUtc);
        ActiveSession = new VenueSession(venue.Id, venue.Name, nowUtc);

        ResetVipPresenceTracking();
        ClearAlerts();
        seedVipPresenceNextScan = true;
        nextScanUtc = DateTime.MinValue;

        var cleanupNote = expiredRemoved > 0 ? $" Removed {expiredRemoved} expired VIP status(es)." : string.Empty;
        ShowNormalToast($"Velvet Rope: shift started at {venue.Name}.{cleanupNote}");
        Log.Information("Shift started for {Venue}. Expired VIP statuses removed: {Expired}.", venue.Name, expiredRemoved);
        return true;
    }

    public SessionReport? EndShift(bool showToast = true)
    {
        var session = ActiveSession;
        if (session is null)
            return null;

        var report = session.ToReport(DateTime.UtcNow);
        Configuration.Reports.Insert(0, report);

        // Keep local history useful without allowing indefinite growth.
        if (Configuration.Reports.Count > 250)
            Configuration.Reports.RemoveRange(250, Configuration.Reports.Count - 250);

        Configuration.Save();

        ActiveSession = null;
        ResetVipPresenceTracking();
        ClearAlerts();

        if (showToast)
            ShowNormalToast($"Velvet Rope: shift ended. {report.UniqueGuests} unique guests observed.");

        Log.Information(
            "Shift ended for {Venue}. Unique guests: {Guests}; VIP arrivals: {Vips}; peak visible: {Peak}.",
            report.VenueName,
            report.UniqueGuests,
            report.VipArrivals,
            report.PeakVisibleGuests);

        return report;
    }

    public void TestAlert(VenueProfile venue, VenueVipEntry link)
    {
        var person = GetPerson(link.PersonId);
        if (person is null)
            return;

        var name = string.IsNullOrWhiteSpace(person.Name) ? "Example VIP" : person.Name.Trim();
        var world = string.IsNullOrWhiteSpace(person.World) ? "Leviathan" : person.World.Trim();
        EnqueueAlert(BuildAlert(venue, link, person, name, world), countForSession: false);
    }

    public VenueVipEntry AddVipToVenue(
        VenueProfile venue,
        string name,
        string world,
        Guid categoryId,
        bool useVipTiering,
        VipDuration duration,
        string initialVariant)
    {
        var cleanName = name.Trim();
        var cleanWorld = world.Trim();

        var person = Configuration.People.FirstOrDefault(p =>
            string.Equals(p.Name.Trim(), cleanName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.World.Trim(), cleanWorld, StringComparison.OrdinalIgnoreCase));

        if (person is null)
        {
            person = new PersonEntry
            {
                Name = cleanName,
                World = cleanWorld
            };
            Configuration.People.Add(person);
        }

        var existing = venue.Vips.FirstOrDefault(v => v.PersonId == person.Id);
        if (existing is not null)
            return existing;

        var link = new VenueVipEntry
        {
            PersonId = person.Id,
            CategoryId = categoryId == Guid.Empty ? Configuration.Categories[0].Id : categoryId,
            UseVipTiering = useVipTiering,
            Duration = duration,
            AddedAtUtc = DateTime.UtcNow
        };

        if (!string.IsNullOrWhiteSpace(initialVariant))
            link.ShoutVariants.Add(initialVariant.Trim());

        venue.Vips.Add(link);
        Configuration.Save();
        return link;
    }

    public bool AssignExistingPerson(VenueProfile venue, Guid personId, Guid categoryId, bool useVipTiering, VipDuration duration)
    {
        if (venue.Vips.Any(v => v.PersonId == personId))
            return false;

        if (Configuration.People.All(p => p.Id != personId))
            return false;

        venue.Vips.Add(new VenueVipEntry
        {
            PersonId = personId,
            CategoryId = categoryId == Guid.Empty ? Configuration.Categories[0].Id : categoryId,
            UseVipTiering = useVipTiering,
            Duration = duration,
            AddedAtUtc = DateTime.UtcNow
        });

        Configuration.Save();
        return true;
    }

    public int CleanupExpiredVips(VenueProfile venue, DateTime nowUtc)
    {
        var expiredIds = venue.Vips
            .Where(v => v.IsExpired(nowUtc))
            .Select(v => v.Id)
            .ToHashSet();

        if (expiredIds.Count == 0)
            return 0;

        venue.Vips.RemoveAll(v => expiredIds.Contains(v.Id));
        Configuration.Save();

        ResetVipPresenceTracking();
        if (ActiveSession is not null)
            seedVipPresenceNextScan = true;

        return expiredIds.Count;
    }

    public void RenewVip(VenueVipEntry link)
    {
        link.AddedAtUtc = DateTime.UtcNow;
        link.Enabled = true;
        Configuration.Save();
    }

    public void RemoveVipFromVenue(VenueProfile venue, Guid linkId)
    {
        venue.Vips.RemoveAll(v => v.Id == linkId);
        Configuration.Save();
        ResetVipPresenceTracking();
        if (ActiveSession is not null)
            seedVipPresenceNextScan = true;
    }

    public VenueProfile? AddVenue(string name)
    {
        var venue = VenueProfile.CreateDefault(string.IsNullOrWhiteSpace(name) ? "New Venue" : name.Trim());
        Configuration.Venues.Add(venue);

        if (ActiveSession is null)
            Configuration.SelectedVenueId = venue.Id;

        Configuration.Save();
        return venue;
    }

    public bool DeleteVenue(Guid venueId)
    {
        if (Configuration.Venues.Count <= 1)
            return false;

        if (ActiveSession?.VenueId == venueId)
            return false;

        var removed = Configuration.Venues.RemoveAll(v => v.Id == venueId) > 0;
        if (!removed)
            return false;

        if (Configuration.SelectedVenueId == venueId)
            Configuration.SelectedVenueId = Configuration.Venues[0].Id;

        Configuration.Save();
        return true;
    }

    public string ExportVenuePack(Guid venueId)
    {
        var venue = Configuration.Venues.First(v => v.Id == venueId);
        var peopleIds = venue.Vips.Select(v => v.PersonId).ToHashSet();
        var categoryIds = venue.Vips.Select(v => v.CategoryId).ToHashSet();

        var envelope = new TransferEnvelope
        {
            Kind = "venue-pack",
            Venue = CloneVenue(venue),
            People = Configuration.People
                .Where(p => peopleIds.Contains(p.Id))
                .Select(ClonePerson)
                .ToList(),
            Categories = Configuration.Categories
                .Where(c => categoryIds.Contains(c.Id))
                .Select(CloneCategory)
                .ToList()
        };

        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    public string ExportVipDatabase()
    {
        var envelope = new TransferEnvelope
        {
            Kind = "vip-database",
            People = Configuration.People.Select(ClonePerson).ToList(),
            Categories = Configuration.Categories.Select(CloneCategory).ToList()
        };

        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    public string ExportUiPack()
    {
        // Retained for backwards compatibility with tooling that expects a JSON
        // representation. File exports use the schema-2 archive format below.
        var envelope = new UiPackEnvelope
        {
            Schema = 2,
            Theme = Configuration.UiTheme.Clone()
        };

        envelope.Theme.AssetPackId = string.Empty;
        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    public string ExportUiPackToFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "No UI pack destination was selected.";

        try
        {
            var finalPath = Path.GetExtension(path).Equals(".vrui", StringComparison.OrdinalIgnoreCase)
                ? path
                : path + ".vrui";

            if (File.Exists(finalPath))
                File.Delete(finalPath);

            var theme = Configuration.UiTheme.Clone();
            var sourcePackId = theme.AssetPackId;
            theme.AssetPackId = string.Empty;

            using var archive = ZipFile.Open(finalPath, ZipArchiveMode.Create);
            var manifest = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
            using (var writer = new StreamWriter(manifest.Open()))
            {
                writer.Write(JsonSerializer.Serialize(new UiPackEnvelope
                {
                    Schema = 2,
                    Theme = theme
                }, JsonOptions));
            }

            CopyThemeAssetIntoArchive(archive, sourcePackId, theme.LogoAsset);
            CopyThemeAssetIntoArchive(archive, sourcePackId, theme.HeaderBackgroundAsset);

            return $"Exported UI pack '{Configuration.UiTheme.PackName}' to {Path.GetFileName(finalPath)}.";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not export Velvet Rope UI pack to {Path}.", path);
            return "Velvet Rope could not write that UI pack file.";
        }
    }

    public string ImportUiPackFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return "That UI pack file could not be found.";

        try
        {
            using var stream = File.OpenRead(path);
            var isZip = stream.Length >= 4 && stream.ReadByte() == 'P' && stream.ReadByte() == 'K';
            stream.Position = 0;

            if (isZip)
                return ImportUiPackArchive(path);

            using var reader = new StreamReader(stream);
            return ImportUiPack(reader.ReadToEnd());
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not read Velvet Rope UI pack from {Path}.", path);
            return "Velvet Rope could not read that UI pack file.";
        }
    }

    public string ImportUiPack(string json)
    {
        UiPackEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<UiPackEnvelope>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not parse Velvet Rope UI pack.");
            return "That file is not a valid Velvet Rope UI pack.";
        }

        if (envelope is null || envelope.Schema is < 1 or > 2 ||
            !string.Equals(envelope.Kind, "ui-pack", StringComparison.OrdinalIgnoreCase) ||
            envelope.Theme is null)
            return "Unsupported Velvet Rope UI pack.";

        envelope.Theme.AssetPackId = string.Empty;
        envelope.Theme.LogoAsset = string.Empty;
        envelope.Theme.HeaderBackgroundAsset = string.Empty;
        envelope.Theme.ShowHeaderLogo = false;
        envelope.Theme.ShowSidebarLogo = false;
        envelope.Theme.ShowHeaderBackground = false;
        envelope.Theme.Sanitize();
        ReplaceTheme(envelope.Theme);
        return $"Applied UI pack '{Configuration.UiTheme.PackName}' by {Configuration.UiTheme.Author}.";
    }

    private string ImportUiPackArchive(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var manifestEntry = archive.GetEntry("manifest.json");
        if (manifestEntry is null || manifestEntry.Length > 512_000)
            return "That UI pack does not contain a valid manifest.";

        UiPackEnvelope? envelope;
        using (var reader = new StreamReader(manifestEntry.Open()))
            envelope = JsonSerializer.Deserialize<UiPackEnvelope>(reader.ReadToEnd(), JsonOptions);

        if (envelope is null || envelope.Schema != 2 ||
            !string.Equals(envelope.Kind, "ui-pack", StringComparison.OrdinalIgnoreCase) ||
            envelope.Theme is null)
            return "Unsupported Velvet Rope UI pack.";

        envelope.Theme.Sanitize();

        var newPackId = BuildAssetPackId(envelope.Theme.PackName);
        var assetRoot = GetAssetPackDirectory(newPackId);
        Directory.CreateDirectory(assetRoot);

        try
        {
            ExtractUiAsset(archive, envelope.Theme.LogoAsset, assetRoot);
            ExtractUiAsset(archive, envelope.Theme.HeaderBackgroundAsset, assetRoot);
            envelope.Theme.AssetPackId = newPackId;
            ReplaceTheme(envelope.Theme);
            return $"Applied UI pack '{Configuration.UiTheme.PackName}' by {Configuration.UiTheme.Author}.";
        }
        catch
        {
            try { Directory.Delete(assetRoot, true); } catch { }
            throw;
        }
    }

    public IDalamudTextureWrap? GetUiAssetTexture(string assetName)
    {
        var path = ResolveUiAssetPath(Configuration.UiTheme.AssetPackId, assetName);
        return path is null ? null : TextureProvider.GetFromFileAbsolute(path).GetWrapOrDefault();
    }

    public string SetUiAssetFromFile(string path, bool headerBackground)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return "That image could not be found.";

        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg" or ".webp"))
            return "UI assets must be PNG, JPG, JPEG, or WEBP images.";

        var info = new FileInfo(path);
        if (info.Length > 5_000_000)
            return "That image is too large. UI assets are limited to 5 MB each.";

        var theme = Configuration.UiTheme;
        if (string.IsNullOrWhiteSpace(theme.AssetPackId))
            theme.AssetPackId = BuildAssetPackId("custom");

        var root = GetAssetPackDirectory(theme.AssetPackId);
        Directory.CreateDirectory(root);
        var fileName = (headerBackground ? "header" : "logo") + extension;
        File.Copy(path, Path.Combine(root, fileName), true);

        if (headerBackground)
        {
            theme.HeaderBackgroundAsset = fileName;
            theme.ShowHeaderBackground = true;
        }
        else
        {
            theme.LogoAsset = fileName;
            theme.ShowHeaderLogo = true;
            theme.ShowSidebarLogo = true;
        }

        theme.Sanitize();
        Configuration.Save();
        return headerBackground ? "Header artwork updated." : "Logo updated.";
    }

    public void ClearUiAsset(bool headerBackground)
    {
        if (headerBackground)
        {
            Configuration.UiTheme.HeaderBackgroundAsset = string.Empty;
            Configuration.UiTheme.ShowHeaderBackground = false;
        }
        else
        {
            Configuration.UiTheme.LogoAsset = string.Empty;
            Configuration.UiTheme.ShowHeaderLogo = false;
            Configuration.UiTheme.ShowSidebarLogo = false;
        }
        Configuration.Save();
    }

    private void ReplaceTheme(UiTheme theme)
    {
        var oldPackId = Configuration.UiTheme.AssetPackId;
        Configuration.UiTheme = theme.Clone();
        Configuration.UiTheme.Sanitize();
        Configuration.Save();

        if (!string.IsNullOrWhiteSpace(oldPackId) &&
            !string.Equals(oldPackId, Configuration.UiTheme.AssetPackId, StringComparison.OrdinalIgnoreCase))
            TryDeleteAssetPack(oldPackId);
    }

    private static string BuildAssetPackId(string name)
    {
        var safe = new string((name ?? "pack").Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        if (string.IsNullOrWhiteSpace(safe)) safe = "pack";
        if (safe.Length > 40) safe = safe[..40];
        return $"{safe}-{Guid.NewGuid():N}"[..Math.Min(55, safe.Length + 33)];
    }

    private static string GetAssetPackDirectory(string packId) =>
        Path.Combine(PluginInterface.ConfigDirectory.FullName, "ui-assets", packId);

    private static string? ResolveUiAssetPath(string packId, string assetName)
    {
        if (string.IsNullOrWhiteSpace(packId) || string.IsNullOrWhiteSpace(assetName))
            return null;
        if (assetName != Path.GetFileName(assetName))
            return null;

        var path = Path.Combine(GetAssetPackDirectory(packId), assetName);
        return File.Exists(path) ? path : null;
    }

    private static void ExtractUiAsset(ZipArchive archive, string assetName, string destinationRoot)
    {
        if (string.IsNullOrWhiteSpace(assetName))
            return;
        if (assetName != Path.GetFileName(assetName))
            throw new InvalidDataException("Invalid UI asset name.");

        var extension = Path.GetExtension(assetName).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg" or ".webp"))
            throw new InvalidDataException("Unsupported UI asset type.");

        var entry = archive.GetEntry($"assets/{assetName}")
            ?? throw new InvalidDataException($"Missing UI asset: {assetName}");
        if (entry.Length > 5_000_000)
            throw new InvalidDataException("UI asset exceeds the 5 MB limit.");

        var destination = Path.Combine(destinationRoot, assetName);
        entry.ExtractToFile(destination, true);
    }

    private void CopyThemeAssetIntoArchive(ZipArchive archive, string packId, string assetName)
    {
        var path = ResolveUiAssetPath(packId, assetName);
        if (path is null)
            return;

        archive.CreateEntryFromFile(path, $"assets/{assetName}", CompressionLevel.Optimal);
    }

    private static void TryDeleteAssetPack(string packId)
    {
        try
        {
            var root = GetAssetPackDirectory(packId);
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
        catch { }
    }

    public void ResetUiTheme()
    {
        var oldPackId = Configuration.UiTheme.AssetPackId;
        Configuration.UiTheme = UiTheme.CreateDefault();
        Configuration.Save();
        if (!string.IsNullOrWhiteSpace(oldPackId))
            TryDeleteAssetPack(oldPackId);
    }

    public string ImportTransfer(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return "Clipboard is empty.";

        TransferEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<TransferEnvelope>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not parse Velvet Rope import payload.");
            return "That clipboard text is not a valid Velvet Rope pack.";
        }

        if (envelope is null || envelope.Schema != 1)
            return "Unsupported Velvet Rope pack.";

        return envelope.Kind switch
        {
            "venue-pack" => ImportVenuePack(envelope),
            "vip-database" => ImportVipDatabase(envelope),
            _ => "Unknown Velvet Rope pack type."
        };
    }

    public string BuildReportSummary(SessionReport report)
    {
        var start = report.StartedAtUtc.ToLocalTime();
        var end = report.EndedAtUtc.ToLocalTime();

        return
            $"Velvet Rope Report - {report.VenueName}\n" +
            $"{start:yyyy-MM-dd h:mm tt} - {end:h:mm tt}\n" +
            $"Unique Guests: {report.UniqueGuests}\n" +
            $"VIP Arrivals: {report.VipArrivals}\n" +
            $"Peak Visible Guests: {report.PeakVisibleGuests}\n" +
            $"Duration: {FormatDuration(report.Duration)}";
    }

    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        return $"{Math.Max(0, duration.Minutes)}m";
    }

    public void DismissCurrentAlert()
    {
        if (alertQueue.Count > 0)
        {
            CurrentAlert = alertQueue.Dequeue();
            arrivalWindow.IsOpen = true;
            arrivalWindow.RequestFocus = true;
            return;
        }

        CurrentAlert = null;
        arrivalWindow.IsOpen = false;
    }

    private void OnContextMenuOpened(IMenuOpenedArgs args)
    {
        if (!Configuration.PlayerContextMenuEnabled || args.MenuType != ContextMenuType.Default)
            return;

        if (args.Target is not MenuTargetDefault target)
            return;

        var name = target.TargetName?.Trim() ?? string.Empty;
        var world = target.TargetHomeWorld.IsValid
            ? target.TargetHomeWorld.Value.Name.ToString().Trim()
            : string.Empty;

        if (target.TargetObject is IPlayerCharacter player)
        {
            name = player.Name.TextValue.Trim();
            world = player.HomeWorld.IsValid
                ? player.HomeWorld.Value.Name.ToString().Trim()
                : world;
        }

        if (string.IsNullOrWhiteSpace(name))
            return;

        // Avoid adding Velvet Rope actions to non-player default context menus.
        if (target.TargetObject is not IPlayerCharacter && !target.TargetHomeWorld.IsValid && target.TargetCharacter is null)
            return;

        var identity = $"{name.ToLowerInvariant()}@{world.ToLowerInvariant()}";
        var nowTick = Environment.TickCount64;
        if (lastContextMenuAddonPtr == args.AddonPtr &&
            lastContextMenuAgentPtr == args.AgentPtr &&
            string.Equals(lastContextMenuIdentity, identity, StringComparison.Ordinal) &&
            nowTick - lastContextMenuInjectionTick >= 0 &&
            nowTick - lastContextMenuInjectionTick < 500)
        {
            Log.Debug("Suppressed duplicate Velvet Rope context-menu injection for {Identity}.", identity);
            return;
        }

        lastContextMenuAddonPtr = args.AddonPtr;
        lastContextMenuAgentPtr = args.AgentPtr;
        lastContextMenuIdentity = identity;
        lastContextMenuInjectionTick = nowTick;

        var venue = SelectedVenue;
        var link = FindVipInVenue(venue, name, world);

        if (link is null)
        {
            args.AddMenuItem(new MenuItem
            {
                Name = "Velvet Rope: Add VIP...",
                PrefixChar = 'V',
                OnClicked = _ => mainWindow.OpenAddPlayerVip(name, world),
                Priority = 100
            });
            return;
        }

        var category = GetCategory(link.CategoryId);
        if (!link.UseVipTiering)
        {
            var roleSuffix = !link.Enabled ? " (disabled)" : string.Empty;
            args.AddMenuItem(new MenuItem
            {
                Name = $"Velvet Rope: {category.Icon} {category.Name}{roleSuffix}",
                PrefixChar = 'V',
                IsSubmenu = true,
                Priority = 100,
                OnClicked = clicked => clicked.OpenSubmenu(
                    category.Name,
                    BuildCategoryContextMenuItems(venue, category, name))
            });
            return;
        }

        var duration = link.Duration;
        var statusSuffix = link.IsExpired(DateTime.UtcNow) ? " (expired)" : !link.Enabled ? " (disabled)" : string.Empty;
        args.AddMenuItem(new MenuItem
        {
            Name = $"VIP Tier: {FormatVipDuration(duration)}{statusSuffix}",
            PrefixChar = 'V',
            IsSubmenu = true,
            Priority = 100,
            OnClicked = clicked => clicked.OpenSubmenu(
                $"{FormatVipDuration(duration)} VIP",
                BuildVipContextMenuItems(venue, duration, name, world))
        });
    }

    private IReadOnlyList<IMenuItem> BuildCategoryContextMenuItems(
        VenueProfile venue,
        VipCategory category,
        string targetName)
    {
        return new IMenuItem[]
        {
            new MenuItem
            {
                Name = $"Venue: {venue.Name}",
                IsEnabled = false
            },
            new MenuItem
            {
                Name = $"Category: {category.Icon} {category.Name}",
                IsEnabled = false
            },
            new MenuItem
            {
                Name = "View in Velvet Rope",
                OnClicked = _ => mainWindow.OpenVipDirectory(targetName)
            }
        };
    }

    private IReadOnlyList<IMenuItem> BuildVipContextMenuItems(
        VenueProfile venue,
        VipDuration duration,
        string targetName,
        string targetWorld)
    {
        var benefits = venue.GetTierBenefits(duration);
        var items = new List<IMenuItem>
        {
            new MenuItem
            {
                Name = $"Venue: {venue.Name}",
                IsEnabled = false
            },
            new MenuItem
            {
                Name = "Tier Benefits",
                IsSubmenu = true,
                OnClicked = clicked => clicked.OpenSubmenu(
                    $"{FormatVipDuration(duration)} Benefits",
                    BuildBenefitLineItems(benefits))
            },
            new MenuItem
            {
                Name = "Copy Benefits Tell",
                IsEnabled = !string.IsNullOrWhiteSpace(benefits),
                OnClicked = _ =>
                {
                    var tell = BuildVipBenefitsTell(venue, duration, targetName, targetWorld);
                    if (!string.IsNullOrWhiteSpace(tell))
                    {
                        ImGui.SetClipboardText(tell);
                        ShowNormalToast($"Velvet Rope: copied {FormatVipDuration(duration)} benefits tell.");
                    }
                }
            },
            new MenuItem
            {
                Name = "View in Velvet Rope",
                OnClicked = _ => mainWindow.OpenVipDirectory(targetName)
            }
        };

        return items;
    }

    private static IReadOnlyList<IMenuItem> BuildBenefitLineItems(string benefits)
    {
        if (string.IsNullOrWhiteSpace(benefits))
        {
            return new IMenuItem[]
            {
                new MenuItem { Name = "No benefits configured for this tier.", IsEnabled = false }
            };
        }

        var normalized = benefits
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace(" • ", "\n", StringComparison.Ordinal);

        var lines = normalized
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(WrapBenefitLine)
            .Take(12)
            .Select(line => (IMenuItem)new MenuItem
            {
                Name = $"• {line}",
                IsEnabled = false
            })
            .ToList();

        return lines.Count > 0
            ? lines
            : new IMenuItem[] { new MenuItem { Name = "No benefits configured for this tier.", IsEnabled = false } };
    }

    private static IEnumerable<string> WrapBenefitLine(string line)
    {
        const int maxLength = 52;
        var remaining = line.Trim();

        while (remaining.Length > maxLength)
        {
            var split = remaining.LastIndexOf(' ', maxLength);
            if (split <= 0)
                split = maxLength;

            yield return remaining[..split].Trim();
            remaining = remaining[split..].Trim();
        }

        if (!string.IsNullOrWhiteSpace(remaining))
            yield return remaining;
    }

    private void OnNamePlateUpdate(
        INamePlateUpdateContext context,
        IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        var nowUtc = DateTime.UtcNow;
        var previewActive = previewCrownObjectId != 0 && nowUtc < previewCrownUntilUtc;
        var normalBadgesActive = Configuration.VipCrownBadgesEnabled && ActiveSession is not null && visibleVipBadges.Count > 0;
        var localPosition = ObjectTable.LocalPlayer?.Position;
        var maxDistance = Math.Clamp(Configuration.VipCrownMaxDistance, 5f, 100f);

        foreach (var handler in handlers)
        {
            var player = handler.PlayerCharacter;
            if (player is null)
                continue;

            // 0.3.7 placed a baked-gold Mentor icon in StatusPrefix. Always strip
            // Velvet Rope's old status marker so upgrading users do not retain it.
            var basePrefix = StripVelvetRopeStatusCrowns(handler.StatusPrefix);

            // Colored text belongs in the actual name field rather than StatusPrefix:
            // StatusPrefix is designed for BitmapFontIcon status icons, whose colors
            // are baked into their textures. Name text honors foreground/glow payloads.
            var baseName = StripVelvetRopeNameCrowns(handler.Name);

            var isPreview = previewActive && handler.GameObjectId == previewCrownObjectId;
            var badgeInfo = new NameplateBadgeInfo(true, VipDuration.Lifetime, Guid.Empty);
            var hasVipBadge = normalBadgesActive && visibleVipBadges.TryGetValue(handler.GameObjectId, out badgeInfo);

            if (hasVipBadge && !isPreview && localPosition.HasValue &&
                System.Numerics.Vector3.Distance(localPosition.Value, player.Position) > maxDistance)
            {
                hasVipBadge = false;
            }

            // Keep the old 0.3.7 status marker cleaned up regardless of whether a
            // crown should currently be visible.
            if (!SameEncodedString(basePrefix, handler.StatusPrefix))
                handler.StatusPrefix = basePrefix;

            if (!isPreview && !hasVipBadge)
            {
                if (!SameEncodedString(baseName, handler.Name))
                    handler.Name = baseName;
                continue;
            }

            var marker = isPreview
                ? GetVipNameCrownPrefix(VipDuration.Lifetime)
                : badgeInfo.UseVipTiering
                    ? GetVipNameCrownPrefix(badgeInfo.Duration)
                    : GetCategoryNameMarkerPrefix(GetCategory(badgeInfo.CategoryId));

            handler.Name = new SeStringBuilder()
                .Append(marker)
                .Append(baseName)
                .Build();
        }
    }

    private static SeString GetVipNameCrownPrefix(VipDuration duration)
    {
        var foreground = duration switch
        {
            VipDuration.Nightly => (ushort)51,   // near-black / charcoal
            VipDuration.Monthly => (ushort)13,   // copper / warm orange
            VipDuration.Yearly => (ushort)2,     // silver / light neutral
            VipDuration.Lifetime => (ushort)548, // gold
            _ => (ushort)548
        };

        // Diamonds identify the three renewable tiers. Lifetime gets a star so it
        // remains visually special even before the player notices the gold color.
        // These are ordinary text glyphs, so FFXIV honors the UIColor payloads.
        var marker = duration == VipDuration.Lifetime ? "★" : "◆";
        var glow = duration == VipDuration.Nightly ? (ushort)2 : (ushort)51;

        return new SeStringBuilder()
            .AddUiForeground(foreground)
            .AddUiGlow(glow)
            .AddText(marker)
            .AddUiGlowOff()
            .AddUiForegroundOff()
            .AddText(" ")
            .Build();
    }

    private static SeString GetCategoryNameMarkerPrefix(VipCategory category)
    {
        var marker = string.IsNullOrWhiteSpace(category.Icon) ? "●" : category.Icon.Trim();

        // Category-only relationships intentionally use one restrained neutral
        // treatment. Their shape communicates the role; color is reserved for VIP tiers.
        return new SeStringBuilder()
            .AddUiForeground(2)
            .AddUiGlow(51)
            .AddText(marker)
            .AddUiGlowOff()
            .AddUiForegroundOff()
            .AddText(" ")
            .Build();
    }

    private static SeString GetLegacyVipNameCrownPrefix(VipDuration duration)
    {
        // Exact 0.3.8 marker sequence. Strip it during redraw so upgrading users
        // do not keep the unsupported crown glyph beside the new diamond/star.
        var foreground = duration switch
        {
            VipDuration.Nightly => (ushort)51,
            VipDuration.Monthly => (ushort)13,
            VipDuration.Yearly => (ushort)2,
            VipDuration.Lifetime => (ushort)548,
            _ => (ushort)548
        };
        var glow = duration == VipDuration.Nightly ? (ushort)2 : (ushort)51;

        return new SeStringBuilder()
            .AddUiForeground(foreground)
            .AddUiGlow(glow)
            .AddText("♛")
            .AddUiGlowOff()
            .AddUiForegroundOff()
            .AddText(" ")
            .Build();
    }

    private static SeString GetLegacyVipStatusCrown(VipDuration duration)
    {
        // Exact 0.3.7 sequence. BitmapFontIcon.Mentor itself is baked gold even
        // though these foreground/glow payloads were present around it.
        var foreground = duration switch
        {
            VipDuration.Nightly => (ushort)51,
            VipDuration.Monthly => (ushort)13,
            VipDuration.Yearly => (ushort)2,
            VipDuration.Lifetime => (ushort)548,
            _ => (ushort)548
        };
        var glow = duration == VipDuration.Nightly ? (ushort)2 : (ushort)51;

        return new SeStringBuilder()
            .AddUiForeground(foreground)
            .AddUiGlow(glow)
            .AddIcon(BitmapFontIcon.Mentor)
            .AddUiGlowOff()
            .AddUiForegroundOff()
            .Build();
    }

    private static SeString StripVelvetRopeStatusCrowns(SeString prefix)
    {
        var bytes = prefix.Encode();
        if (bytes.Length == 0)
            return prefix;

        var changed = false;
        foreach (var duration in new[] { VipDuration.Nightly, VipDuration.Monthly, VipDuration.Yearly, VipDuration.Lifetime })
        {
            bytes = RemoveByteSequence(bytes, GetLegacyVipStatusCrown(duration).Encode(), out var removed);
            changed |= removed;
        }

        return changed ? SeString.Parse(bytes) : prefix;
    }

    private SeString StripVelvetRopeNameCrowns(SeString name)
    {
        var bytes = name.Encode();
        if (bytes.Length == 0)
            return name;

        var changed = false;
        foreach (var duration in new[] { VipDuration.Nightly, VipDuration.Monthly, VipDuration.Yearly, VipDuration.Lifetime })
        {
            bytes = RemoveByteSequence(bytes, GetLegacyVipNameCrownPrefix(duration).Encode(), out var removedLegacy);
            changed |= removedLegacy;

            bytes = RemoveByteSequence(bytes, GetVipNameCrownPrefix(duration).Encode(), out var removedCurrent);
            changed |= removedCurrent;
        }

        foreach (var category in Configuration.Categories)
        {
            bytes = RemoveByteSequence(bytes, GetCategoryNameMarkerPrefix(category).Encode(), out var removedCategory);
            changed |= removedCategory;
        }

        return changed ? SeString.Parse(bytes) : name;
    }

    private static byte[] RemoveByteSequence(byte[] source, byte[] sequence, out bool removed)
    {
        removed = false;
        if (source.Length == 0 || sequence.Length == 0 || source.Length < sequence.Length)
            return source;

        var output = new List<byte>(source.Length);
        var index = 0;
        while (index < source.Length)
        {
            var matches = index + sequence.Length <= source.Length;
            if (matches)
            {
                for (var i = 0; i < sequence.Length; i++)
                {
                    if (source[index + i] == sequence[i])
                        continue;
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                removed = true;
                index += sequence.Length;
                continue;
            }

            output.Add(source[index]);
            index++;
        }

        return removed ? output.ToArray() : source;
    }

    private static bool SameEncodedString(SeString left, SeString right) =>
        left.Encode().AsSpan().SequenceEqual(right.Encode());

    public void RefreshVipNameplates() => NamePlateGui.RequestRedraw();

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "start":
            case "on":
                StartShift();
                break;
            case "end":
            case "stop":
            case "off":
                EndShift();
                break;
            case "reset":
                ResetVipPresenceTracking();
                if (ActiveSession is not null)
                    seedVipPresenceNextScan = true;
                ShowNormalToast("Velvet Rope: VIP presence tracking reset.");
                break;
            default:
                ToggleMainUi();
                break;
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        var now = DateTime.UtcNow;
        if (previewCrownObjectId != 0 && now >= previewCrownUntilUtc)
        {
            previewCrownObjectId = 0;
            NamePlateGui.RequestRedraw();
        }

        if (ActiveSession is null)
            return;

        if (now < nextScanUtc)
            return;

        nextScanUtc = now + ScanInterval;
        ScanActiveSession(now);
    }

    private void ScanActiveSession(DateTime nowUtc)
    {
        var session = ActiveSession;
        if (session is null)
            return;

        var venue = Configuration.Venues.FirstOrDefault(v => v.Id == session.VenueId);
        if (venue is null)
            return;

        try
        {
            var localPlayer = ObjectTable.LocalPlayer;
            var localName = localPlayer?.Name.TextValue.Trim() ?? string.Empty;
            var localWorld = localPlayer is not null && localPlayer.HomeWorld.IsValid
                ? localPlayer.HomeWorld.Value.Name.ToString().Trim()
                : string.Empty;

            var visibleGuests = new List<(ulong ObjectId, string Name, string World)>();

            foreach (var obj in ObjectTable.PlayerObjects)
            {
                if (obj is not IPlayerCharacter player)
                    continue;

                var name = player.Name.TextValue.Trim();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var world = player.HomeWorld.IsValid
                    ? player.HomeWorld.Value.Name.ToString().Trim()
                    : string.Empty;

                if (SameIdentity(name, world, localName, localWorld))
                    continue;

                visibleGuests.Add((player.GameObjectId, name, world));
                session.ObserveGuest(name, world, nowUtc);
            }

            session.ObserveVisibleGuestCount(visibleGuests.Count);
            ScanVipArrivals(venue, visibleGuests, nowUtc);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while scanning active Velvet Rope session.");
        }
    }

    private void ScanVipArrivals(
        VenueProfile venue,
        List<(ulong ObjectId, string Name, string World)> visibleGuests,
        DateTime nowUtc)
    {
        var enabledLinks = venue.Vips
            .Where(v => v.Enabled && !v.IsExpired(nowUtc))
            .ToList();
        var currentMatches = new Dictionary<Guid, (VenueVipEntry Link, PersonEntry Person, ulong ObjectId, string Name, string World)>();

        foreach (var link in enabledLinks)
        {
            var person = GetPerson(link.PersonId);
            if (person is null || !person.Enabled || string.IsNullOrWhiteSpace(person.Name))
                continue;

            var match = visibleGuests.FirstOrDefault(g => Matches(person, g.Name, g.World));
            if (string.IsNullOrWhiteSpace(match.Name))
                continue;

            currentMatches[link.Id] = (link, person, match.ObjectId, match.Name, match.World);
        }

        var currentIds = currentMatches.Keys.ToHashSet();

        var nextBadges = currentMatches.Values
            .GroupBy(match => match.ObjectId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var link = group.First().Link;
                    return new NameplateBadgeInfo(link.UseVipTiering, link.Duration, link.CategoryId);
                });

        var badgeSetChanged = nextBadges.Count != visibleVipBadges.Count ||
            nextBadges.Any(pair => !visibleVipBadges.TryGetValue(pair.Key, out var currentBadge) || currentBadge != pair.Value);

        if (badgeSetChanged)
        {
            visibleVipBadges.Clear();
            foreach (var pair in nextBadges)
                visibleVipBadges[pair.Key] = pair.Value;
            NamePlateGui.RequestRedraw();
        }

        if (seedVipPresenceNextScan)
        {
            presentVipLinksLastScan.Clear();
            presentVipLinksLastScan.UnionWith(currentIds);
            absentVipLinksSince.Clear();
            seedVipPresenceNextScan = false;
            Log.Debug("Seeded {Count} currently visible VIP(s) for {Venue}.", currentIds.Count, venue.Name);
            return;
        }

        foreach (var departedId in presentVipLinksLastScan.Where(id => !currentIds.Contains(id)))
            absentVipLinksSince.TryAdd(departedId, nowUtc);

        foreach (var id in currentIds)
        {
            if (presentVipLinksLastScan.Contains(id))
                continue;

            var shouldAlert = true;
            if (absentVipLinksSince.TryGetValue(id, out var leftAt))
            {
                shouldAlert = (nowUtc - leftAt).TotalSeconds >= Math.Max(0, Configuration.ReentryGraceSeconds);
                absentVipLinksSince.Remove(id);
            }

            if (!shouldAlert)
                continue;

            var match = currentMatches[id];
            var alert = BuildAlert(venue, match.Link, match.Person, match.Name, match.World);
            EnqueueAlert(alert, countForSession: true);
        }

        presentVipLinksLastScan.Clear();
        presentVipLinksLastScan.UnionWith(currentIds);
    }

    private ArrivalAlert BuildAlert(
        VenueProfile venue,
        VenueVipEntry link,
        PersonEntry person,
        string detectedName,
        string detectedWorld)
    {
        var category = GetCategory(link.CategoryId);
        var template = ChooseTemplate(venue, link, category);

        var publicMessage = template
            .Replace("{name}", detectedName, StringComparison.OrdinalIgnoreCase)
            .Replace("{world}", detectedWorld, StringComparison.OrdinalIgnoreCase)
            .Replace("{venue}", venue.Name, StringComparison.OrdinalIgnoreCase)
            .Replace("{category}", category.Name, StringComparison.OrdinalIgnoreCase);

        var publicAnnouncementEnabled = link.PreparePublicShout;
        var message = publicAnnouncementEnabled
            ? publicMessage
            : $"{detectedName} is marked for silent recognition at {venue.Name}. Staff recognition only; no public shout is prepared.";
        var copyText = publicAnnouncementEnabled
            ? (publicMessage.StartsWith('/') ? publicMessage : $"/sh {publicMessage}")
            : string.Empty;

        return new ArrivalAlert(
            detectedName,
            detectedWorld,
            venue.Name,
            category.Name,
            category.Icon,
            category.AccentR,
            category.AccentG,
            category.AccentB,
            category.AccentA,
            message,
            copyText,
            publicAnnouncementEnabled);
    }

    private string ChooseTemplate(VenueProfile venue, VenueVipEntry link, VipCategory category)
    {
        var variants = link.ShoutVariants
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .ToList();

        if (variants.Count > 0)
        {
            var index = random.Next(variants.Count);

            if (variants.Count > 1 &&
                lastVariantByLink.TryGetValue(link.Id, out var previous) &&
                index == previous)
            {
                index = (index + 1 + random.Next(variants.Count - 1)) % variants.Count;
            }

            lastVariantByLink[link.Id] = index;
            return variants[index];
        }

        if (!string.IsNullOrWhiteSpace(venue.DefaultShoutTemplate))
            return venue.DefaultShoutTemplate.Trim();

        if (!string.IsNullOrWhiteSpace(category.DefaultShoutTemplate))
            return category.DefaultShoutTemplate.Trim();

        return "Everyone give a warm welcome to {name}!";
    }

    private void EnqueueAlert(ArrivalAlert alert, bool countForSession)
    {
        if (countForSession && ActiveSession is not null)
        {
            ActiveSession.RecordVipArrival(new VipArrivalRecord(
                DateTime.UtcNow,
                alert.CharacterName,
                alert.HomeWorld,
                alert.CategoryName));
        }

        ShowNormalToast($"{alert.CategoryIcon} {alert.CategoryName} detected: {alert.CharacterDisplay}");
        PlayAlertSoundIfEnabled();
        Log.Information("{Category} detected: {Character} at {Venue}.",
            alert.CategoryName,
            alert.CharacterDisplay,
            alert.VenueName);

        if (CurrentAlert is null)
        {
            CurrentAlert = alert;
            arrivalWindow.IsOpen = true;
            arrivalWindow.RequestFocus = true;
            return;
        }

        alertQueue.Enqueue(alert);
    }

    private void ShowNormalToast(string message)
    {
        if (Configuration.NativeToastEnabled)
            ToastGui.ShowNormal(message);
    }

    private void PlayAlertSoundIfEnabled()
    {
        if (!Configuration.AlertSoundEnabled)
            return;

        try
        {
            MessageBeep(0x00000040); // MB_ICONASTERISK
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not play Velvet Rope alert sound.");
        }
    }

    private void ResetVipPresenceTracking()
    {
        presentVipLinksLastScan.Clear();
        absentVipLinksSince.Clear();
        seedVipPresenceNextScan = false;
        if (visibleVipBadges.Count > 0)
        {
            visibleVipBadges.Clear();
            NamePlateGui.RequestRedraw();
        }
    }

    private void ClearAlerts()
    {
        alertQueue.Clear();
        CurrentAlert = null;
        arrivalWindow.IsOpen = false;
    }

    private string ImportVipDatabase(TransferEnvelope envelope)
    {
        var addedPeople = 0;
        var addedCategories = MergeCategories(envelope.Categories, out _);

        foreach (var incoming in envelope.People)
        {
            if (string.IsNullOrWhiteSpace(incoming.Name))
                continue;

            if (FindPersonByIdentity(incoming.Name, incoming.World) is not null)
                continue;

            Configuration.People.Add(ClonePersonWithNewId(incoming));
            addedPeople++;
        }

        Configuration.Save();
        return $"Imported {addedPeople} people and {addedCategories} categories.";
    }

    private string ImportVenuePack(TransferEnvelope envelope)
    {
        if (envelope.Venue is null)
            return "This venue pack does not contain a venue profile.";

        var categoryMap = new Dictionary<Guid, Guid>();
        MergeCategories(envelope.Categories, out categoryMap);

        var personMap = new Dictionary<Guid, Guid>();
        foreach (var incoming in envelope.People)
        {
            if (string.IsNullOrWhiteSpace(incoming.Name))
                continue;

            var existing = FindPersonByIdentity(incoming.Name, incoming.World);
            if (existing is null)
            {
                existing = ClonePersonWithNewId(incoming);
                Configuration.People.Add(existing);
            }

            personMap[incoming.Id] = existing.Id;
        }

        var importedVenue = VenueProfile.CreateDefault(MakeUniqueVenueName(envelope.Venue.Name));
        importedVenue.DefaultShoutTemplate = envelope.Venue.DefaultShoutTemplate;
        importedVenue.NightlyBenefits = envelope.Venue.NightlyBenefits;
        importedVenue.MonthlyBenefits = envelope.Venue.MonthlyBenefits;
        importedVenue.YearlyBenefits = envelope.Venue.YearlyBenefits;
        importedVenue.LifetimeBenefits = envelope.Venue.LifetimeBenefits;
        importedVenue.Enabled = envelope.Venue.Enabled;

        foreach (var incomingLink in envelope.Venue.Vips)
        {
            if (!personMap.TryGetValue(incomingLink.PersonId, out var newPersonId))
                continue;

            var categoryId = categoryMap.TryGetValue(incomingLink.CategoryId, out var mapped)
                ? mapped
                : Configuration.Categories[0].Id;

            importedVenue.Vips.Add(new VenueVipEntry
            {
                PersonId = newPersonId,
                Enabled = incomingLink.Enabled,
                CategoryId = categoryId,
                PreparePublicShout = incomingLink.PreparePublicShout,
                UseVipTiering = incomingLink.UseVipTiering,
                Duration = incomingLink.Duration,
                AddedAtUtc = incomingLink.AddedAtUtc == default ? DateTime.UtcNow : incomingLink.AddedAtUtc,
                ShoutVariants = incomingLink.ShoutVariants
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Select(v => v.Trim())
                    .ToList()
            });
        }

        Configuration.Venues.Add(importedVenue);
        if (ActiveSession is null)
            Configuration.SelectedVenueId = importedVenue.Id;

        Configuration.Save();
        return $"Imported venue '{importedVenue.Name}' with {importedVenue.Vips.Count} VIP entries.";
    }

    private int MergeCategories(
        IEnumerable<VipCategory> incomingCategories,
        out Dictionary<Guid, Guid> idMap)
    {
        var added = 0;
        idMap = new Dictionary<Guid, Guid>();

        foreach (var incoming in incomingCategories)
        {
            if (string.IsNullOrWhiteSpace(incoming.Name))
                continue;

            var existing = Configuration.Categories.FirstOrDefault(c =>
                string.Equals(c.Name.Trim(), incoming.Name.Trim(), StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                existing = CloneCategoryWithNewId(incoming);
                Configuration.Categories.Add(existing);
                added++;
            }

            idMap[incoming.Id] = existing.Id;
        }

        return added;
    }

    private PersonEntry? FindPersonByIdentity(string name, string world) =>
        Configuration.People.FirstOrDefault(p =>
            string.Equals(p.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.World.Trim(), world.Trim(), StringComparison.OrdinalIgnoreCase));

    private string MakeUniqueVenueName(string requested)
    {
        var baseName = string.IsNullOrWhiteSpace(requested) ? "Imported Venue" : requested.Trim();
        if (Configuration.Venues.All(v => !string.Equals(v.Name, baseName, StringComparison.OrdinalIgnoreCase)))
            return baseName;

        var i = 2;
        while (Configuration.Venues.Any(v =>
                   string.Equals(v.Name, $"{baseName} ({i})", StringComparison.OrdinalIgnoreCase)))
            i++;

        return $"{baseName} ({i})";
    }

    private static bool SameIdentity(
        string nameA,
        string worldA,
        string nameB,
        string worldB) =>
        !string.IsNullOrWhiteSpace(nameB) &&
        string.Equals(nameA, nameB, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(worldA, worldB, StringComparison.OrdinalIgnoreCase);

    private static bool Matches(PersonEntry person, string playerName, string homeWorld)
    {
        if (!string.Equals(person.Name.Trim(), playerName, StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.IsNullOrWhiteSpace(person.World))
            return true;

        return string.Equals(person.World.Trim(), homeWorld, StringComparison.OrdinalIgnoreCase);
    }

    private static PersonEntry ClonePerson(PersonEntry p) => new()
    {
        Id = p.Id,
        Enabled = p.Enabled,
        Name = p.Name,
        World = p.World,
        Notes = string.Empty
    };

    private static PersonEntry ClonePersonWithNewId(PersonEntry p) => new()
    {
        Enabled = p.Enabled,
        Name = p.Name,
        World = p.World,
        Notes = p.Notes
    };

    private static VipCategory CloneCategory(VipCategory c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Icon = c.Icon,
        DefaultShoutTemplate = c.DefaultShoutTemplate,
        AccentR = c.AccentR,
        AccentG = c.AccentG,
        AccentB = c.AccentB,
        AccentA = c.AccentA,
        BuiltIn = c.BuiltIn
    };

    private static VipCategory CloneCategoryWithNewId(VipCategory c) => new()
    {
        Name = c.Name,
        Icon = c.Icon,
        DefaultShoutTemplate = c.DefaultShoutTemplate,
        AccentR = c.AccentR,
        AccentG = c.AccentG,
        AccentB = c.AccentB,
        AccentA = c.AccentA,
        BuiltIn = false
    };

    private static VenueProfile CloneVenue(VenueProfile v) => new()
    {
        Id = v.Id,
        Name = v.Name,
        Enabled = v.Enabled,
        DefaultShoutTemplate = v.DefaultShoutTemplate,
        NightlyBenefits = v.NightlyBenefits,
        MonthlyBenefits = v.MonthlyBenefits,
        YearlyBenefits = v.YearlyBenefits,
        LifetimeBenefits = v.LifetimeBenefits,
        Vips = v.Vips.Select(link => new VenueVipEntry
        {
            Id = link.Id,
            PersonId = link.PersonId,
            Enabled = link.Enabled,
            CategoryId = link.CategoryId,
            PreparePublicShout = link.PreparePublicShout,
            UseVipTiering = link.UseVipTiering,
            Duration = link.Duration,
            AddedAtUtc = link.AddedAtUtc,
            ShoutVariants = link.ShoutVariants.ToList()
        }).ToList()
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    [DllImport("user32.dll")]
    private static extern bool MessageBeep(uint uType);
}
