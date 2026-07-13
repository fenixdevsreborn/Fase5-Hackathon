using MudBlazor;

namespace ConexaoSolidaria.Web.Theme;

/// <summary>
/// Identidade visual do Conexao Solidaria aplicada ao MudThemeProvider.
/// Tokens: primary #0b3b60, secondary #0f7594, impacto #23a36d, destaque #f4b942,
/// superficie #ffffff, superficie alt #f4f7fa, texto #14212b / #52616d, perigo #c43838.
/// </summary>
public static class AppTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#0b3b60",
            Secondary = "#0f7594",
            Tertiary = "#23a36d",
            Info = "#0f7594",
            Success = "#23a36d",
            Warning = "#f4b942",
            Error = "#c43838",
            Background = "#f4f7fa",
            BackgroundGray = "#e9eff5",
            Surface = "#ffffff",
            AppbarBackground = "#0b3b60",
            AppbarText = "#ffffff",
            DrawerBackground = "#ffffff",
            DrawerText = "#14212b",
            DrawerIcon = "#52616d",
            TextPrimary = "#14212b",
            TextSecondary = "#52616d",
            ActionDefault = "#52616d",
            Divider = "#e2e8ee",
            LinesDefault = "#e2e8ee",
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "14px",
            AppbarHeight = "64px",
            DrawerWidthLeft = "260px",
        },
    };
}
