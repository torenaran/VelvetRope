using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace VelvetRope.Windows;

internal static class VelvetStyle
{
    private static UiTheme current = UiTheme.CreateDefault();

    public static UiTheme Current => current;

    public static Vector4 Background => current.Background.ToVector4();
    public static Vector4 Card => current.Card.ToVector4();
    public static Vector4 CardRaised => current.CardRaised.ToVector4();
    public static Vector4 Velvet => current.Primary.ToVector4();
    public static Vector4 VelvetBright => current.PrimaryBright.ToVector4();
    public static Vector4 Gold => current.Accent.ToVector4();
    public static Vector4 GoldDim => current.AccentDim.ToVector4();
    public static Vector4 Ivory => current.Text.ToVector4();
    public static Vector4 Muted => current.MutedText.ToVector4();
    public static Vector4 Green => current.Success.ToVector4();
    public static Vector4 Purple => current.PeakMetric.ToVector4();
    public static Vector4 Blue => current.SecondaryMetric.ToVector4();
    public static Vector4 Pink => current.VipMetric.ToVector4();

    public static void Apply(UiTheme theme)
    {
        theme.Sanitize();
        current = theme;
    }

    public static Vector4 CategoryAccent(VipCategory category) => new(
        category.AccentR,
        category.AccentG,
        category.AccentB,
        category.AccentA);

    public static Vector4 WithAlpha(Vector4 color, float alpha) =>
        new(color.X, color.Y, color.Z, Math.Clamp(alpha, 0f, 1f));

    public static Vector4 Darken(Vector4 color, float amount, float alpha = 1f)
    {
        var scale = Math.Clamp(1f - amount, 0f, 1f);
        return new Vector4(color.X * scale, color.Y * scale, color.Z * scale, alpha);
    }

    public static void PushChrome()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Background);
        ImGui.PushStyleColor(ImGuiCol.Text, Ivory);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, Muted);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Card);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, CardRaised);
        ImGui.PushStyleColor(ImGuiCol.Border, GoldDim);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Darken(CardRaised, 0.02f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Darken(VelvetBright, 0.55f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Darken(VelvetBright, 0.40f));
        ImGui.PushStyleColor(ImGuiCol.Button, Velvet);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, VelvetBright);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, WithAlpha(VelvetBright, 1f));
        ImGui.PushStyleColor(ImGuiCol.Header, Darken(Velvet, 0.40f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Darken(VelvetBright, 0.28f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, Velvet);
        ImGui.PushStyleColor(ImGuiCol.CheckMark, Gold);
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, Gold);
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, WithAlpha(Gold, 1f));
        ImGui.PushStyleColor(ImGuiCol.Separator, GoldDim);

        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, current.CornerRounding);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, current.BorderSize);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, current.FrameRounding);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, current.BorderSize);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, current.CornerRounding);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, current.BorderSize);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8f, Math.Max(2f, current.ControlPaddingY / 2f)));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(current.ItemSpacing, current.ItemSpacing));
    }

    public static void PopChrome()
    {
        ImGui.PopStyleVar(8);
        ImGui.PopStyleColor(19);
    }

    public static void PushAccentButton(Vector4 accent)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, Darken(accent, 0.35f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Darken(accent, 0.12f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, accent);
    }

    public static void PopAccentButton() => ImGui.PopStyleColor(3);

    // ImGui users can run very different global font scales. Controls size from
    // the active font and the current UI pack rather than assuming fixed pixels.
    public static float ControlHeight(float extraY = 8f)
    {
        var effectiveY = Math.Max(extraY, current.ControlPaddingY);
        return Math.Max(ImGui.GetFrameHeight(), ImGui.GetTextLineHeight() + effectiveY);
    }

    public static Vector2 ButtonSize(string label, float minWidth = 0f, float extraX = 24f, float extraY = 8f)
    {
        var marker = label.IndexOf("##", StringComparison.Ordinal);
        var visibleLabel = marker >= 0 ? label[..marker] : label;
        var text = ImGui.CalcTextSize(visibleLabel);
        var effectiveY = Math.Max(extraY, current.ControlPaddingY);
        return new Vector2(
            Math.Max(minWidth, text.X + extraX),
            Math.Max(ControlHeight(effectiveY), text.Y + effectiveY));
    }

    public static float PanelHeight(int textLines, int frameRows = 0, float extra = 0f)
    {
        var style = ImGui.GetStyle();
        var textHeight = textLines * ImGui.GetTextLineHeightWithSpacing();
        var frameHeight = frameRows * (ImGui.GetFrameHeight() + style.ItemSpacing.Y);
        return style.WindowPadding.Y * 2f + textHeight + frameHeight + extra;
    }
}
