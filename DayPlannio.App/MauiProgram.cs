using DayPlannio.App.Views;
using Microsoft.Extensions.Logging;

namespace DayPlannio.App
{
    public static class MauiProgram
    {
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

            string baseUrl;

#if ANDROID
            baseUrl = "http://10.0.2.2:5143/";
#else
            baseUrl = "http://localhost:5143/";
#endif

            builder.Services.AddSingleton(new HttpClient
            {
                BaseAddress = new Uri(baseUrl)
            });

            builder.Services.AddTransient<Login>();
            builder.Services.AddTransient<CadastroUsuario>();
            builder.Services.AddSingleton<App>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
            {
#if WINDOWS
    handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
#endif
#if ANDROID
                handler.PlatformView.BackgroundTintList =
                    Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
                handler.PlatformView.SetPadding(50, 40, 50, 40);
#endif
            });

            Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
            {
#if ANDROID
                handler.PlatformView.BackgroundTintList =
                    Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
                handler.PlatformView.SetPadding(50, 40, 50, 40);
#endif
            });

            return builder.Build();
        }
    }
}