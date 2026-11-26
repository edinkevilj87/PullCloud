using System.Runtime.InteropServices;

namespace PullCloud;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        HideOwnConsole();
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }

    // When started as "dotnet PullCloud.dll" (see the launcher), dotnet.exe gets a console window.
    // Hide it – but only if the console belongs to us alone, never a terminal the user started us from.
    private static void HideOwnConsole()
    {
        try
        {
            var hwnd = GetConsoleWindow();
            if (hwnd == IntPtr.Zero) return;
            var pids = new uint[4];
            if (GetConsoleProcessList(pids, (uint)pids.Length) != 1) return;
            ShowWindow(hwnd, 0);
            FreeConsole();
        }
        catch { /* cosmetic only */ }
    }

    [DllImport("kernel32.dll")] private static extern IntPtr GetConsoleWindow();
    [DllImport("kernel32.dll")] private static extern uint GetConsoleProcessList(uint[] list, uint count);
    [DllImport("kernel32.dll")] private static extern bool FreeConsole();
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
