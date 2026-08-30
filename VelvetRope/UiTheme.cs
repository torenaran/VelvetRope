using System;
using System.Numerics;
using System.Linq;

namespace VelvetRope;

[Serializable]
public sealed class ThemeColor
{
    public float R { get; set; }
    public float G { get; set; }
    public float B { get; set; }
    public float A { get; set; } = 1f;

    public ThemeColor() { }

    public ThemeColor(float r, float g, float b, float a = 1f)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public Vector4 ToVector4() => new(R, G, B, A);

    public void Set(Vector4 value)
    {
        R = value.X;
        G = value.Y;
        B = value.Z;
        A = value.W;
    }

    public ThemeColor Clone() => new(R, G, B, A);
}

[Serializable]
public sealed class UiTheme
{
    public string PackName { get; set; } = "Velvet Classic";
    public string Author { get; set; } = "Velvet Rope";
    public string Description { get; set; } = "The signature velvet lounge theme.";

    public string BrandMark { get; set; } = "◆";
    public string BrandTitle { get; set; } = "VELVET ROPE";
    public string Tagline { get; set; } = "Venue guest & VIP management";

    // Schema 2 UI packs can carry local image assets. Asset names are simple
    // filenames; AssetPackId points to the extracted pack folder under the
    // plugin configuration directory.
    public string AssetPackId { get; set; } = string.Empty;
    public string LogoAsset { get; set; } = string.Empty;
    public string HeaderBackgroundAsset { get; set; } = string.Empty;

    public bool ShowHeaderLogo { get; set; } = false;
    public bool ShowSidebarLogo { get; set; } = false;
    public bool ShowHeaderBackground { get; set; } = false;
    public bool ShowBrandTitle { get; set; } = true;

    public float HeaderHeight { get; set; } = 76f;
    public float HeaderLogoHeight { get; set; } = 54f;
    public float SidebarLogoHeight { get; set; } = 112f;
    public float HeaderBackgroundOpacity { get; set; } = 0.70f;
    public float HeaderOverlayOpacity { get; set; } = 0.48f;
    public float LogoOpacity { get; set; } = 1.00f;

    public ThemeColor Background { get; set; } = new(0.055f, 0.030f, 0.052f);
    public ThemeColor Card { get; set; } = new(0.095f, 0.050f, 0.078f);
    public ThemeColor CardRaised { get; set; } = new(0.135f, 0.064f, 0.092f);
    public ThemeColor Primary { get; set; } = new(0.47f, 0.075f, 0.17f);
    public ThemeColor PrimaryBright { get; set; } = new(0.70f, 0.13f, 0.27f);
    public ThemeColor Accent { get; set; } = new(0.86f, 0.70f, 0.39f);
    public ThemeColor AccentDim { get; set; } = new(0.46f, 0.35f, 0.20f);
    public ThemeColor Text { get; set; } = new(0.95f, 0.91f, 0.84f);
    public ThemeColor MutedText { get; set; } = new(0.66f, 0.55f, 0.62f);
    public ThemeColor Success { get; set; } = new(0.38f, 0.83f, 0.60f);

    public ThemeColor VipMetric { get; set; } = new(0.90f, 0.38f, 0.58f);
    public ThemeColor PeakMetric { get; set; } = new(0.65f, 0.43f, 0.89f);
    public ThemeColor SecondaryMetric { get; set; } = new(0.38f, 0.66f, 0.91f);

    public float CornerRounding { get; set; } = 7f;
    public float FrameRounding { get; set; } = 5f;
    public float BorderSize { get; set; } = 1f;
    public float ControlPaddingY { get; set; } = 8f;
    public float ItemSpacing { get; set; } = 8f;
    public float SidebarWidth { get; set; } = 232f;

    public bool ShowBrandTagline { get; set; } = true;

    public static UiTheme CreateDefault() => new();

    public UiTheme Clone() => new()
    {
        PackName = PackName,
        Author = Author,
        Description = Description,
        BrandMark = BrandMark,
        BrandTitle = BrandTitle,
        Tagline = Tagline,
        AssetPackId = AssetPackId,
        LogoAsset = LogoAsset,
        HeaderBackgroundAsset = HeaderBackgroundAsset,
        ShowHeaderLogo = ShowHeaderLogo,
        ShowSidebarLogo = ShowSidebarLogo,
        ShowHeaderBackground = ShowHeaderBackground,
        ShowBrandTitle = ShowBrandTitle,
        HeaderHeight = HeaderHeight,
        HeaderLogoHeight = HeaderLogoHeight,
        SidebarLogoHeight = SidebarLogoHeight,
        HeaderBackgroundOpacity = HeaderBackgroundOpacity,
        HeaderOverlayOpacity = HeaderOverlayOpacity,
        LogoOpacity = LogoOpacity,
        Background = Background.Clone(),
        Card = Card.Clone(),
        CardRaised = CardRaised.Clone(),
        Primary = Primary.Clone(),
        PrimaryBright = PrimaryBright.Clone(),
        Accent = Accent.Clone(),
        AccentDim = AccentDim.Clone(),
        Text = Text.Clone(),
        MutedText = MutedText.Clone(),
        Success = Success.Clone(),
        VipMetric = VipMetric.Clone(),
        PeakMetric = PeakMetric.Clone(),
        SecondaryMetric = SecondaryMetric.Clone(),
        CornerRounding = CornerRounding,
        FrameRounding = FrameRounding,
        BorderSize = BorderSize,
        ControlPaddingY = ControlPaddingY,
        ItemSpacing = ItemSpacing,
        SidebarWidth = SidebarWidth,
        ShowBrandTagline = ShowBrandTagline
    };

    public void Sanitize()
    {
        // JSON packs are user-supplied. Explicit null color objects should not be
        // able to break the appearance editor or strand the user in a bad theme.
        Background ??= new ThemeColor(0.055f, 0.030f, 0.052f);
        Card ??= new ThemeColor(0.095f, 0.050f, 0.078f);
        CardRaised ??= new ThemeColor(0.135f, 0.064f, 0.092f);
        Primary ??= new ThemeColor(0.47f, 0.075f, 0.17f);
        PrimaryBright ??= new ThemeColor(0.70f, 0.13f, 0.27f);
        Accent ??= new ThemeColor(0.86f, 0.70f, 0.39f);
        AccentDim ??= new ThemeColor(0.46f, 0.35f, 0.20f);
        Text ??= new ThemeColor(0.95f, 0.91f, 0.84f);
        MutedText ??= new ThemeColor(0.66f, 0.55f, 0.62f);
        Success ??= new ThemeColor(0.38f, 0.83f, 0.60f);
        VipMetric ??= new ThemeColor(0.90f, 0.38f, 0.58f);
        PeakMetric ??= new ThemeColor(0.65f, 0.43f, 0.89f);
        SecondaryMetric ??= new ThemeColor(0.38f, 0.66f, 0.91f);

        PackName = Clean(PackName, "Custom UI Pack", 80);
        Author = Clean(Author, "Unknown", 80);
        Description = Clean(Description, string.Empty, 240);
        BrandMark = Clean(BrandMark, "◆", 8);
        BrandTitle = Clean(BrandTitle, "VELVET ROPE", 60);
        Tagline = Clean(Tagline, "Venue guest & VIP management", 120);
        AssetPackId = CleanAssetId(AssetPackId);
        LogoAsset = CleanAssetName(LogoAsset);
        HeaderBackgroundAsset = CleanAssetName(HeaderBackgroundAsset);

        ClampColor(Background);
        ClampColor(Card);
        ClampColor(CardRaised);
        ClampColor(Primary);
        ClampColor(PrimaryBright);
        ClampColor(Accent);
        ClampColor(AccentDim);
        ClampColor(Text);
        ClampColor(MutedText);
        ClampColor(Success);
        ClampColor(VipMetric);
        ClampColor(PeakMetric);
        ClampColor(SecondaryMetric);

        CornerRounding = Math.Clamp(CornerRounding, 0f, 20f);
        FrameRounding = Math.Clamp(FrameRounding, 0f, 20f);
        BorderSize = Math.Clamp(BorderSize, 0f, 3f);
        ControlPaddingY = Math.Clamp(ControlPaddingY, 2f, 18f);
        ItemSpacing = Math.Clamp(ItemSpacing, 2f, 18f);
        SidebarWidth = Math.Clamp(SidebarWidth, 220f, 420f);
        HeaderHeight = Math.Clamp(HeaderHeight, 62f, 180f);
        HeaderLogoHeight = Math.Clamp(HeaderLogoHeight, 28f, 140f);
        SidebarLogoHeight = Math.Clamp(SidebarLogoHeight, 48f, 220f);
        HeaderBackgroundOpacity = Math.Clamp(HeaderBackgroundOpacity, 0f, 1f);
        HeaderOverlayOpacity = Math.Clamp(HeaderOverlayOpacity, 0f, 1f);
        LogoOpacity = Math.Clamp(LogoOpacity, 0f, 1f);
    }

    private static string Clean(string? value, string fallback, int max)
    {
        var clean = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return clean.Length <= max ? clean : clean[..max];
    }

    private static string CleanAssetId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var clean = new string(value.Trim().Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        return clean.Length <= 100 ? clean : clean[..100];
    }

    private static string CleanAssetName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var clean = value.Trim();
        if (clean != System.IO.Path.GetFileName(clean))
            return string.Empty;

        return clean.Length <= 120 ? clean : string.Empty;
    }

    private static void ClampColor(ThemeColor color)
    {
        color.R = Math.Clamp(color.R, 0f, 1f);
        color.G = Math.Clamp(color.G, 0f, 1f);
        color.B = Math.Clamp(color.B, 0f, 1f);
        color.A = Math.Clamp(color.A, 0f, 1f);
    }
}

[Serializable]
public sealed class UiPackEnvelope
{
    public int Schema { get; set; } = 2;
    public string Kind { get; set; } = "ui-pack";
    public UiTheme Theme { get; set; } = UiTheme.CreateDefault();
}
