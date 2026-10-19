using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BossKey;

internal sealed record AppWindow(nint Handle, int ProcessId, long StartTicks, string Program, string Title);
internal sealed record HiddenWindow(AppWindow Window, Native.Placement Placement);

internal sealed class WindowManager
{
    private readonly Dictionary<nint, HiddenWindow> hidden = [];
    internal bool IsHidden => hidden.Count > 0;
    internal int Count => hidden.Count;

    internal static List<AppWindow> GetWindows()
    {
        List<AppWindow> result = [];
        Native.EnumWindows((window, _) =>
        {
            if (!Native.IsWindowVisible(window)) return true;
            string title = Native.GetTitle(window);
            if (string.IsNullOrWhiteSpace(title)) return true;
            Native.GetWindowThreadProcessId(window, out uint processId);
            if (processId == Environment.ProcessId) return true;
            try
            {
                using Process process = Process.GetProcessById((int)processId);
                string name = process.ProcessName;
                if (name is "explorer" or "ShellExperienceHost" or "StartMenuExperienceHost" or "SearchHost" or "ApplicationFrameHost") return true;
                result.Add(new(window, (int)processId, process.StartTime.ToUniversalTime().Ticks, name, title));
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
            return true;
        }, 0);
        return result;
    }

    internal int HideSelected(IEnumerable<string> programs, int? onlyProcess = null)
    {
        HashSet<string> selected = new(programs, StringComparer.OrdinalIgnoreCase);
        foreach (AppWindow window in GetWindows())
        {
            if (!selected.Contains(window.Program) || hidden.ContainsKey(window.Handle) || (onlyProcess != null && window.ProcessId != onlyProcess)) continue;
            Native.Placement placement = new() { Length = Marshal.SizeOf<Native.Placement>() };
            if (!Native.GetWindowPlacement(window.Handle, ref placement)) continue;
            hidden.Add(window.Handle, new(window, placement));
            Native.ShowWindow(window.Handle, 0);
            if (Native.IsWindowVisible(window.Handle)) hidden.Remove(window.Handle);
        }
        return hidden.Count;
    }

    internal void RestoreAll()
    {
        foreach (HiddenWindow saved in hidden.Values.ToArray())
        {
            if (!Matches(saved.Window)) { hidden.Remove(saved.Window.Handle); continue; }
            Native.Placement placement = saved.Placement;
            Native.SetWindowPlacement(saved.Window.Handle, ref placement);
            Native.ShowWindow(saved.Window.Handle, placement.ShowCommand == 3 ? 3 : placement.ShowCommand == 2 ? 2 : 8);
            if (Native.IsWindowVisible(saved.Window.Handle)) hidden.Remove(saved.Window.Handle);
        }
    }

    private static bool Matches(AppWindow window)
    {
        if (!Native.IsWindow(window.Handle)) return false;
        Native.GetWindowThreadProcessId(window.Handle, out uint id);
        if (id != window.ProcessId) return false;
        try
        {
            using Process process = Process.GetProcessById(window.ProcessId);
            return process.StartTime.ToUniversalTime().Ticks == window.StartTicks;
        }
        catch { return false; }
    }
}
