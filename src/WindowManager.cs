using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BossKey;

internal sealed record AppWindow(nint Handle, int ProcessId, long StartTicks, string Program, string Title);
internal sealed record HiddenWindow(AppWindow Window, Native.Placement Placement, Native.Rect? Bounds);

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
            Native.Rect? bounds = null;
            if (!Native.IsIconic(window.Handle) && !Native.IsZoomed(window.Handle) && Native.GetWindowRect(window.Handle, out Native.Rect rect))
                bounds = rect;
            hidden.Add(window.Handle, new(window, placement, bounds));
            Native.ShowWindow(window.Handle, 0);
            if (Native.IsWindowVisible(window.Handle)) hidden.Remove(window.Handle);
        }
        return hidden.Count;
    }

    internal void RestoreAll()
    {
        nint foreground = 0;
        // EnumWindows는 앞쪽 창부터 반환합니다. 뒤쪽부터 올려 기존 겹침 순서를 유지합니다.
        foreach (HiddenWindow saved in hidden.Values.Reverse().ToArray())
        {
            if (!Matches(saved.Window)) { hidden.Remove(saved.Window.Handle); continue; }
            Native.Placement placement = saved.Placement;
            bool restored = Native.SetWindowPlacement(saved.Window.Handle, ref placement);
            Native.ShowWindow(saved.Window.Handle, placement.ShowCommand == 3 ? 3 : placement.ShowCommand == 2 ? 2 : 8);
            // NormalPosition은 작업 영역 좌표입니다. 실제 화면 좌표는 별도로 저장해
            // 창이 다시 표시되며 위치·크기를 바꾸는 경우에도 숨기기 직전 상태로 맞춥니다.
            if (saved.Bounds is Native.Rect bounds)
            {
                const uint flags = 0x0004 | 0x0010 | 0x0200; // Z순서·활성 창·소유 창 순서 유지
                restored = Native.SetWindowPos(saved.Window.Handle, 0, bounds.Left, bounds.Top,
                    bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, flags) && restored;
                restored = Native.GetWindowRect(saved.Window.Handle, out Native.Rect actual) && actual.Equals(bounds) && restored;
            }
            if (!Native.IsIconic(saved.Window.Handle))
            {
                const uint frontFlags = 0x0001 | 0x0002 | 0x0010 | 0x0200; // 크기·위치 유지, 비활성 상태로 HWND_TOP에 올림
                restored = Native.SetWindowPos(saved.Window.Handle, 0, 0, 0, 0, 0, frontFlags) && restored;
                foreground = saved.Window.Handle;
            }
            if (restored && Native.IsWindowVisible(saved.Window.Handle)) hidden.Remove(saved.Window.Handle);
        }
        if (foreground != 0) Native.SetForegroundWindow(foreground);
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
