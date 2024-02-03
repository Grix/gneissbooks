using System;
using System.Globalization;
using System.Threading;
using Avalonia;
namespace GneissBooks.Desktop;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        CultureInfo myNumberCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        myNumberCulture.NumberFormat = new NumberFormatInfo
        {
            NaNSymbol = "NaN",
            NumberGroupSeparator = "",
            PercentGroupSeparator = "",
            CurrencyGroupSeparator = "",
            NumberDecimalSeparator = ".",
            PercentDecimalSeparator = ".",
            CurrencyDecimalSeparator = ".",
            NumberDecimalDigits = 2,
            CurrencyDecimalDigits = 2,
            PercentDecimalDigits = 0,
        };
        Thread.CurrentThread.CurrentCulture = myNumberCulture;
        CultureInfo.DefaultThreadCurrentCulture = myNumberCulture;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

}
