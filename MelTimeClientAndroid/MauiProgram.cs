using Android.Graphics.Drawables;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;

namespace MelTimeClientAndroid;

public static class MauiProgram
{
    /// <summary>Fond des champs de saisie : une cellule pleine, sans soulignement.</summary>
    private const string CouleurCelluleHaut = "#343434";
    private const string CouleurCelluleBas = "#242424";
    private const string CouleurBordure = "#454545";

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

        // Degrade du haut vers le bas et bordure claire : la cellule prend
        // ainsi du relief au lieu de rester un aplat.
        var fond = new GradientDrawable(
            GradientDrawable.Orientation.TopBottom,
            new[]
            {
                Android.Graphics.Color.ParseColor(CouleurCelluleHaut).ToArgb(),
                Android.Graphics.Color.ParseColor(CouleurCelluleBas).ToArgb()
            });

        fond.SetCornerRadius(RayonCellule * densite);
        fond.SetStroke(
            Math.Max(1, (int)densite),
            Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.ParseColor(CouleurBordure)));

        vue.Background = fond;

        // La teinte s'applique au fond : la laisser en place repeindrait
        // notre cellule, et une teinte transparente l'effacerait entierement.
        // On la retire donc purement et simplement.
        vue.BackgroundTintList = null;

        // Le texte se cale au centre de la cellule.
        if (vue is Android.Widget.TextView texte)
        {
            texte.Gravity = Android.Views.GravityFlags.CenterHorizontal;
        }
    }
}
