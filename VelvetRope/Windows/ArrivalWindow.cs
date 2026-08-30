using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace VelvetRope.Windows;

public sealed class ArrivalWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private DateTime copiedUntilUtc = DateTime.MinValue;
    private string copiedLabel = string.Empty;

    public ArrivalWindow(Plugin plugin)
        : base("Velvet Rope Arrival###VelvetRopeArrival",
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.plugin = plugin;
        IsOpen = false;
        ShowCloseButton = false;
        RespectCloseHotkey = false;
        IsTopMost = true;
        Position = new Vector2(500, 250);
        PositionCondition = ImGuiCond.Appearing;
    }

    public void Dispose() { }

    public override bool DrawConditions() => plugin.CurrentAlert is not null;

    public override void Draw()
    {
        var alert = plugin.CurrentAlert;
        if (alert is null)
            return;

        VelvetStyle.Apply(plugin.Configuration.UiTheme);
        VelvetStyle.PushChrome();

        var accent = new Vector4(
            alert.AccentR,
            alert.AccentG,
            alert.AccentB,
            alert.AccentA);

        var popupWidth = Math.Clamp(
            Math.Max(560f, ImGui.CalcTextSize(alert.CharacterDisplay).X + 100f),
            560f,
            760f);
        var messageHeight = Math.Max(
            VelvetStyle.PanelHeight(2, extra: 10),
            ImGui.CalcTextSize(alert.Message, false, popupWidth - 34f).Y + VelvetStyle.PanelHeight(1, extra: 20));

        var theme = plugin.Configuration.UiTheme;
        var heroHeight = Math.Max(VelvetStyle.PanelHeight(3, extra: 10), Math.Min(theme.HeaderHeight, 130f));

        ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.Darken(accent, 0.84f));
        ImGui.PushStyleColor(ImGuiCol.Border, accent);
        ImGui.BeginChild("##arrivalHero", new Vector2(popupWidth, heroHeight), true);

        var heroPos = ImGui.GetWindowPos();
        var heroSize = ImGui.GetWindowSize();
        var drawList = ImGui.GetWindowDrawList();
        if (theme.ShowHeaderBackground)
        {
            var background = plugin.GetUiAssetTexture(theme.HeaderBackgroundAsset);
            if (background is not null)
            {
                drawList.AddImage(
                    background.Handle,
                    heroPos + Vector2.One,
                    heroPos + heroSize - Vector2.One,
                    Vector2.Zero,
                    Vector2.One,
                    ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, theme.HeaderBackgroundOpacity)));
                var overlay = VelvetStyle.Darken(accent, 0.90f);
                overlay.W = Math.Max(0.30f, theme.HeaderOverlayOpacity);
                drawList.AddRectFilled(
                    heroPos + Vector2.One,
                    heroPos + heroSize - Vector2.One,
                    ImGui.ColorConvertFloat4ToU32(overlay),
                    theme.CornerRounding);
            }
        }

        var textX = 12f;
        var logo = theme.ShowHeaderLogo ? plugin.GetUiAssetTexture(theme.LogoAsset) : null;
        if (logo is not null && logo.Height > 0)
        {
            var logoHeight = Math.Min(theme.HeaderLogoHeight, heroHeight - 18f);
            var logoWidth = logoHeight * logo.Width / (float)logo.Height;
            ImGui.SetCursorPos(new Vector2(12f, Math.Max(8f, (heroHeight - logoHeight) * 0.5f)));
            ImGui.Image(logo.Handle, new Vector2(logoWidth, logoHeight), Vector2.Zero, Vector2.One,
                new Vector4(1f, 1f, 1f, theme.LogoOpacity));
            textX += logoWidth + 12f;
        }

        ImGui.SetCursorPos(new Vector2(textX, 11f));
        if (theme.ShowBrandTitle)
            ImGui.TextColored(VelvetStyle.Gold, $"{theme.BrandMark}  {theme.BrandTitle}");
        else
            ImGui.TextColored(VelvetStyle.Gold, theme.PackName);
        ImGui.SameLine();
        ImGui.TextDisabled(alert.VenueName);

        ImGui.SetCursorPos(new Vector2(textX, 11f + ImGui.GetTextLineHeightWithSpacing() + 7f));
        var arrivalLabel = alert.PublicAnnouncementEnabled
            ? $"{alert.CategoryIcon}  {alert.CategoryName.ToUpperInvariant()} ARRIVAL"
            : $"{alert.CategoryIcon}  SILENT {alert.CategoryName.ToUpperInvariant()} ARRIVAL";
        ImGui.TextColored(accent, arrivalLabel);
        ImGui.SetCursorPosX(textX);
        ImGui.Text(alert.CharacterDisplay);

        ImGui.EndChild();
        ImGui.PopStyleColor(2);

        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.ChildBg, VelvetStyle.Card);
        ImGui.PushStyleColor(ImGuiCol.Border, VelvetStyle.GoldDim);
        ImGui.BeginChild("##message", new Vector2(popupWidth, messageHeight), true);
        ImGui.TextColored(VelvetStyle.Gold, alert.PublicAnnouncementEnabled ? "PREPARED SHOUT" : "STAFF NOTICE");
        ImGui.Spacing();
        ImGui.TextWrapped(alert.Message);
        ImGui.EndChild();
        ImGui.PopStyleColor(2);

        ImGui.Spacing();

        if (alert.PublicAnnouncementEnabled)
        {
            VelvetStyle.PushAccentButton(accent);
            if (ImGui.Button("COPY SHOUT", VelvetStyle.ButtonSize("COPY SHOUT", 160, extraY: 10)))
            {
                ImGui.SetClipboardText(alert.CopyText);
                copiedLabel = "Shout copied";
                copiedUntilUtc = DateTime.UtcNow.AddSeconds(2);
            }
            VelvetStyle.PopAccentButton();
            ImGui.SameLine();
        }

        if (ImGui.Button("COPY NAME", VelvetStyle.ButtonSize("COPY NAME", 125, extraY: 10)))
        {
            ImGui.SetClipboardText(alert.CharacterDisplay);
            copiedLabel = "Name copied";
            copiedUntilUtc = DateTime.UtcNow.AddSeconds(2);
        }

        ImGui.SameLine();

        if (ImGui.Button("DISMISS", VelvetStyle.ButtonSize("DISMISS", 125, extraY: 10)))
            plugin.DismissCurrentAlert();

        if (DateTime.UtcNow < copiedUntilUtc)
        {
            ImGui.SameLine();
            ImGui.TextColored(VelvetStyle.Green, $"● {copiedLabel}");
        }

        ImGui.Spacing();
        ImGui.TextColored(
            VelvetStyle.Muted,
            alert.PublicAnnouncementEnabled
                ? "Nothing is sent automatically. COPY SHOUT prepares the /sh line for you."
                : "This VIP is marked silent. Velvet Rope alerts staff without preparing a public /sh line.");

        if (plugin.PendingAlertCount > 0)
            ImGui.TextColored(VelvetStyle.Gold, $"◆ {plugin.PendingAlertCount} more VIP alert(s) waiting");

        VelvetStyle.PopChrome();
    }
}
