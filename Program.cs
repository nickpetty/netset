namespace NetSet;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // Dev helper: `NetSet.exe --export-icon netset.ico` regenerates the exe icon.
        if (args.Length == 2 && args[0] == "--export-icon")
        {
            IconFactory.ExportIco(args[1]);
            return;
        }

        using var mutex = new Mutex(true, @"Local\NetSet.SingleInstance", out bool first);
        if (!first) return;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp(showWindow: args.Contains("--show")));
    }
}
