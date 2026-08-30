using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.ImGuiFileDialog;

namespace VelvetRope.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private const string TargetVipPopupId = "Add Target to VIP###VelvetRopeTargetVip";
    private const string TierBenefitsPopupId = "VIP Tier Reference###VelvetRopeTierBenefits";
    private const string TierBenefitsEditorPopupId = "Edit VIP Tier Benefits###VelvetRopeTierBenefitsEditor";

    private readonly Plugin plugin;
    private readonly FileDialogManager uiPackDialogs = new();

    private string newVenueName = string.Empty;

    private string newVipName = string.Empty;
    private string newVipWorld = string.Empty;
    private string newVipVariant = string.Empty;
    private Guid newVipCategoryId = Guid.Empty;
    private bool newVipUseTiering = true;
    private VipDuration newVipDuration = VipDuration.Lifetime;
    private Guid assignExistingPersonId = Guid.Empty;
    private bool assignExistingUseTiering = true;

    private bool requestTargetVipPopup;
    private string targetVipName = string.Empty;
    private string targetVipWorld = string.Empty;
    private Guid targetVipCategoryId = Guid.Empty;
    private bool targetVipUseTiering = true;
    private VipDuration targetVipDuration = VipDuration.Nightly;
    private Guid targetVipExistingLinkId = Guid.Empty;
    private string targetVipFeedback = string.Empty;

    private bool requestTierBenefitsPopup;
    private bool requestTierBenefitsEditorPopup;
    private Guid tierBenefitsEditorVenueId = Guid.Empty;
    private string tierBenefitsFeedback = string.Empty;

    private string vipSearch = string.Empty;

    private string newCategoryName = string.Empty;
    private string newCategoryIcon = "★";
    private string newCategoryTemplate = "Welcome {name} to {venue}! ♥";

    private string transferStatus = string.Empty;
    private string uiPackStatus = string.Empty;
    private int selectedPage;
    private bool sidebarResizeDirty;

    public MainWindow(Plugin plugin)
        : base("Velvet Rope 0.3.10###VelvetRopeMain")
    {
        this.plugin = plugin;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(920, 620),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void Dispose() { }

    public void OpenVipDirectory(string search = "")
    {
        selectedPage = 1;
        vipSearch = search ?? string.Empty;
        IsOpen = true;
        RequestFocus = true;
    }

    public void OpenAddPlayerVip(string name, string world)
    {
        selectedPage = 1;
        IsOpen = true;
        RequestFocus = true;
        PreparePlayerVipPopup(plugin.SelectedVenue, name, world);
    }

    public override void Draw()
    {
        VelvetStyle.Apply(plugin.Configuration.UiTheme);
        VelvetStyle.PushChrome();

        DrawHeader();
        ImGui.Spacing();

        const float splitterWidth = 7f;
        const float minimumSidebarWidth = 220f;
        const float maximumSidebarWidth = 420f;
        const float minimumContentWidth = 560f;

        var availableWidth = ImGui.GetContentRegionAvail().X;
        var navMinimum = ImGui.CalcTextSize("★  VIP Directory").X + 48f;
        var requestedSidebarWidth = Math.Max(plugin.Configuration.UiTheme.SidebarWidth, navMinimum);
        var responsiveMax = Math.Max(
            minimumSidebarWidth,
            Math.Min(maximumSidebarWidth, availableWidth - minimumContentWidth - splitterWidth));
        var sidebarWidth = Math.Clamp(requestedSidebarWidth, minimumSidebarWidth, responsiveMax);

        ImGui.BeginChild("##VelvetSidebar", new Vector2(sidebarWidth, -1), true);
        DrawSidebar();
        ImGui.EndChild();

        ImGui.SameLine(0f, 0f);
        DrawSidebarResizeHandle(sidebarWidth, minimumSidebarWidth, maximumSidebarWidth);
        ImGui.SameLine(0f, 0f);

        ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.Background);
        ImGui.BeginChild("##VelvetContent", new Vector2(0, -1), false);

        // Reserve a small, fixed footer on every page for the privacy promise.
        // The actual page gets its own scroll region so the footer never scrolls away.
        var footerHeight = ImGui.GetTextLineHeightWithSpacing() + 22f;
        var pageHeight = Math.Max(140f, ImGui.GetContentRegionAvail().Y - footerHeight - ImGui.GetStyle().ItemSpacing.Y);

        ImGui.BeginChild("##VelvetPageBody", new Vector2(0, pageHeight), false);
        switch (selectedPage)
        {
            case 0:
                DrawDashboard();
                break;
            case 1:
                DrawVips();
                break;
            case 2:
                DrawVenues();
                break;
            case 3:
                DrawReports();
                break;
            default:
                DrawSettings();
                break;
        }
        ImGui.EndChild();

        DrawPrivacyFooter();

        ImGui.EndChild();
        ImGui.PopStyleColor();

        if (requestTargetVipPopup)
        {
            ImGui.OpenPopup(TargetVipPopupId);
            requestTargetVipPopup = false;
        }

        DrawTargetVipPopup();

        if (requestTierBenefitsPopup)
        {
            ImGui.OpenPopup(TierBenefitsPopupId);
            requestTierBenefitsPopup = false;
        }

        if (requestTierBenefitsEditorPopup)
        {
            ImGui.OpenPopup(TierBenefitsEditorPopupId);
            requestTierBenefitsEditorPopup = false;
        }

        DrawTierBenefitsPopup();
        DrawTierBenefitsEditorPopup();

        VelvetStyle.PopChrome();
        uiPackDialogs.Draw();
    }

    private void DrawHeader()
    {
        var active = plugin.ActiveSession is not null;
        var status = active ? "● SHIFT ACTIVE" : "○ SHIFT OFF";
        var statusColor = active ? VelvetStyle.Green : VelvetStyle.Muted;
        var theme = plugin.Configuration.UiTheme;
        var headerHeight = Math.Max(theme.HeaderHeight, VelvetStyle.PanelHeight(2, extra: 8));

        ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.CardRaised);
        ImGui.PushStyleColor(ImGuiCol.Border, VelvetStyle.GoldDim);
        ImGui.BeginChild("##VelvetBrandHeader", new Vector2(-1, headerHeight), true);

        var windowPos = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();
        var drawList = ImGui.GetWindowDrawList();

        if (theme.ShowHeaderBackground)
        {
            var background = plugin.GetUiAssetTexture(theme.HeaderBackgroundAsset);
            if (background is not null)
            {
                var tint = new Vector4(1f, 1f, 1f, theme.HeaderBackgroundOpacity);
                drawList.AddImage(
                    background.Handle,
                    windowPos + Vector2.One,
                    windowPos + windowSize - Vector2.One,
                    Vector2.Zero,
                    Vector2.One,
                    ImGui.ColorConvertFloat4ToU32(tint));

                var overlay = theme.Background.ToVector4();
                overlay.W = theme.HeaderOverlayOpacity;
                drawList.AddRectFilled(
                    windowPos + Vector2.One,
                    windowPos + windowSize - Vector2.One,
                    ImGui.ColorConvertFloat4ToU32(overlay),
                    theme.CornerRounding);
            }
        }

        var contentX = 14f;
        var logo = theme.ShowHeaderLogo ? plugin.GetUiAssetTexture(theme.LogoAsset) : null;
        if (logo is not null && logo.Height > 0)
        {
            var logoHeight = Math.Min(theme.HeaderLogoHeight, headerHeight - 14f);
            var logoWidth = logoHeight * logo.Width / (float)logo.Height;
            ImGui.SetCursorPos(new Vector2(14f, Math.Max(7f, (headerHeight - logoHeight) * 0.5f)));
            ImGui.Image(
                logo.Handle,
                new Vector2(logoWidth, logoHeight),
                Vector2.Zero,
                Vector2.One,
                new Vector4(1f, 1f, 1f, theme.LogoOpacity));
            contentX += logoWidth + 14f;
        }

        var statusWidth = ImGui.CalcTextSize(status).X;
        ImGui.SetCursorPos(new Vector2(
            Math.Max(contentX, windowSize.X - statusWidth - 18f),
            12f));
        ImGui.TextColored(statusColor, status);

        ImGui.SetCursorPos(new Vector2(contentX, 13f));
        if (theme.ShowBrandTitle)
        {
            ImGui.TextColored(VelvetStyle.Gold, $"{theme.BrandMark}  {theme.BrandTitle}");
            ImGui.SameLine();
            ImGui.TextDisabled("0.3.10");
        }
        else
        {
            ImGui.TextDisabled($"Velvet Rope 0.3.10 · {theme.PackName}");
        }

        if (theme.ShowBrandTagline)
        {
            ImGui.SetCursorPos(new Vector2(contentX, 13f + ImGui.GetTextLineHeightWithSpacing() + 5f));
            ImGui.TextColored(VelvetStyle.Muted, theme.Tagline);
        }

        drawList.AddRect(
            windowPos,
            windowPos + windowSize,
            ImGui.ColorConvertFloat4ToU32(VelvetStyle.GoldDim),
            theme.CornerRounding);

        ImGui.EndChild();
        ImGui.PopStyleColor(2);
    }

    private void DrawSidebar()
    {
        ImGui.TextColored(VelvetStyle.Gold, "CONTROL DESK");
        ImGui.TextDisabled("Choose a workspace");

        var theme = plugin.Configuration.UiTheme;
        var sidebarLogo = theme.ShowSidebarLogo ? plugin.GetUiAssetTexture(theme.LogoAsset) : null;
        if (sidebarLogo is not null && sidebarLogo.Height > 0)
        {
            ImGui.Spacing();
            var logoHeight = theme.SidebarLogoHeight;
            var logoWidth = logoHeight * sidebarLogo.Width / (float)sidebarLogo.Height;
            var available = ImGui.GetContentRegionAvail().X;
            if (logoWidth > available)
            {
                logoWidth = available;
                logoHeight = logoWidth * sidebarLogo.Height / (float)sidebarLogo.Width;
            }
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (available - logoWidth) * 0.5f));
            ImGui.Image(sidebarLogo.Handle, new Vector2(logoWidth, logoHeight), Vector2.Zero, Vector2.One, new Vector4(1f, 1f, 1f, theme.LogoOpacity));
        }

        ImGui.Spacing();

        DrawNavButton("◆  Dashboard", 0);
        DrawNavButton("★  VIP Directory", 1);
        DrawNavButton("♛  Venues", 2);
        DrawNavButton("▦  Reports", 3);
        DrawNavButton("●  Settings", 4);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var sidebarVenue = plugin.SelectedVenue;
        var session = plugin.ActiveSession;

        ImGui.TextDisabled("CURRENT VENUE");
        ImGui.TextColored(VelvetStyle.Ivory, sidebarVenue.Name);
        if (session is not null)
            ImGui.TextColored(VelvetStyle.Green, $"● LIVE · {Plugin.FormatDuration(session.Elapsed(DateTime.UtcNow))}");
        else
            ImGui.TextColored(VelvetStyle.Muted, "○ No active shift");

        ImGui.Spacing();
        var configuredTierBenefits = new[]
        {
            sidebarVenue.NightlyBenefits,
            sidebarVenue.MonthlyBenefits,
            sidebarVenue.YearlyBenefits,
            sidebarVenue.LifetimeBenefits
        }.Count(value => !string.IsNullOrWhiteSpace(value));

        ImGui.TextDisabled($"VIP TIERS · {configuredTierBenefits}/4 configured");
        VelvetStyle.PushAccentButton(VelvetStyle.Blue);
        if (ImGui.Button("♛  TIER REFERENCE", new Vector2(-1, VelvetStyle.ControlHeight(8))))
        {
            tierBenefitsFeedback = string.Empty;
            requestTierBenefitsPopup = true;
        }
        VelvetStyle.PopAccentButton();
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text($"VIP benefits for {sidebarVenue.Name}");
            ImGui.TextDisabled("Quickly check what Nightly, Monthly, Yearly, and Lifetime status includes.");
            ImGui.EndTooltip();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextDisabled("APPEARANCE");
        ImGui.TextWrapped(theme.PackName);
        if (ImGui.Button("IMPORT UI PACK", new Vector2(-1, VelvetStyle.ControlHeight(8))))
            OpenUiPackImportDialog();
        DrawCompactUiPackHelp();

        ImGui.Spacing();
        ImGui.TextColored(VelvetStyle.Muted, "Drag the right edge to resize");
    }

    private void DrawSidebarResizeHandle(float displayedWidth, float minWidth, float maxWidth)
    {
        var height = Math.Max(40f, ImGui.GetContentRegionAvail().Y);
        var size = new Vector2(7f, height);
        var start = ImGui.GetCursorScreenPos();

        ImGui.InvisibleButton("##VelvetSidebarResize", size);
        var hovered = ImGui.IsItemHovered();
        var active = ImGui.IsItemActive();

        var lineColor = active
            ? VelvetStyle.Gold
            : hovered ? VelvetStyle.GoldDim : VelvetStyle.WithAlpha(VelvetStyle.GoldDim, 0.35f);
        var x = start.X + size.X * 0.5f;
        ImGui.GetWindowDrawList().AddLine(
            new Vector2(x, start.Y + 4f),
            new Vector2(x, start.Y + size.Y - 4f),
            ImGui.ColorConvertFloat4ToU32(lineColor),
            active || hovered ? 2f : 1f);

        if (active)
        {
            var delta = ImGui.GetIO().MouseDelta.X;
            if (Math.Abs(delta) > 0.001f)
            {
                plugin.Configuration.UiTheme.SidebarWidth = Math.Clamp(displayedWidth + delta, minWidth, maxWidth);
                sidebarResizeDirty = true;
            }
        }
        else if (sidebarResizeDirty)
        {
            SaveUiTheme();
            sidebarResizeDirty = false;
        }

        if (hovered && !active)
        {
            ImGui.BeginTooltip();
            ImGui.Text("Drag to resize the Control Desk");
            ImGui.TextDisabled("The width is remembered with your current appearance settings.");
            ImGui.EndTooltip();
        }
    }

    private void DrawNavButton(string label, int page)
    {
        var selected = selectedPage == page;
        if (selected)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, VelvetStyle.Velvet);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, VelvetStyle.VelvetBright);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, VelvetStyle.VelvetBright);
            ImGui.PushStyleColor(ImGuiCol.Text, VelvetStyle.Gold);
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Button, VelvetStyle.Card);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, VelvetStyle.CardRaised);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, VelvetStyle.Velvet);
            ImGui.PushStyleColor(ImGuiCol.Text, VelvetStyle.Ivory);
        }

        if (ImGui.Button(label, new Vector2(-1, VelvetStyle.ControlHeight(10))))
            selectedPage = page;

        ImGui.PopStyleColor(4);
    }

    private static void DrawPageTitle(string title, string subtitle)
    {
        ImGui.TextColored(VelvetStyle.Gold, title);
        ImGui.TextColored(VelvetStyle.Muted, subtitle);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void DrawDashboard()
    {
        DrawPageTitle(
            "TONIGHT'S FLOOR",
            "Run the shift, watch VIP arrivals, and see attendance at a glance.");

        ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.CardRaised);
        ImGui.PushStyleColor(ImGuiCol.Border, VelvetStyle.GoldDim);
        ImGui.BeginChild("##shiftControl", new Vector2(-1, VelvetStyle.PanelHeight(1, 1, 18)), true);

        ImGui.TextColored(VelvetStyle.Gold, "VENUE PROFILE");
        DrawVenueSelector();

        ImGui.SameLine();
        if (plugin.ActiveSession is null)
        {
            VelvetStyle.PushAccentButton(VelvetStyle.VelvetBright);
            if (ImGui.Button("START SHIFT", VelvetStyle.ButtonSize("START SHIFT", 150)))
                plugin.StartShift();
            VelvetStyle.PopAccentButton();
            ImGui.SameLine();
            ImGui.TextDisabled("Anonymous attendance starts when the shift begins.");
        }
        else
        {
            VelvetStyle.PushAccentButton(VelvetStyle.Gold);
            if (ImGui.Button("END SHIFT", VelvetStyle.ButtonSize("END SHIFT", 150)))
                plugin.EndShift();
            VelvetStyle.PopAccentButton();
            ImGui.SameLine();
            ImGui.TextColored(VelvetStyle.Green, "● Tracking live");
        }

        ImGui.EndChild();
        ImGui.PopStyleColor(2);
        ImGui.Spacing();

        var session = plugin.ActiveSession;
        if (session is null)
        {
            ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.Card);
            ImGui.BeginChild("##idleWelcome", new Vector2(-1, VelvetStyle.PanelHeight(4, extra: 12)), true);
            ImGui.TextColored(VelvetStyle.Pink, "THE ROPE IS CLOSED");
            ImGui.Spacing();
            ImGui.TextWrapped(
                "Choose a venue profile and start your shift. Velvet Rope will watch that venue's VIP list while counting general attendance without retaining guest identities.");
            ImGui.EndChild();
            ImGui.PopStyleColor();

            if (plugin.SelectedVenue.Vips.Count == 0)
            {
                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.Card);
                ImGui.PushStyleColor(ImGuiCol.Border, VelvetStyle.GoldDim);
                ImGui.BeginChild("##quickStart", new Vector2(-1, VelvetStyle.PanelHeight(4, extra: 18)), true);
                ImGui.TextColored(VelvetStyle.Gold, "QUICK START");
                ImGui.TextUnformatted("1. Target a player and use VIP Directory → Add Target.");
                ImGui.TextUnformatted("2. Choose their VIP status and optionally configure tier benefits.");
                ImGui.TextUnformatted("3. Return here and Start Shift when the venue opens.");
                ImGui.EndChild();
                ImGui.PopStyleColor(2);
            }

            if (plugin.Configuration.Reports.Count > 0)
            {
                ImGui.Spacing();
                var last = plugin.Configuration.Reports[0];
                ImGui.TextColored(VelvetStyle.Gold, "LAST COMPLETED SHIFT");
                ImGui.Spacing();

                var available = ImGui.GetContentRegionAvail().X;
                var cardWidth = Math.Max(150f, (available - 16f) / 3f);
                DrawMetricCard("Unique Guests", last.UniqueGuests.ToString(), cardWidth, VelvetStyle.Gold);
                ImGui.SameLine();
                DrawMetricCard("VIP Arrivals", last.VipArrivals.ToString(), cardWidth, VelvetStyle.Pink);
                ImGui.SameLine();
                DrawMetricCard("Peak Visible", last.PeakVisibleGuests.ToString(), cardWidth, VelvetStyle.Purple);
                ImGui.TextDisabled($"{last.VenueName}  •  {last.StartedAtUtc.ToLocalTime():ddd, MMM d}");
            }

            return;
        }

        var contentWidth = ImGui.GetContentRegionAvail().X;
        var metricWidth = Math.Max(125f, (contentWidth - 24f) / 4f);

        DrawMetricCard("Unique Guests", session.UniqueGuests.ToString(), metricWidth, VelvetStyle.Gold);
        ImGui.SameLine();
        DrawMetricCard("VIP Arrivals", session.VipArrivals.ToString(), metricWidth, VelvetStyle.Pink);
        ImGui.SameLine();
        DrawMetricCard("Peak Visible", session.PeakVisibleGuests.ToString(), metricWidth, VelvetStyle.Purple);
        ImGui.SameLine();
        DrawMetricCard("Shift Time", Plugin.FormatDuration(session.Elapsed(DateTime.UtcNow)), metricWidth, VelvetStyle.Green);

        ImGui.Spacing();

        if (plugin.CurrentAlert is not null)
        {
            var alert = plugin.CurrentAlert;
            var accent = new Vector4(alert.AccentR, alert.AccentG, alert.AccentB, alert.AccentA);

            ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.Darken(accent, 0.82f));
            ImGui.PushStyleColor(ImGuiCol.Border, accent);
            ImGui.BeginChild("##liveArrival", new Vector2(-1, VelvetStyle.PanelHeight(4, 1, 18)), true);
            ImGui.TextColored(accent, $"{alert.CategoryIcon}  LIVE VIP ARRIVAL");
            ImGui.SameLine();
            ImGui.TextDisabled($"• {alert.CategoryName}");
            ImGui.Text(alert.CharacterDisplay);
            ImGui.TextWrapped(alert.Message);
            ImGui.Spacing();
            if (alert.PublicAnnouncementEnabled)
            {
                VelvetStyle.PushAccentButton(accent);
                if (ImGui.Button("COPY SHOUT", VelvetStyle.ButtonSize("COPY SHOUT", 130)))
                    ImGui.SetClipboardText(alert.CopyText);
                VelvetStyle.PopAccentButton();
            }
            else
            {
                ImGui.TextColored(VelvetStyle.Muted, "Silent VIP • staff notice only");
            }
            ImGui.EndChild();
            ImGui.PopStyleColor(2);
            ImGui.Spacing();
        }

        ImGui.TextColored(VelvetStyle.Gold, "RECENT VIP ARRIVALS");
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.Card);
        ImGui.BeginChild("##recentArrivals", new Vector2(-1, 190), true);

        if (session.RecentVipArrivals.Count == 0)
        {
            ImGui.TextDisabled("The guest list is quiet. VIP arrivals will appear here during the shift.");
        }
        else
        {
            var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH |
                             ImGuiTableFlags.SizingStretchProp;

            if (ImGui.BeginTable("##recentVipTable", 5, tableFlags))
            {
                ImGui.TableSetupColumn("Name");
                ImGui.TableSetupColumn("World");
                ImGui.TableSetupColumn("Status");
                ImGui.TableSetupColumn("Time");
                ImGui.TableSetupColumn("Date");
                ImGui.TableHeadersRow();

                foreach (var arrival in session.RecentVipArrivals.Take(12))
                {
                    var local = arrival.TimestampUtc.ToLocalTime();
                    var category = plugin.Configuration.Categories.FirstOrDefault(c =>
                        string.Equals(c.Name, arrival.CategoryName, StringComparison.OrdinalIgnoreCase));
                    var accent = category is null ? VelvetStyle.Pink : VelvetStyle.CategoryAccent(category);

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(arrival.CharacterName);
                    ImGui.TableNextColumn();
                    ImGui.TextColored(VelvetStyle.Muted, string.IsNullOrWhiteSpace(arrival.HomeWorld) ? "—" : arrival.HomeWorld);
                    ImGui.TableNextColumn();
                    ImGui.TextColored(accent, arrival.CategoryName);
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"{local:h:mm tt}");
                    ImGui.TableNextColumn();
                    ImGui.TextColored(VelvetStyle.Muted, $"{local:MM/dd/yyyy}");
                }

                ImGui.EndTable();
            }
        }

        ImGui.EndChild();
        ImGui.PopStyleColor();

        if (plugin.CurrentAlert is not null || plugin.PendingAlertCount > 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(VelvetStyle.Gold,
                $"◆ Alert queue: {(plugin.CurrentAlert is null ? 0 : 1) + plugin.PendingAlertCount}");
        }

    }

    private void DrawVips()
    {
        DrawPageTitle(
            "VIP DIRECTORY",
            "Manage the people you explicitly want Velvet Rope to recognize at this venue.");

        DrawVenueSelector();
        var venue = plugin.SelectedVenue;
        ImGui.SameLine();
        ImGui.TextColored(VelvetStyle.Pink, $"★ {venue.Vips.Count} monitored");

        var expiredCount = venue.Vips.Count(v => v.IsExpired(DateTime.UtcNow));
        if (expiredCount > 0)
        {
            ImGui.SameLine();
            ImGui.TextColored(VelvetStyle.Gold, $"• {expiredCount} expired");
            ImGui.SameLine();
            if (ImGui.Button("CLEAN EXPIRED", VelvetStyle.ButtonSize("CLEAN EXPIRED", 118, extraY: 4)))
                plugin.CleanupExpiredVips(venue, DateTime.UtcNow);
        }

        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.CardRaised);
        ImGui.PushStyleColor(ImGuiCol.Border, VelvetStyle.GoldDim);
        ImGui.BeginChild("##addVipCard", new Vector2(-1, VelvetStyle.PanelHeight(5, 6, 18)), true);
        ImGui.TextColored(VelvetStyle.Gold, "ADD TO THE GUEST LIST");
        ImGui.TextDisabled("Create a new person record for this venue.");
        ImGui.Spacing();

        EnsureNewVipCategory();

        ImGui.SetNextItemWidth(250);
        ImGui.InputText("Character name##newVip", ref newVipName, 64);

        ImGui.SetNextItemWidth(180);
        ImGui.InputText("Home world##newVip", ref newVipWorld, 32);
        ImGui.SameLine();
        ImGui.TextDisabled("recommended for exact matching");

        if (DrawCategoryCombo("Category##newVip", ref newVipCategoryId))
            newVipUseTiering = DefaultTieringForCategory(newVipCategoryId);

        ImGui.Checkbox("Use VIP tiering##newVip", ref newVipUseTiering);
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text("Turn this off for role-only relationships such as Staff, DJ, Owner, Partner, or Regular.");
            ImGui.TextDisabled("Role-only entries do not expire and use the category glyph as a neutral nameplate marker.");
            ImGui.EndTooltip();
        }

        if (newVipUseTiering)
            DrawVipDurationCombo("VIP status##newVip", ref newVipDuration);

        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("First shout variant##newVip", ref newVipVariant, 500);
        ImGui.TextDisabled("Tokens: {name}, {world}, {venue}, {category}. Leave blank to use the category or venue default.");

        VelvetStyle.PushAccentButton(VelvetStyle.VelvetBright);
        if (ImGui.Button("ADD VIP", VelvetStyle.ButtonSize("ADD VIP", 120)))
        {
            if (!string.IsNullOrWhiteSpace(newVipName))
            {
                plugin.AddVipToVenue(
                    venue,
                    newVipName,
                    newVipWorld,
                    newVipCategoryId,
                    newVipUseTiering,
                    newVipDuration,
                    newVipVariant);

                newVipName = string.Empty;
                newVipWorld = string.Empty;
                newVipVariant = string.Empty;
            }
        }
        VelvetStyle.PopAccentButton();

        ImGui.SameLine();
        VelvetStyle.PushAccentButton(VelvetStyle.Gold);
        if (ImGui.Button("ADD TARGET", VelvetStyle.ButtonSize("ADD TARGET", 135)))
            PrepareTargetVipPopup(venue);
        VelvetStyle.PopAccentButton();

        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text("Add your current in-game player target.");
            ImGui.TextDisabled("Velvet Rope reads their character name and home world,");
            ImGui.TextDisabled("then asks which VIP status to give them before saving.");
            ImGui.EndTooltip();
        }

        ImGui.SameLine();
        VelvetStyle.PushAccentButton(VelvetStyle.Blue);
        if (ImGui.Button("TIER REFERENCE", VelvetStyle.ButtonSize("TIER REFERENCE", 155)))
        {
            tierBenefitsFeedback = string.Empty;
            requestTierBenefitsPopup = true;
        }
        VelvetStyle.PopAccentButton();

        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text("Open this venue's VIP tier quick reference.");
            ImGui.TextDisabled("If you have a player targeted, you can copy a prepared /tell for them.");
            ImGui.EndTooltip();
        }

        if (!string.IsNullOrWhiteSpace(targetVipFeedback))
        {
            ImGui.SameLine();
            ImGui.TextColored(VelvetStyle.Muted, targetVipFeedback);
        }

        ImGui.EndChild();
        ImGui.PopStyleColor(2);

        DrawAssignExisting(venue);

        ImGui.Spacing();
        ImGui.TextColored(VelvetStyle.Gold, "VENUE VIPS");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(280);
        ImGui.InputText("Search##vipSearch", ref vipSearch, 80);
        ImGui.Spacing();

        var links = venue.Vips
            .Select(link => (Link: link, Person: plugin.GetPerson(link.PersonId)))
            .Where(x => x.Person is not null)
            .Where(x =>
                string.IsNullOrWhiteSpace(vipSearch) ||
                x.Person!.DisplayName.Contains(vipSearch, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (links.Count == 0)
        {
            ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.Card);
            ImGui.BeginChild("##emptyVipList", new Vector2(-1, VelvetStyle.PanelHeight(1, extra: 12)), true);
            ImGui.TextDisabled("No matching VIPs in this venue profile.");
            ImGui.EndChild();
            ImGui.PopStyleColor();
            return;
        }

        foreach (var item in links)
        {
            var link = item.Link;
            var person = item.Person!;
            ImGui.PushID(link.Id.ToString());

            var category = plugin.GetCategory(link.CategoryId);
            var accent = VelvetStyle.CategoryAccent(category);
            var silentLabel = link.PreparePublicShout ? string.Empty : "  •  SILENT";
            var statusLabel = link.UseVipTiering ? $"  •  {FormatVipDuration(link.Duration)}" : "";
            var header = $"{category.Icon}  {person.DisplayName}     {category.Name}{statusLabel}{silentLabel}";

            ImGui.PushStyleColor(ImGuiCol.Header, VelvetStyle.Darken(accent, 0.70f));
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, VelvetStyle.Darken(accent, 0.48f));
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, VelvetStyle.Darken(accent, 0.30f));
            var open = ImGui.CollapsingHeader(header);
            ImGui.PopStyleColor(3);

            if (open)
            {
                ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.Card);
                ImGui.PushStyleColor(ImGuiCol.Border, VelvetStyle.WithAlpha(accent, 0.55f));
                ImGui.BeginChild("##vipEditor", new Vector2(-1, VelvetStyle.PanelHeight(7, 8, 115)), true);

                ImGui.TextColored(accent, $"{category.Icon} {category.Name.ToUpperInvariant()}");
                ImGui.SameLine();
                ImGui.TextDisabled("venue relationship");

                var enabled = link.Enabled;
                if (ImGui.Checkbox("Monitor at this venue", ref enabled))
                {
                    link.Enabled = enabled;
                    plugin.Configuration.Save();
                }

                ImGui.SameLine();
                var personEnabled = person.Enabled;
                if (ImGui.Checkbox("Enabled globally", ref personEnabled))
                {
                    person.Enabled = personEnabled;
                    plugin.Configuration.Save();
                }

                var silentVip = !link.PreparePublicShout;
                if (ImGui.Checkbox("Silent recognition (staff alert only)", ref silentVip))
                {
                    link.PreparePublicShout = !silentVip;
                    plugin.Configuration.Save();
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text("Silent entries are still detected and shown to staff.");
                    ImGui.TextDisabled("Velvet Rope shows staff the arrival, but does not prepare a public /sh line.");
                    ImGui.EndTooltip();
                }

                ImGui.TextDisabled("Name and world are global. Changing them updates this person anywhere they are reused.");

                var name = person.Name;
                ImGui.SetNextItemWidth(300);
                if (ImGui.InputText("Character name", ref name, 64))
                {
                    person.Name = name;
                    plugin.Configuration.Save();
                }

                var world = person.World;
                ImGui.SetNextItemWidth(220);
                if (ImGui.InputText("Home world", ref world, 32))
                {
                    person.World = world;
                    plugin.Configuration.Save();
                }

                var notes = person.Notes;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText("Private notes", ref notes, 300))
                {
                    person.Notes = notes;
                    plugin.Configuration.Save();
                }

                var categoryId = link.CategoryId;
                if (DrawCategoryCombo("Category", ref categoryId))
                {
                    link.CategoryId = categoryId;
                    plugin.Configuration.Save();
                }

                var useTiering = link.UseVipTiering;
                if (ImGui.Checkbox("Use VIP tiering", ref useTiering))
                {
                    link.UseVipTiering = useTiering;
                    link.AddedAtUtc = DateTime.UtcNow;
                    plugin.Configuration.Save();
                    plugin.RefreshVipNameplates();
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text("Disable tiering for role-only entries such as Staff.");
                    ImGui.TextDisabled("Their category glyph appears in a neutral color and the relationship does not expire.");
                    ImGui.EndTooltip();
                }

                if (link.UseVipTiering)
                {
                    var duration = link.Duration;
                    if (DrawVipDurationCombo("VIP status", ref duration))
                    {
                        link.Duration = duration;
                        plugin.Configuration.Save();
                    }

                    ImGui.SameLine();
                    DrawVipTierBenefitHint(venue, link.Duration);

                    var addedLocal = link.AddedAtUtc.ToLocalTime();
                    var expirationUtc = link.GetExpirationUtc();
                    ImGui.TextColored(VelvetStyle.Gold, "STATUS WINDOW");
                    ImGui.TextDisabled($"Entered: {addedLocal:MMM d, yyyy h:mm tt}");
                    if (expirationUtc.HasValue)
                    {
                        var expiresLocal = expirationUtc.Value.ToLocalTime();
                        var expired = link.IsExpired(DateTime.UtcNow);
                        ImGui.TextColored(expired ? VelvetStyle.Pink : VelvetStyle.Muted,
                            expired ? $"Expired: {expiresLocal:MMM d, yyyy h:mm tt}" : $"Expires: {expiresLocal:MMM d, yyyy h:mm tt}");
                    }
                    else
                    {
                        ImGui.TextColored(VelvetStyle.Green, "Lifetime status • no expiration");
                    }

                    if (link.Duration != VipDuration.Lifetime)
                    {
                        VelvetStyle.PushAccentButton(VelvetStyle.Gold);
                        if (ImGui.Button("RENEW FROM TODAY", VelvetStyle.ButtonSize("RENEW FROM TODAY", 145)))
                            plugin.RenewVip(link);
                        VelvetStyle.PopAccentButton();
                    }
                }
                else
                {
                    ImGui.TextColored(VelvetStyle.Gold, "ROLE RECOGNITION");
                    ImGui.TextDisabled($"{category.Icon} {category.Name} • no VIP tier • no expiration");
                }

                var variants = string.Join("\n", link.ShoutVariants);
                ImGui.TextColored(VelvetStyle.Gold, "SHOUT VARIANTS");
                ImGui.TextDisabled("One per line. Velvet Rope rotates them and avoids the most recent variant when possible.");
                if (ImGui.InputTextMultiline(
                        "##variants",
                        ref variants,
                        4000,
                        new Vector2(-1, 96)))
                {
                    link.ShoutVariants = variants
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .ToList();
                    plugin.Configuration.Save();
                }

                VelvetStyle.PushAccentButton(accent);
                if (ImGui.Button("TEST POPUP", VelvetStyle.ButtonSize("TEST POPUP", 120)))
                    plugin.TestAlert(venue, link);
                VelvetStyle.PopAccentButton();

                ImGui.SameLine();
                if (ImGui.Button("REMOVE FROM VENUE", VelvetStyle.ButtonSize("REMOVE FROM VENUE", 160)))
                {
                    plugin.RemoveVipFromVenue(venue, link.Id);
                    ImGui.EndChild();
                    ImGui.PopStyleColor(2);
                    ImGui.PopID();
                    break;
                }

                ImGui.EndChild();
                ImGui.PopStyleColor(2);
            }

            ImGui.Spacing();
            ImGui.PopID();
        }
    }

    private void PrepareTargetVipPopup(VenueProfile venue)
    {
        targetVipFeedback = string.Empty;

        if (!plugin.TryGetCurrentTargetPlayer(out var name, out var world))
        {
            targetVipFeedback = "Target a player first.";
            return;
        }

        PreparePlayerVipPopup(venue, name, world);
    }

    private void PreparePlayerVipPopup(VenueProfile venue, string name, string world)
    {
        targetVipFeedback = string.Empty;
        targetVipName = name.Trim();
        targetVipWorld = world.Trim();
        targetVipDuration = VipDuration.Nightly;
        targetVipCategoryId = plugin.Configuration.Categories[0].Id;
        targetVipUseTiering = DefaultTieringForCategory(targetVipCategoryId);
        targetVipExistingLinkId = Guid.Empty;

        var existing = plugin.FindVipInVenue(venue, targetVipName, targetVipWorld);
        if (existing is not null)
        {
            targetVipExistingLinkId = existing.Id;
            targetVipDuration = existing.Duration;
            targetVipCategoryId = existing.CategoryId;
            targetVipUseTiering = existing.UseVipTiering;
        }

        requestTargetVipPopup = true;
    }

    private void DrawTargetVipPopup()
    {
        ImGui.SetNextWindowSize(new Vector2(540f, 445f), ImGuiCond.Appearing);

        if (!ImGui.BeginPopup(TargetVipPopupId))
            return;

        var venue = plugin.SelectedVenue;
        var existing = targetVipExistingLinkId == Guid.Empty
            ? null
            : venue.Vips.FirstOrDefault(v => v.Id == targetVipExistingLinkId);

        ImGui.TextColored(VelvetStyle.Gold, existing is null ? "ADD TARGET TO VENUE" : "UPDATE VENUE ENTRY");
        ImGui.TextDisabled(venue.Name);
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(VelvetStyle.Ivory, targetVipName);
        ImGui.SameLine();
        ImGui.TextColored(
            VelvetStyle.Muted,
            string.IsNullOrWhiteSpace(targetVipWorld) ? "Home world unavailable" : $"@ {targetVipWorld}");

        if (existing is not null)
        {
            ImGui.Spacing();
            ImGui.TextColored(VelvetStyle.Gold, "This player is already recognized at this venue.");
            ImGui.TextDisabled(targetVipUseTiering
                ? "Saving will update their category/status and renew the tier timer from now."
                : "Saving will update their role/category without adding a VIP expiration timer.");
        }

        ImGui.Spacing();
        if (DrawCategoryCombo("Category##targetVip", ref targetVipCategoryId))
            targetVipUseTiering = DefaultTieringForCategory(targetVipCategoryId);

        ImGui.Checkbox("Use VIP tiering##targetVip", ref targetVipUseTiering);
        if (targetVipUseTiering)
        {
            DrawVipDurationCombo("VIP status##targetVip", ref targetVipDuration);

            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.CardRaised);
            ImGui.PushStyleColor(ImGuiCol.Border, VelvetStyle.GoldDim);
            ImGui.BeginChild("##targetVipDurationHelp", new Vector2(-1, VelvetStyle.PanelHeight(2, extra: 12)), true);
            ImGui.TextColored(VelvetStyle.Gold, "STATUS DURATION");
            ImGui.TextWrapped(GetTargetDurationDescription(targetVipDuration));
            ImGui.EndChild();
            ImGui.PopStyleColor(2);

            var selectedBenefits = venue.GetTierBenefits(targetVipDuration);
            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.Card);
            ImGui.PushStyleColor(ImGuiCol.Border, VelvetStyle.WithAlpha(VelvetStyle.Blue, 0.55f));
            ImGui.BeginChild("##targetVipBenefits", new Vector2(-1, VelvetStyle.PanelHeight(2, extra: 16)), true);
            ImGui.TextColored(VelvetStyle.Blue, "WHAT THIS TIER INCLUDES");
            if (string.IsNullOrWhiteSpace(selectedBenefits))
                ImGui.TextDisabled("No benefits have been configured for this tier yet.");
            else
                ImGui.TextWrapped(selectedBenefits);
            ImGui.EndChild();
            ImGui.PopStyleColor(2);
        }
        else
        {
            var selectedCategory = plugin.GetCategory(targetVipCategoryId);
            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.CardRaised);
            ImGui.PushStyleColor(ImGuiCol.Border, VelvetStyle.GoldDim);
            ImGui.BeginChild("##targetRoleHelp", new Vector2(-1, VelvetStyle.PanelHeight(2, extra: 12)), true);
            ImGui.TextColored(VelvetStyle.Gold, "ROLE RECOGNITION");
            ImGui.TextWrapped($"{selectedCategory.Icon} {selectedCategory.Name} will use a neutral nameplate marker and will not expire.");
            ImGui.EndChild();
            ImGui.PopStyleColor(2);
        }

        ImGui.Spacing();

        VelvetStyle.PushAccentButton(VelvetStyle.VelvetBright);
        var saveLabel = existing is null
            ? "ADD TO VENUE"
            : targetVipUseTiering ? "UPDATE & RENEW" : "UPDATE ENTRY";
        if (ImGui.Button(saveLabel, VelvetStyle.ButtonSize(saveLabel, 150)))
        {
            if (existing is null)
            {
                plugin.AddVipToVenue(
                    venue,
                    targetVipName,
                    targetVipWorld,
                    targetVipCategoryId,
                    targetVipUseTiering,
                    targetVipDuration,
                    string.Empty);

                targetVipFeedback = $"Added {targetVipName}.";
            }
            else
            {
                plugin.UpdateVipStatus(existing, targetVipCategoryId, targetVipUseTiering, targetVipDuration);
                targetVipFeedback = $"Updated {targetVipName}.";
            }

            targetVipExistingLinkId = Guid.Empty;
            ImGui.CloseCurrentPopup();
        }
        VelvetStyle.PopAccentButton();

        ImGui.SameLine();
        if (ImGui.Button("CANCEL", VelvetStyle.ButtonSize("CANCEL", 105)))
        {
            targetVipExistingLinkId = Guid.Empty;
            ImGui.CloseCurrentPopup();
        }

        ImGui.Spacing();
        ImGui.TextDisabled("The target is not saved until you confirm.");

        ImGui.EndPopup();
    }

    private static void DrawVipTierBenefitHint(VenueProfile venue, VipDuration duration)
    {
        var benefits = venue.GetTierBenefits(duration);
        var accent = GetTierAccent(duration);

        ImGui.TextColored(accent, "◆ view benefits");
        if (!ImGui.IsItemHovered())
            return;

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 430f);
        ImGui.TextColored(accent, $"{Plugin.FormatVipDuration(duration).ToUpperInvariant()} VIP");
        ImGui.TextDisabled(venue.Name);
        ImGui.Separator();
        ImGui.Spacing();
        if (string.IsNullOrWhiteSpace(benefits))
            ImGui.TextDisabled("No benefits are configured for this tier yet.");
        else
            ImGui.TextWrapped(benefits);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    private static string GetTargetDurationDescription(VipDuration duration) => duration switch
    {
        VipDuration.Nightly => "Nightly VIPs expire at the next local midnight and are cleaned up when the next shift starts.",
        VipDuration.Monthly => "Monthly VIPs expire one calendar month after you add or renew them.",
        VipDuration.Yearly => "Yearly VIPs expire one calendar year after you add or renew them.",
        VipDuration.Lifetime => "Lifetime VIPs do not expire automatically.",
        _ => "Choose how long this venue should recognize the target as a VIP."
    };

    private void DrawTierBenefitsPopup()
    {
        ImGui.SetNextWindowSize(new Vector2(690f, 620f), ImGuiCond.Appearing);

        if (!ImGui.BeginPopup(TierBenefitsPopupId))
            return;

        var venue = plugin.SelectedVenue;
        var hasTarget = plugin.TryGetCurrentTargetPlayer(out var targetName, out var targetWorld);

        ImGui.TextColored(VelvetStyle.Gold, "VIP TIER REFERENCE");
        ImGui.TextDisabled($"Quick reference for {venue.Name}");
        ImGui.Separator();
        ImGui.Spacing();

        if (hasTarget)
        {
            ImGui.TextColored(VelvetStyle.Blue, "TARGET");
            ImGui.SameLine();
            ImGui.TextColored(
                VelvetStyle.Ivory,
                string.IsNullOrWhiteSpace(targetWorld)
                    ? targetName
                    : $"{targetName} @ {targetWorld}");
            ImGui.TextDisabled("Use Copy Tell to prepare a private message for this player.");
        }
        else
        {
            ImGui.TextColored(VelvetStyle.Muted, "No player targeted.");
            ImGui.TextDisabled("You can still browse or copy tier benefits. Target a player to prepare a /tell.");
        }

        ImGui.Spacing();

        foreach (var duration in new[]
                 {
                     VipDuration.Nightly,
                     VipDuration.Monthly,
                     VipDuration.Yearly,
                     VipDuration.Lifetime
                 })
        {
            DrawTierBenefitsShowcaseCard(venue, duration, hasTarget, targetName, targetWorld);
            ImGui.Spacing();
        }

        if (!string.IsNullOrWhiteSpace(tierBenefitsFeedback))
            ImGui.TextColored(VelvetStyle.Green, tierBenefitsFeedback);

        ImGui.Spacing();
        if (ImGui.Button("CLOSE", VelvetStyle.ButtonSize("CLOSE", 105)))
            ImGui.CloseCurrentPopup();

        ImGui.EndPopup();
    }

    private void DrawTierBenefitsShowcaseCard(
        VenueProfile venue,
        VipDuration duration,
        bool hasTarget,
        string targetName,
        string targetWorld)
    {
        var benefits = venue.GetTierBenefits(duration);
        var accent = GetTierAccent(duration);
        var label = Plugin.FormatVipDuration(duration).ToUpperInvariant();

        ImGui.PushID($"benefitShowcase_{duration}");
        ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.CardRaised);
        ImGui.PushStyleColor(ImGuiCol.Border, VelvetStyle.WithAlpha(accent, 0.70f));
        ImGui.BeginChild("##tierCard", new Vector2(-1, VelvetStyle.PanelHeight(3, 1, 30)), true);

        ImGui.TextColored(accent, label);
        ImGui.SameLine();
        ImGui.TextDisabled(GetTierValidityShort(duration));

        if (string.IsNullOrWhiteSpace(benefits))
        {
            ImGui.Spacing();
            ImGui.TextDisabled("No benefits configured yet. Edit this tier from Venues.");
        }
        else
        {
            ImGui.Spacing();
            ImGui.TextWrapped(benefits);
            ImGui.Spacing();

            var copyText = plugin.BuildVipBenefitsText(venue, duration);
            if (ImGui.Button("COPY BENEFITS", VelvetStyle.ButtonSize("COPY BENEFITS", 140)))
            {
                ImGui.SetClipboardText(copyText);
                tierBenefitsFeedback = $"Copied {Plugin.FormatVipDuration(duration)} benefits.";
            }

            ImGui.SameLine();
            ImGui.BeginDisabled(!hasTarget);
            VelvetStyle.PushAccentButton(accent);
            if (ImGui.Button("COPY TELL TO TARGET", VelvetStyle.ButtonSize("COPY TELL TO TARGET", 185)))
            {
                var tell = plugin.BuildVipBenefitsTell(venue, duration, targetName, targetWorld);
                if (!string.IsNullOrWhiteSpace(tell))
                {
                    ImGui.SetClipboardText(tell);
                    tierBenefitsFeedback = $"Prepared a {Plugin.FormatVipDuration(duration)} VIP /tell for {targetName}.";
                }
            }
            VelvetStyle.PopAccentButton();
            ImGui.EndDisabled();
        }

        ImGui.EndChild();
        ImGui.PopStyleColor(2);
        ImGui.PopID();
    }

    private void DrawTierBenefitsEditorPopup()
    {
        ImGui.SetNextWindowSize(new Vector2(720f, 650f), ImGuiCond.Appearing);

        if (!ImGui.BeginPopup(TierBenefitsEditorPopupId))
            return;

        var venue = plugin.Configuration.Venues.FirstOrDefault(v => v.Id == tierBenefitsEditorVenueId)
                    ?? plugin.SelectedVenue;

        ImGui.TextColored(VelvetStyle.Gold, "EDIT VIP TIER BENEFITS");
        ImGui.TextDisabled(venue.Name);
        ImGui.TextWrapped(
            "Describe what guests receive at each status. These descriptions appear in the VIP Benefits showcase and can be copied into a tell for your current target.");
        ImGui.Spacing();
        ImGui.TextDisabled("Tip: keep each tier concise enough to fit comfortably in one in-game tell.");
        ImGui.Separator();
        ImGui.Spacing();

        foreach (var duration in new[]
                 {
                     VipDuration.Nightly,
                     VipDuration.Monthly,
                     VipDuration.Yearly,
                     VipDuration.Lifetime
                 })
        {
            var accent = GetTierAccent(duration);
            ImGui.PushID($"benefitEditor_{duration}");
            ImGui.TextColored(accent, Plugin.FormatVipDuration(duration).ToUpperInvariant());
            ImGui.SameLine();
            ImGui.TextDisabled(GetTierValidityShort(duration));

            var value = venue.GetTierBenefits(duration);
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputTextMultiline(
                    "##benefits",
                    ref value,
                    600,
                    new Vector2(-1, 78f)))
            {
                venue.SetTierBenefits(duration, value);
                plugin.Configuration.Save();
            }

            ImGui.Spacing();
            ImGui.PopID();
        }

        if (ImGui.Button("DONE", VelvetStyle.ButtonSize("DONE", 105)))
            ImGui.CloseCurrentPopup();

        ImGui.EndPopup();
    }

    private static Vector4 GetTierAccent(VipDuration duration) => duration switch
    {
        VipDuration.Nightly => VelvetStyle.Blue,
        VipDuration.Monthly => VelvetStyle.Pink,
        VipDuration.Yearly => VelvetStyle.Gold,
        VipDuration.Lifetime => VelvetStyle.Green,
        _ => VelvetStyle.Ivory
    };

    private static string GetTierValidityShort(VipDuration duration) => duration switch
    {
        VipDuration.Nightly => "Valid for the night",
        VipDuration.Monthly => "Valid for one month",
        VipDuration.Yearly => "Valid for one year",
        VipDuration.Lifetime => "Never expires",
        _ => string.Empty
    };

    private void DrawAssignExisting(VenueProfile venue)
    {
        var availablePeople = plugin.Configuration.People
            .Where(p => venue.Vips.All(v => v.PersonId != p.Id))
            .OrderBy(p => p.Name)
            .ToList();

        if (availablePeople.Count == 0)
            return;

        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.Card);
        ImGui.BeginChild("##assignExisting", new Vector2(-1, VelvetStyle.PanelHeight(4, 4, 14)), true);
        ImGui.TextColored(VelvetStyle.Gold, "REUSE AN EXISTING PERSON");
        ImGui.TextDisabled("Useful when the same guest is recognized at more than one venue.");

        if (assignExistingPersonId == Guid.Empty ||
            availablePeople.All(p => p.Id != assignExistingPersonId))
        {
            assignExistingPersonId = availablePeople[0].Id;
        }

        var selectedPerson = availablePeople.First(p => p.Id == assignExistingPersonId);
        ImGui.SetNextItemWidth(300);

        if (ImGui.BeginCombo("Existing person##assign", selectedPerson.DisplayName))
        {
            foreach (var person in availablePeople)
            {
                var selected = person.Id == assignExistingPersonId;
                if (ImGui.Selectable(person.DisplayName, selected))
                    assignExistingPersonId = person.Id;
                if (selected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        if (DrawCategoryCombo("Category##assign", ref newVipCategoryId))
            assignExistingUseTiering = DefaultTieringForCategory(newVipCategoryId);

        ImGui.Checkbox("Use VIP tiering##assign", ref assignExistingUseTiering);
        if (assignExistingUseTiering)
        {
            ImGui.SameLine();
            DrawVipDurationCombo("Status##assign", ref newVipDuration, 150);
        }

        ImGui.SameLine();
        VelvetStyle.PushAccentButton(VelvetStyle.Gold);
        if (ImGui.Button("ASSIGN TO VENUE", VelvetStyle.ButtonSize("ASSIGN TO VENUE", 150)))
            plugin.AssignExistingPerson(venue, assignExistingPersonId, newVipCategoryId, assignExistingUseTiering, newVipDuration);
        VelvetStyle.PopAccentButton();

        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private void DrawVenues()
    {
        DrawPageTitle(
            "VENUE PROFILES",
            "Keep each workplace, greeting style, and VIP relationship separate.");

        ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.CardRaised);
        ImGui.PushStyleColor(ImGuiCol.Border, VelvetStyle.GoldDim);
        ImGui.BeginChild("##createVenue", new Vector2(-1, VelvetStyle.PanelHeight(1, 1, 14)), true);
        ImGui.TextColored(VelvetStyle.Gold, "CREATE A VENUE PROFILE");
        ImGui.SetNextItemWidth(300);
        ImGui.InputText("New venue name", ref newVenueName, 80);
        ImGui.SameLine();
        VelvetStyle.PushAccentButton(VelvetStyle.VelvetBright);
        if (ImGui.Button("CREATE", VelvetStyle.ButtonSize("CREATE", 100)))
        {
            plugin.AddVenue(newVenueName);
            newVenueName = string.Empty;
        }
        VelvetStyle.PopAccentButton();
        ImGui.EndChild();
        ImGui.PopStyleColor(2);

        ImGui.Spacing();
        ImGui.TextColored(VelvetStyle.Gold, "YOUR VENUES");
        ImGui.Spacing();

        foreach (var venue in plugin.Configuration.Venues.ToList())
        {
            ImGui.PushID(venue.Id.ToString());

            var selected = venue.Id == plugin.Configuration.SelectedVenueId;
            var active = plugin.ActiveSession?.VenueId == venue.Id;
            var accent = active ? VelvetStyle.Green : selected ? VelvetStyle.Gold : VelvetStyle.Pink;
            var title = $"{(active ? "●" : selected ? "◆" : "○")}  {venue.Name}     {venue.Vips.Count} VIPs";

            ImGui.PushStyleColor(ImGuiCol.Header, VelvetStyle.Darken(accent, 0.78f));
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, VelvetStyle.Darken(accent, 0.58f));
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, VelvetStyle.Darken(accent, 0.42f));
            var open = ImGui.CollapsingHeader(
                title,
                selected ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None);
            ImGui.PopStyleColor(3);

            if (open)
            {
                ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.Card);
                ImGui.PushStyleColor(ImGuiCol.Border, VelvetStyle.WithAlpha(accent, 0.55f));
                ImGui.BeginChild("##venueEditor", new Vector2(-1, VelvetStyle.PanelHeight(6, 4, 118)), true);

                if (active)
                    ImGui.TextColored(VelvetStyle.Green, "● ACTIVE SHIFT");
                else if (selected)
                    ImGui.TextColored(VelvetStyle.Gold, "◆ CURRENT PROFILE");

                var name = venue.Name;
                ImGui.SetNextItemWidth(350);
                if (ImGui.InputText("Venue name", ref name, 80))
                {
                    venue.Name = name;
                    plugin.Configuration.Save();
                }

                var defaultTemplate = venue.DefaultShoutTemplate;
                ImGui.TextColored(VelvetStyle.Gold, "FALLBACK SHOUT TEMPLATE");
                if (ImGui.InputTextMultiline(
                        "##venueDefaultTemplate",
                        ref defaultTemplate,
                        1000,
                        new Vector2(-1, 68)))
                {
                    venue.DefaultShoutTemplate = defaultTemplate;
                    plugin.Configuration.Save();
                }

                ImGui.TextDisabled("Tokens: {name}, {world}, {venue}, {category}.");

                var configuredTiers = new[]
                {
                    venue.NightlyBenefits,
                    venue.MonthlyBenefits,
                    venue.YearlyBenefits,
                    venue.LifetimeBenefits
                }.Count(v => !string.IsNullOrWhiteSpace(v));

                VelvetStyle.PushAccentButton(VelvetStyle.Blue);
                if (ImGui.Button("EDIT VIP BENEFITS", VelvetStyle.ButtonSize("EDIT VIP BENEFITS", 165)))
                {
                    tierBenefitsEditorVenueId = venue.Id;
                    requestTierBenefitsEditorPopup = true;
                }
                VelvetStyle.PopAccentButton();
                ImGui.SameLine();
                ImGui.TextDisabled($"{configuredTiers}/4 tiers configured");

                ImGui.Spacing();

                if (!selected)
                {
                    ImGui.BeginDisabled(plugin.ActiveSession is not null);
                    VelvetStyle.PushAccentButton(VelvetStyle.Gold);
                    if (ImGui.Button("SELECT PROFILE", VelvetStyle.ButtonSize("SELECT PROFILE", 130)))
                        plugin.SelectVenue(venue.Id);
                    VelvetStyle.PopAccentButton();
                    ImGui.EndDisabled();
                    ImGui.SameLine();
                }

                if (ImGui.Button("EXPORT VENUE PACK", VelvetStyle.ButtonSize("EXPORT VENUE PACK", 155)))
                {
                    ImGui.SetClipboardText(plugin.ExportVenuePack(venue.Id));
                    transferStatus = $"Copied {venue.Name} venue pack to clipboard.";
                }

                ImGui.SameLine();
                ImGui.BeginDisabled(
                    plugin.Configuration.Venues.Count <= 1 ||
                    plugin.ActiveSession?.VenueId == venue.Id);

                if (ImGui.Button("DELETE VENUE", VelvetStyle.ButtonSize("DELETE VENUE", 120)))
                {
                    plugin.DeleteVenue(venue.Id);
                    ImGui.EndDisabled();
                    ImGui.EndChild();
                    ImGui.PopStyleColor(2);
                    ImGui.PopID();
                    break;
                }

                ImGui.EndDisabled();
                ImGui.EndChild();
                ImGui.PopStyleColor(2);
            }

            ImGui.Spacing();
            ImGui.PopID();
        }

        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.CardRaised);
        ImGui.BeginChild("##venueTransfer", new Vector2(-1, VelvetStyle.PanelHeight(1, 1, 10)), true);
        ImGui.TextColored(VelvetStyle.Gold, "SHARE / IMPORT");
        if (ImGui.Button("IMPORT VENUE / VIP PACK FROM CLIPBOARD", VelvetStyle.ButtonSize("IMPORT VENUE / VIP PACK FROM CLIPBOARD")))
            transferStatus = plugin.ImportTransfer(ImGui.GetClipboardText());

        if (!string.IsNullOrWhiteSpace(transferStatus))
        {
            ImGui.SameLine();
            ImGui.TextColored(VelvetStyle.Muted, transferStatus);
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private void DrawReports()
    {
        DrawPageTitle(
            "SHIFT REPORTS",
            "Anonymous attendance totals and venue performance, without a historical guest list.");

        if (plugin.ActiveSession is not null)
        {
            ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.Darken(VelvetStyle.Green, 0.82f));
            ImGui.PushStyleColor(ImGuiCol.Border, VelvetStyle.Green);
            ImGui.BeginChild("##liveReportNotice", new Vector2(-1, VelvetStyle.PanelHeight(2, extra: 8)), true);
            ImGui.TextColored(VelvetStyle.Green, "● CURRENT SHIFT IS LIVE");
            ImGui.TextDisabled("Its aggregate report will be saved when the shift ends.");
            ImGui.EndChild();
            ImGui.PopStyleColor(2);
            ImGui.Spacing();
        }

        if (plugin.Configuration.Reports.Count == 0)
        {
            ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.Card);
            ImGui.BeginChild("##emptyReports", new Vector2(-1, VelvetStyle.PanelHeight(2, extra: 12)), true);
            ImGui.TextColored(VelvetStyle.Gold, "NO COMPLETED SHIFTS YET");
            ImGui.TextDisabled("End a shift to create your first anonymous attendance report.");
            ImGui.EndChild();
            ImGui.PopStyleColor();
            return;
        }

        foreach (var report in plugin.Configuration.Reports.ToList())
        {
            ImGui.PushID(report.Id.ToString());
            var localStart = report.StartedAtUtc.ToLocalTime();
            var header = $"◆  {report.VenueName}     {localStart:ddd, MMM d yyyy}     {report.UniqueGuests} guests";

            ImGui.PushStyleColor(ImGuiCol.Header, VelvetStyle.Darken(VelvetStyle.Gold, 0.76f));
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, VelvetStyle.Darken(VelvetStyle.Gold, 0.58f));
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, VelvetStyle.Darken(VelvetStyle.Gold, 0.42f));
            var open = ImGui.CollapsingHeader(header);
            ImGui.PopStyleColor(3);

            if (open)
            {
                var localEnd = report.EndedAtUtc.ToLocalTime();
                ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.Card);
                ImGui.PushStyleColor(ImGuiCol.Border, VelvetStyle.GoldDim);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(14f, 14f));

                // Reports are intentionally roomier than utility panels. The extra
                // padding keeps metric cards, tables, and actions from feeling pressed
                // against the report border at larger Dalamud font scales.
                var reportLine = ImGui.GetTextLineHeightWithSpacing();
                var reportMetricHeight = VelvetStyle.PanelHeight(2, extra: 12);
                var reportHourlyHeight = report.HourlyAttendance.Count > 0
                    ? reportLine * (report.HourlyAttendance.Count + 3f) + 22f
                    : 0f;
                var reportHeight = 14f * 2f + reportLine + 18f + reportMetricHeight + 22f +
                                   reportHourlyHeight + VelvetStyle.ControlHeight() + 34f;

                ImGui.BeginChild("##reportCard", new Vector2(-1, reportHeight), true);

                ImGui.TextColored(VelvetStyle.Muted,
                    $"{localStart:h:mm tt} – {localEnd:h:mm tt}   •   {Plugin.FormatDuration(report.Duration)}");
                ImGui.Dummy(new Vector2(0, 5f));

                var available = ImGui.GetContentRegionAvail().X;
                var cardWidth = Math.Max(140f, (available - 16f) / 3f);
                DrawMetricCard("Unique Guests", report.UniqueGuests.ToString(), cardWidth, VelvetStyle.Gold);
                ImGui.SameLine();
                DrawMetricCard("VIP Arrivals", report.VipArrivals.ToString(), cardWidth, VelvetStyle.Pink);
                ImGui.SameLine();
                DrawMetricCard("Peak Visible", report.PeakVisibleGuests.ToString(), cardWidth, VelvetStyle.Purple);

                if (report.HourlyAttendance.Count > 0)
                {
                    ImGui.Dummy(new Vector2(0, 8f));
                    ImGui.TextColored(VelvetStyle.Gold, "NEW UNIQUE GUESTS BY HOUR");
                    ImGui.Dummy(new Vector2(0, 3f));

                    var hourlyTableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH |
                                           ImGuiTableFlags.SizingStretchProp;
                    if (ImGui.BeginTable("##hourlyAttendanceTable", 3, hourlyTableFlags))
                    {
                        ImGui.TableSetupColumn("Hour");
                        ImGui.TableSetupColumn("New Guests");
                        ImGui.TableSetupColumn("Running Total");
                        ImGui.TableHeadersRow();

                        var runningTotal = 0;
                        foreach (var bucket in report.HourlyAttendance.OrderBy(b => b.HourStartUtc))
                        {
                            runningTotal += bucket.NewUniqueGuests;
                            var hour = bucket.HourStartUtc.ToLocalTime();

                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.TextColored(VelvetStyle.Muted, $"{hour:h tt}");
                            ImGui.TableNextColumn();
                            ImGui.TextUnformatted(bucket.NewUniqueGuests.ToString());
                            ImGui.TableNextColumn();
                            ImGui.TextColored(VelvetStyle.Gold, runningTotal.ToString());
                        }

                        ImGui.EndTable();
                    }
                }

                ImGui.Dummy(new Vector2(0, 9f));
                VelvetStyle.PushAccentButton(VelvetStyle.Gold);
                if (ImGui.Button("COPY SUMMARY", VelvetStyle.ButtonSize("COPY SUMMARY", 130)))
                    ImGui.SetClipboardText(plugin.BuildReportSummary(report));
                VelvetStyle.PopAccentButton();

                ImGui.SameLine();
                if (ImGui.Button("DELETE REPORT", VelvetStyle.ButtonSize("DELETE REPORT", 125)))
                {
                    plugin.Configuration.Reports.RemoveAll(r => r.Id == report.Id);
                    plugin.Configuration.Save();
                    ImGui.EndChild();
                    ImGui.PopStyleVar();
                    ImGui.PopStyleColor(2);
                    ImGui.PopID();
                    break;
                }

                ImGui.EndChild();
                ImGui.PopStyleVar();
                ImGui.PopStyleColor(2);
            }

            ImGui.Spacing();
            ImGui.PopID();
        }

        ImGui.Spacing();
        if (ImGui.Button("CLEAR ALL REPORTS", VelvetStyle.ButtonSize("CLEAR ALL REPORTS")))
        {
            plugin.Configuration.Reports.Clear();
            plugin.Configuration.Save();
        }
    }

    private void DrawSettings()
    {
        DrawPageTitle(
            "SETTINGS",
            "Tune alerts, categories, and sharing without cluttering the live dashboard.");

        ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.CardRaised);
        ImGui.PushStyleColor(ImGuiCol.Border, VelvetStyle.GoldDim);
        ImGui.BeginChild("##alertSettings", new Vector2(-1, VelvetStyle.PanelHeight(1, 3, 18)), true);
        ImGui.TextColored(VelvetStyle.Gold, "ARRIVAL ALERTS");

        var grace = plugin.Configuration.ReentryGraceSeconds;
        ImGui.SetNextItemWidth(140);
        if (ImGui.InputInt("VIP re-entry grace (seconds)", ref grace))
        {
            plugin.Configuration.ReentryGraceSeconds = Math.Clamp(grace, 0, 600);
            plugin.Configuration.Save();
        }

        var nativeToast = plugin.Configuration.NativeToastEnabled;
        if (ImGui.Checkbox("Show native Dalamud toast", ref nativeToast))
        {
            plugin.Configuration.NativeToastEnabled = nativeToast;
            plugin.Configuration.Save();
        }

        var sound = plugin.Configuration.AlertSoundEnabled;
        if (ImGui.Checkbox("Play Windows alert sound for VIP arrivals", ref sound))
        {
            plugin.Configuration.AlertSoundEnabled = sound;
            plugin.Configuration.Save();
        }

        ImGui.EndChild();
        ImGui.PopStyleColor(2);

        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.CardRaised);
        ImGui.PushStyleColor(ImGuiCol.Border, VelvetStyle.GoldDim);
        ImGui.BeginChild("##inWorldVipTools", new Vector2(-1, VelvetStyle.PanelHeight(7, 3, 34)), true);
        ImGui.TextColored(VelvetStyle.Gold, "IN-WORLD VIP TOOLS");
        ImGui.TextDisabled("Local-only helpers for recognizing and checking VIPs while you work a venue.");

        var crowns = plugin.Configuration.VipCrownBadgesEnabled;
        if (ImGui.Checkbox("Show venue role / tier markers on recognized nameplates during an active shift", ref crowns))
        {
            plugin.Configuration.VipCrownBadgesEnabled = crowns;
            plugin.Configuration.Save();
            plugin.RefreshVipNameplates();
        }

        ImGui.TextColored(new Vector4(0.70f, 0.72f, 0.76f, 1f), "Nightly: ◆ Black");
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.72f, 0.39f, 0.18f, 1f), "  •  Monthly: ◆ Copper");
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.78f, 0.82f, 0.86f, 1f), "  •  Yearly: ◆ Silver");
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.96f, 0.73f, 0.19f, 1f), "  •  Lifetime: ★ Gold");

        ImGui.TextDisabled("Tiered VIPs use colored diamond/star markers; role-only entries use their category glyph in a neutral color.");

        var maxDistance = plugin.Configuration.VipCrownMaxDistance;
        ImGui.SetNextItemWidth(180);
        if (ImGui.SliderFloat("Marker distance##vipCrown", ref maxDistance, 10f, 80f, "%.0f yalms"))
        {
            plugin.Configuration.VipCrownMaxDistance = maxDistance;
            plugin.Configuration.Save();
            plugin.RefreshVipNameplates();
        }

        ImGui.TextDisabled($"Visible recognized people matched for marker display: {plugin.VisibleVipBadgeCount}");
        if (ImGui.Button("PREVIEW GOLD LIFETIME STAR", VelvetStyle.ButtonSize("PREVIEW GOLD LIFETIME STAR", 250)))
        {
            if (!plugin.PreviewCrownOnCurrentTarget())
                ImGui.OpenPopup("##NoCrownPreviewTarget");
        }
        if (ImGui.BeginPopup("##NoCrownPreviewTarget"))
        {
            ImGui.TextUnformatted("Target a player first.");
            ImGui.EndPopup();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("Shows for 10 seconds and ignores shift/VIP rules.");

        var contextMenu = plugin.Configuration.PlayerContextMenuEnabled;
        if (ImGui.Checkbox("Add VIP Tier / benefits actions to player right-click menus", ref contextMenu))
        {
            plugin.Configuration.PlayerContextMenuEnabled = contextMenu;
            plugin.Configuration.Save();
        }
        ImGui.TextDisabled("The existing Add Target button remains available in the VIP Directory.");

        ImGui.EndChild();
        ImGui.PopStyleColor(2);

        ImGui.Spacing();
        DrawAppearance();

        ImGui.Spacing();
        DrawCategories();

        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.CardRaised);
        ImGui.PushStyleColor(ImGuiCol.Border, VelvetStyle.GoldDim);
        ImGui.BeginChild("##importExport", new Vector2(-1, VelvetStyle.PanelHeight(2, 1, 14)), true);
        ImGui.TextColored(VelvetStyle.Gold, "IMPORT / EXPORT");
        ImGui.TextDisabled("Share a people database or import Velvet Rope packs from other venue staff.");

        if (ImGui.Button("EXPORT GLOBAL PEOPLE DATABASE", VelvetStyle.ButtonSize("EXPORT GLOBAL PEOPLE DATABASE")))
        {
            ImGui.SetClipboardText(plugin.ExportVipDatabase());
            transferStatus = "Copied global people database to clipboard.";
        }

        ImGui.SameLine();
        if (ImGui.Button("IMPORT FROM CLIPBOARD", VelvetStyle.ButtonSize("IMPORT FROM CLIPBOARD")))
            transferStatus = plugin.ImportTransfer(ImGui.GetClipboardText());

        if (!string.IsNullOrWhiteSpace(transferStatus))
        {
            ImGui.Spacing();
            ImGui.TextColored(VelvetStyle.Muted, transferStatus);
        }

        ImGui.EndChild();
        ImGui.PopStyleColor(2);

    }

    private void DrawAppearance()
    {
        var theme = plugin.Configuration.UiTheme;

        ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.CardRaised);
        ImGui.PushStyleColor(ImGuiCol.Border, VelvetStyle.GoldDim);
        ImGui.BeginChild("##appearanceSettings", new Vector2(-1, 650), true);

        ImGui.TextColored(VelvetStyle.Gold, "UI PACK & APPEARANCE");
        ImGui.TextDisabled("Apply a .vrui pack or build your own look with live theme controls.");
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.Card);
        ImGui.PushStyleColor(ImGuiCol.Border, VelvetStyle.GoldDim);
        ImGui.BeginChild("##activeUiPack", new Vector2(-1, VelvetStyle.PanelHeight(5, 1, 24)), true);
        ImGui.TextColored(VelvetStyle.Gold, $"{theme.BrandMark}  {theme.PackName}");
        ImGui.TextColored(VelvetStyle.Muted, $"by {theme.Author}");
        if (!string.IsNullOrWhiteSpace(theme.Description))
            ImGui.TextWrapped(theme.Description);
        else
            ImGui.TextDisabled("No pack description.");

        ImGui.Spacing();
        VelvetStyle.PushAccentButton(VelvetStyle.Gold);
        if (ImGui.Button("IMPORT UI PACK", VelvetStyle.ButtonSize("IMPORT UI PACK", 145)))
            OpenUiPackImportDialog();
        VelvetStyle.PopAccentButton();

        ImGui.SameLine();
        if (ImGui.Button("EXPORT UI PACK", VelvetStyle.ButtonSize("EXPORT UI PACK", 145)))
            OpenUiPackExportDialog();

        ImGui.SameLine();
        if (ImGui.Button("RESET VELVET CLASSIC", VelvetStyle.ButtonSize("RESET VELVET CLASSIC", 175)))
        {
            plugin.ResetUiTheme();
            uiPackStatus = "Restored the Velvet Classic UI pack.";
        }

        DrawUiPackHelp();

        ImGui.EndChild();
        ImGui.PopStyleColor(2);

        if (!string.IsNullOrWhiteSpace(uiPackStatus))
        {
            ImGui.Spacing();
            ImGui.TextColored(VelvetStyle.Green, uiPackStatus);
        }

        ImGui.Spacing();
        ImGui.TextColored(VelvetStyle.Gold, "PACK IDENTITY");

        var packName = theme.PackName;
        ImGui.SetNextItemWidth(260);
        if (ImGui.InputText("Pack name##ui", ref packName, 80))
        {
            theme.PackName = packName;
            SaveUiTheme();
        }

        ImGui.SameLine();
        var author = theme.Author;
        ImGui.SetNextItemWidth(220);
        if (ImGui.InputText("Author##ui", ref author, 80))
        {
            theme.Author = author;
            SaveUiTheme();
        }

        var description = theme.Description;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("Description##ui", ref description, 240))
        {
            theme.Description = description;
            SaveUiTheme();
        }

        ImGui.Spacing();
        ImGui.TextColored(VelvetStyle.Gold, "BRAND ASSETS");
        ImGui.TextDisabled("Schema 2 .vrui packs can carry logos and header artwork inside the pack.");

        VelvetStyle.PushAccentButton(VelvetStyle.Gold);
        if (ImGui.Button("CHOOSE LOGO", VelvetStyle.ButtonSize("CHOOSE LOGO", 125)))
            OpenUiAssetDialog(false);
        VelvetStyle.PopAccentButton();
        ImGui.SameLine();
        if (ImGui.Button("CLEAR LOGO", VelvetStyle.ButtonSize("CLEAR LOGO", 115)))
        {
            plugin.ClearUiAsset(false);
            uiPackStatus = "Logo cleared.";
        }
        ImGui.SameLine();
        ImGui.TextDisabled(string.IsNullOrWhiteSpace(theme.LogoAsset) ? "No logo loaded" : theme.LogoAsset);

        VelvetStyle.PushAccentButton(VelvetStyle.Gold);
        if (ImGui.Button("CHOOSE HEADER ART", VelvetStyle.ButtonSize("CHOOSE HEADER ART", 165)))
            OpenUiAssetDialog(true);
        VelvetStyle.PopAccentButton();
        ImGui.SameLine();
        if (ImGui.Button("CLEAR HEADER ART", VelvetStyle.ButtonSize("CLEAR HEADER ART", 155)))
        {
            plugin.ClearUiAsset(true);
            uiPackStatus = "Header artwork cleared.";
        }
        ImGui.SameLine();
        ImGui.TextDisabled(string.IsNullOrWhiteSpace(theme.HeaderBackgroundAsset) ? "No header artwork loaded" : theme.HeaderBackgroundAsset);

        var showHeaderLogo = theme.ShowHeaderLogo;
        if (ImGui.Checkbox("Show logo in header", ref showHeaderLogo))
        {
            theme.ShowHeaderLogo = showHeaderLogo;
            SaveUiTheme();
        }
        ImGui.SameLine();
        var showSidebarLogo = theme.ShowSidebarLogo;
        if (ImGui.Checkbox("Show logo in sidebar", ref showSidebarLogo))
        {
            theme.ShowSidebarLogo = showSidebarLogo;
            SaveUiTheme();
        }
        ImGui.SameLine();
        var showHeaderArt = theme.ShowHeaderBackground;
        if (ImGui.Checkbox("Show header artwork", ref showHeaderArt))
        {
            theme.ShowHeaderBackground = showHeaderArt;
            SaveUiTheme();
        }

        var showBrandTitle = theme.ShowBrandTitle;
        if (ImGui.Checkbox("Show text title with logo", ref showBrandTitle))
        {
            theme.ShowBrandTitle = showBrandTitle;
            SaveUiTheme();
        }

        var headerHeight = theme.HeaderHeight;
        ImGui.SetNextItemWidth(220);
        if (ImGui.SliderFloat("Header height##ui", ref headerHeight, 62f, 180f, "%.0f px"))
        {
            theme.HeaderHeight = headerHeight;
            SaveUiTheme();
        }
        ImGui.SameLine();
        var headerLogoHeight = theme.HeaderLogoHeight;
        ImGui.SetNextItemWidth(220);
        if (ImGui.SliderFloat("Header logo size##ui", ref headerLogoHeight, 28f, 140f, "%.0f px"))
        {
            theme.HeaderLogoHeight = headerLogoHeight;
            SaveUiTheme();
        }

        var sidebarLogoHeight = theme.SidebarLogoHeight;
        ImGui.SetNextItemWidth(220);
        if (ImGui.SliderFloat("Sidebar logo size##ui", ref sidebarLogoHeight, 48f, 220f, "%.0f px"))
        {
            theme.SidebarLogoHeight = sidebarLogoHeight;
            SaveUiTheme();
        }
        ImGui.SameLine();
        var logoOpacity = theme.LogoOpacity;
        ImGui.SetNextItemWidth(220);
        if (ImGui.SliderFloat("Logo opacity##ui", ref logoOpacity, 0.15f, 1f, "%.2f"))
        {
            theme.LogoOpacity = logoOpacity;
            SaveUiTheme();
        }

        var artOpacity = theme.HeaderBackgroundOpacity;
        ImGui.SetNextItemWidth(220);
        if (ImGui.SliderFloat("Header art opacity##ui", ref artOpacity, 0f, 1f, "%.2f"))
        {
            theme.HeaderBackgroundOpacity = artOpacity;
            SaveUiTheme();
        }
        ImGui.SameLine();
        var overlayOpacity = theme.HeaderOverlayOpacity;
        ImGui.SetNextItemWidth(220);
        if (ImGui.SliderFloat("Header overlay##ui", ref overlayOpacity, 0f, 1f, "%.2f"))
        {
            theme.HeaderOverlayOpacity = overlayOpacity;
            SaveUiTheme();
        }

        ImGui.Spacing();
        ImGui.TextColored(VelvetStyle.Gold, "BRANDING");

        var mark = theme.BrandMark;
        ImGui.SetNextItemWidth(90);
        if (ImGui.InputText("Mark##ui", ref mark, 8))
        {
            theme.BrandMark = mark;
            SaveUiTheme();
        }

        ImGui.SameLine();
        var title = theme.BrandTitle;
        ImGui.SetNextItemWidth(240);
        if (ImGui.InputText("Header title##ui", ref title, 60))
        {
            theme.BrandTitle = title;
            SaveUiTheme();
        }

        var tagline = theme.Tagline;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("Tagline##ui", ref tagline, 120))
        {
            theme.Tagline = tagline;
            SaveUiTheme();
        }

        var showTagline = theme.ShowBrandTagline;
        if (ImGui.Checkbox("Show tagline in header", ref showTagline))
        {
            theme.ShowBrandTagline = showTagline;
            SaveUiTheme();
        }

        ImGui.Spacing();
        ImGui.TextColored(VelvetStyle.Gold, "PALETTE");
        ImGui.TextDisabled("Changes preview live. Category arrival colors remain independently configurable below.");

        var paletteFlags = ImGuiTableFlags.SizingStretchSame;
        if (ImGui.BeginTable("##uiPalette", 2, paletteFlags))
        {
            ImGui.TableNextColumn(); DrawThemeColor("Background##ui", theme.Background);
            ImGui.TableNextColumn(); DrawThemeColor("Panel##ui", theme.Card);
            ImGui.TableNextColumn(); DrawThemeColor("Raised panel##ui", theme.CardRaised);
            ImGui.TableNextColumn(); DrawThemeColor("Primary##ui", theme.Primary);
            ImGui.TableNextColumn(); DrawThemeColor("Primary hover##ui", theme.PrimaryBright);
            ImGui.TableNextColumn(); DrawThemeColor("Accent##ui", theme.Accent);
            ImGui.TableNextColumn(); DrawThemeColor("Accent dim##ui", theme.AccentDim);
            ImGui.TableNextColumn(); DrawThemeColor("Main text##ui", theme.Text);
            ImGui.TableNextColumn(); DrawThemeColor("Muted text##ui", theme.MutedText);
            ImGui.TableNextColumn(); DrawThemeColor("Success / live##ui", theme.Success);
            ImGui.TableNextColumn(); DrawThemeColor("VIP metric##ui", theme.VipMetric);
            ImGui.TableNextColumn(); DrawThemeColor("Peak metric##ui", theme.PeakMetric);
            ImGui.TableNextColumn(); DrawThemeColor("Secondary metric##ui", theme.SecondaryMetric);
            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGui.TextColored(VelvetStyle.Gold, "SHAPE & SPACING");

        var corner = theme.CornerRounding;
        ImGui.SetNextItemWidth(220);
        if (ImGui.SliderFloat("Panel rounding##ui", ref corner, 0f, 20f, "%.0f px"))
        {
            theme.CornerRounding = corner;
            SaveUiTheme();
        }

        ImGui.SameLine();
        var frame = theme.FrameRounding;
        ImGui.SetNextItemWidth(220);
        if (ImGui.SliderFloat("Control rounding##ui", ref frame, 0f, 20f, "%.0f px"))
        {
            theme.FrameRounding = frame;
            SaveUiTheme();
        }

        var border = theme.BorderSize;
        ImGui.SetNextItemWidth(220);
        if (ImGui.SliderFloat("Border thickness##ui", ref border, 0f, 3f, "%.1f px"))
        {
            theme.BorderSize = border;
            SaveUiTheme();
        }

        ImGui.SameLine();
        var padding = theme.ControlPaddingY;
        ImGui.SetNextItemWidth(220);
        if (ImGui.SliderFloat("Control height##ui", ref padding, 2f, 18f, "%.0f"))
        {
            theme.ControlPaddingY = padding;
            SaveUiTheme();
        }

        var spacing = theme.ItemSpacing;
        ImGui.SetNextItemWidth(220);
        if (ImGui.SliderFloat("Item spacing##ui", ref spacing, 2f, 18f, "%.0f"))
        {
            theme.ItemSpacing = spacing;
            SaveUiTheme();
        }

        var sidebar = theme.SidebarWidth;
        ImGui.SetNextItemWidth(220);
        if (ImGui.SliderFloat("Sidebar width##ui", ref sidebar, 220f, 420f, "%.0f px"))
        {
            theme.SidebarWidth = sidebar;
            SaveUiTheme();
        }
        ImGui.SameLine();
        if (ImGui.Button("RESET WIDTH", VelvetStyle.ButtonSize("RESET WIDTH", 110f)))
        {
            theme.SidebarWidth = 232f;
            SaveUiTheme();
        }

        ImGui.Spacing();
        ImGui.TextColored(VelvetStyle.Muted,
            "UI packs contain presentation settings only. Importing one does not change venues, VIPs, reports, or attendance data.");

        ImGui.EndChild();
        ImGui.PopStyleColor(2);
    }

    private void DrawThemeColor(string label, ThemeColor color)
    {
        var value = color.ToVector4();
        ImGui.SetNextItemWidth(-1);
        if (!ImGui.ColorEdit4(label, ref value))
            return;

        color.Set(value);
        SaveUiTheme();
    }

    private void SaveUiTheme()
    {
        plugin.Configuration.UiTheme.Sanitize();
        plugin.Configuration.Save();
    }

    private void OpenUiPackImportDialog()
    {
        uiPackDialogs.OpenFileDialog(
            "Import Velvet Rope UI Pack",
            ".vrui",
            (success, path) =>
            {
                if (!success)
                    return;

                uiPackStatus = plugin.ImportUiPackFromFile(path);
            });
    }

    private void OpenUiAssetDialog(bool headerBackground)
    {
        uiPackDialogs.OpenFileDialog(
            headerBackground ? "Choose Velvet Rope Header Artwork" : "Choose Velvet Rope Logo",
            "Images{.png,.jpg,.jpeg,.webp}",
            (success, path) =>
            {
                if (!success)
                    return;

                uiPackStatus = plugin.SetUiAssetFromFile(path, headerBackground);
            });
    }

    private void OpenUiPackExportDialog()
    {
        uiPackDialogs.SaveFileDialog(
            "Export Velvet Rope UI Pack",
            ".vrui",
            "VelvetRope-UI-Pack",
            ".vrui",
            (success, path) =>
            {
                if (!success)
                    return;

                uiPackStatus = plugin.ExportUiPackToFile(path);
            });
    }

    private void DrawCategories()
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.CardRaised);
        ImGui.PushStyleColor(ImGuiCol.Border, VelvetStyle.GoldDim);
        ImGui.BeginChild("##categorySettings", new Vector2(-1, 420), true);
        ImGui.TextColored(VelvetStyle.Gold, "VIP CATEGORIES");
        ImGui.TextDisabled("Categories control the arrival accent and fallback greeting.");
        ImGui.Spacing();

        foreach (var category in plugin.Configuration.Categories.ToList())
        {
            ImGui.PushID(category.Id.ToString());
            var accent = VelvetStyle.CategoryAccent(category);

            ImGui.PushStyleColor(ImGuiCol.Header, VelvetStyle.Darken(accent, 0.72f));
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, VelvetStyle.Darken(accent, 0.52f));
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, VelvetStyle.Darken(accent, 0.35f));
            var open = ImGui.CollapsingHeader($"{category.Icon}  {category.Name}");
            ImGui.PopStyleColor(3);

            if (open)
            {
                ImGui.TextColored(accent, "CATEGORY ACCENT");

                var name = category.Name;
                ImGui.SetNextItemWidth(220);
                if (ImGui.InputText("Name", ref name, 40))
                {
                    category.Name = name;
                    plugin.Configuration.Save();
                }

                var icon = category.Icon;
                ImGui.SetNextItemWidth(100);
                if (ImGui.InputText("Icon", ref icon, 8))
                {
                    category.Icon = icon;
                    plugin.Configuration.Save();
                }

                var template = category.DefaultShoutTemplate;
                ImGui.TextColored(VelvetStyle.Gold, "DEFAULT SHOUT");
                if (ImGui.InputTextMultiline(
                        "##categoryTemplate",
                        ref template,
                        1000,
                        new Vector2(-1, 65)))
                {
                    category.DefaultShoutTemplate = template;
                    plugin.Configuration.Save();
                }

                var color = new Vector4(
                    category.AccentR,
                    category.AccentG,
                    category.AccentB,
                    category.AccentA);

                if (ImGui.ColorEdit4("Popup accent", ref color))
                {
                    category.AccentR = color.X;
                    category.AccentG = color.Y;
                    category.AccentB = color.Z;
                    category.AccentA = color.W;
                    plugin.Configuration.Save();
                }

                if (!category.BuiltIn)
                {
                    if (ImGui.Button("DELETE CATEGORY", VelvetStyle.ButtonSize("DELETE CATEGORY")))
                    {
                        var fallback = plugin.Configuration.Categories.First(c => c.Id != category.Id);
                        foreach (var venue in plugin.Configuration.Venues)
                        foreach (var link in venue.Vips.Where(v => v.CategoryId == category.Id))
                            link.CategoryId = fallback.Id;

                        plugin.Configuration.Categories.RemoveAll(c => c.Id == category.Id);
                        plugin.Configuration.Save();
                        ImGui.PopID();
                        break;
                    }
                }
                else
                {
                    ImGui.TextDisabled("Built-in category");
                }
            }

            ImGui.PopID();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextColored(VelvetStyle.Gold, "CREATE CUSTOM CATEGORY");

        ImGui.SetNextItemWidth(200);
        ImGui.InputText("Name##newCategory", ref newCategoryName, 40);

        ImGui.SetNextItemWidth(80);
        ImGui.InputText("Icon##newCategory", ref newCategoryIcon, 8);

        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("Default shout##newCategory", ref newCategoryTemplate, 500);

        VelvetStyle.PushAccentButton(VelvetStyle.VelvetBright);
        if (ImGui.Button("ADD CATEGORY", VelvetStyle.ButtonSize("ADD CATEGORY", 130)) && !string.IsNullOrWhiteSpace(newCategoryName))
        {
            plugin.Configuration.Categories.Add(new VipCategory
            {
                Name = newCategoryName.Trim(),
                Icon = string.IsNullOrWhiteSpace(newCategoryIcon) ? "★" : newCategoryIcon.Trim(),
                DefaultShoutTemplate = newCategoryTemplate.Trim(),
                BuiltIn = false
            });

            plugin.Configuration.Save();
            newCategoryName = string.Empty;
            newCategoryIcon = "★";
            newCategoryTemplate = "Welcome {name} to {venue}! ♥";
        }
        VelvetStyle.PopAccentButton();

        ImGui.EndChild();
        ImGui.PopStyleColor(2);
    }

    private void DrawVenueSelector()
    {
        var venue = plugin.SelectedVenue;
        ImGui.SetNextItemWidth(300);

        ImGui.BeginDisabled(plugin.ActiveSession is not null);
        if (ImGui.BeginCombo("Venue##selector", venue.Name))
        {
            foreach (var option in plugin.Configuration.Venues)
            {
                var selected = option.Id == venue.Id;
                if (ImGui.Selectable(option.Name, selected))
                    plugin.SelectVenue(option.Id);

                if (selected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }
        ImGui.EndDisabled();

        if (plugin.ActiveSession is not null)
        {
            ImGui.SameLine();
            ImGui.TextDisabled("Profile locked while shift is active");
        }
    }

    private bool DrawCategoryCombo(string label, ref Guid categoryId)
    {
        if (plugin.Configuration.Categories.Count == 0)
            return false;

        var selectedCategoryId = categoryId;
        if (selectedCategoryId == Guid.Empty ||
            plugin.Configuration.Categories.All(c => c.Id != selectedCategoryId))
        {
            categoryId = plugin.Configuration.Categories[0].Id;
        }

        var changed = false;
        var current = plugin.GetCategory(categoryId);
        ImGui.SetNextItemWidth(220);

        if (ImGui.BeginCombo(label, $"{current.Icon} {current.Name}"))
        {
            foreach (var category in plugin.Configuration.Categories)
            {
                var selected = category.Id == categoryId;
                if (ImGui.Selectable($"{category.Icon} {category.Name}", selected))
                {
                    categoryId = category.Id;
                    changed = true;
                }

                if (selected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        return changed;
    }

    private bool DrawVipDurationCombo(string label, ref VipDuration duration, float width = 220f)
    {
        var changed = false;
        ImGui.SetNextItemWidth(width);

        if (ImGui.BeginCombo(label, FormatVipDuration(duration)))
        {
            foreach (var option in Enum.GetValues<VipDuration>())
            {
                var selected = option == duration;
                if (ImGui.Selectable(FormatVipDuration(option), selected))
                {
                    duration = option;
                    changed = true;
                }

                if (selected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        return changed;
    }

    private static string FormatVipDuration(VipDuration duration) => duration switch
    {
        VipDuration.Nightly => "Nightly",
        VipDuration.Monthly => "Monthly",
        VipDuration.Yearly => "Yearly",
        VipDuration.Lifetime => "Lifetime",
        _ => "Lifetime"
    };

    private bool DefaultTieringForCategory(Guid categoryId)
    {
        var category = plugin.GetCategory(categoryId);
        return string.Equals(category.Name, "VIP", StringComparison.OrdinalIgnoreCase);
    }

    private void EnsureNewVipCategory()
    {
        if (newVipCategoryId == Guid.Empty ||
            plugin.Configuration.Categories.All(c => c.Id != newVipCategoryId))
        {
            newVipCategoryId = plugin.Configuration.Categories[0].Id;
        }
    }

    private static void DrawMetricCard(string label, string value, float width, Vector4 accent)
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.CardRaised);
        ImGui.PushStyleColor(ImGuiCol.Border, VelvetStyle.WithAlpha(accent, 0.70f));
        ImGui.BeginChild($"##metric_{label}", new Vector2(width, VelvetStyle.PanelHeight(2, extra: 8)), true);
        ImGui.TextColored(accent, label.ToUpperInvariant());
        ImGui.Spacing();
        ImGui.TextColored(VelvetStyle.Ivory, value);
        ImGui.EndChild();
        ImGui.PopStyleColor(2);
    }

    private static void DrawCompactUiPackHelp()
    {
        ImGui.TextDisabled("About UI packs  (?)");
        if (!ImGui.IsItemHovered())
            return;

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 430f);
        ImGui.TextColored(VelvetStyle.Gold, "UI PACKS CHANGE THE LOOK, NOT YOUR DATA");
        ImGui.Spacing();
        ImGui.TextWrapped(
            "UI packs are visual themes for Velvet Rope. They can change colors, spacing, branding, logos, and header artwork so the plugin can match a venue's style.");
        ImGui.Spacing();
        ImGui.TextWrapped(
            "Importing a UI pack does not change your venues, VIPs, shout messages, attendance totals, or saved reports.");
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    private static void DrawUiPackHelp()
    {
        ImGui.TextDisabled("What are UI packs?");
        var hovered = ImGui.IsItemHovered();
        ImGui.TextDisabled("Hover to learn more.");
        hovered |= ImGui.IsItemHovered();
        if (!hovered)
            return;

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 430f);
        ImGui.TextColored(VelvetStyle.Gold, "UI PACKS CHANGE THE LOOK, NOT YOUR DATA");
        ImGui.Spacing();
        ImGui.TextWrapped(
            "UI packs are visual themes for Velvet Rope. They can change colors, spacing, branding, logos, and header artwork so the plugin can match a venue's style.");
        ImGui.Spacing();
        ImGui.TextWrapped(
            "Importing a UI pack does not change your venues, VIPs, shout messages, attendance totals, or saved reports. You can switch packs or reset to Velvet Classic at any time.");
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    private static void DrawPrivacyFooter()
    {
        ImGui.Separator();
        ImGui.BeginGroup();
        ImGui.TextColored(VelvetStyle.Blue, "◆ PRIVACY-FIRST");
        ImGui.SameLine();
        ImGui.TextColored(VelvetStyle.Muted, "No general guest list is saved");
        ImGui.SameLine();
        ImGui.TextDisabled("• hover for details");
        ImGui.EndGroup();

        if (!ImGui.IsItemHovered())
            return;

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 430f);
        ImGui.TextColored(VelvetStyle.Blue, "YOUR GUESTS STAY PRIVATE");
        ImGui.Spacing();
        ImGui.TextWrapped(
            "Velvet Rope counts people during the current shift without creating a saved guest list. " +
            "When the shift ends, only attendance totals are kept, such as unique guests and peak attendance.");
        ImGui.Spacing();
        ImGui.TextWrapped(
            "VIPs are different: people you add to the VIP Directory are saved so Velvet Rope can recognize them and show arrival alerts.");
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

}
