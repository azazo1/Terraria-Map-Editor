using Avalonia;
using ReactiveUI.Avalonia;
using System;
using TEdit5.Diagnostics;

namespace TEdit5;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // 无界面自检: 不启动图形界面, 直接跑核心链路, 便于在 CI 上验证.
        if (args.Length > 0 && args[0] == "--smoke-test")
        {
            Environment.Exit(SmokeTest.RunAsync().GetAwaiter().GetResult());
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace()
                .UseReactiveUI(builder => { });
    }
}
