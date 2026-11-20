using Android.Content.Res;
using Android.Graphics.Drawables;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;

namespace MelTimeClientAndroid;

public static class MauiProgram
{
    /// <summary>Fond des champs de saisie : une cellule pleine, sans soulignement.</summary>
    private const string CouleurCellule = "#111111";

    private const float RayonCellule = 8f;

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Les champs d'Android sont dessines avec un simple trait en bas, dans
        // le style Material. On le remplace par une cellule pleine et arrondie,
        // identique sur tous les ecrans de saisie.
        AppliquerStyleCellule();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static void AppliquerStyleCellule()
    {
        EntryHandler.Mapper.AppendToMapping("Cellule", (handler, _) =>
            Habiller(handler.PlatformView, handler));

        EditorHandler.Mapper.AppendToMapping("Cellule", (handler, _) =>
            Habiller(handler.PlatformView, handler));

        PickerHandler.Mapper.AppendToMapping("Cellule", (handler, _) =>
            Habiller(handler.PlatformView, handler));

        DatePickerHandler.Mapper.AppendToMapping("Cellule", (handler, _) =>
            Habiller(handler.PlatformView, handler));

        TimePickerHandler.Mapper.AppendToMapping("Cellule", (handler, _) =>
            Habiller(handler.PlatformView, handler));
    }

    /// <summary>
    /// Applique un fond plein arrondi et retire la teinte de fond, qui est
    /// justement le trait dessine sous le champ.
    /// </summary>
    private static void Habiller(Android.Views.View? vue, IViewHandler handler)
    {
        if (vue is null || handler.MauiContext?.Context is not { } contexte)
        {
            return;
        }

        var densite = contexte.Resources?.DisplayMetrics?.Density ?? 1f;

        var fond = new GradientDrawable();
        fond.SetColor(Android.Graphics.Color.ParseColor(CouleurCellule));
        fond.SetCornerRadius(RayonCellule * densite);

        vue.Background = fond;

        // La teinte de fond est ce qui dessine le trait Material : sans elle,
        // seul le drawable ci-dessus reste visible.
        vue.BackgroundTintList = ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
    }
}
