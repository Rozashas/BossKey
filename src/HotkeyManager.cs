using System.ComponentModel;
using System.Runtime.InteropServices;

namespace BossKey;

internal sealed class HotkeyManager : NativeWindow, IMessageFilter, IDisposable
{
    internal static readonly int[] ModifierKeys = [0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0x5B, 0x5C];
    private Hotkey hotkey = new();
    private bool registered;
    internal bool Capturing { get; set; }
    internal event Action? Triggered;
    internal event Action<Hotkey?>? Captured;
    internal Hotkey Hotkey
    {
        get => hotkey;
        set => Apply(value);
    }

    internal HotkeyManager(Hotkey? initial = null)
    {
        CreateHandle(new CreateParams { Caption = "BossKey.Hotkey", Parent = new nint(-3) });
        try { Apply(initial ?? new()); }
        catch { DestroyHandle(); throw; }
        Application.AddMessageFilter(this);
    }

    private void Apply(Hotkey candidate)
    {
        if (!candidate.IsValid()) throw new ArgumentException("유효한 단축키 필요.");
        if (registered) Native.UnregisterHotKey(Handle, 1);
        if (!Register(candidate))
        {
            int error = Marshal.GetLastWin32Error();
            if (registered) registered = Register(hotkey);
            throw new Win32Exception(error, "단축키 등록 실패. 다른 프로그램 사용 여부 또는 시스템 예약키 확인.");
        }
        hotkey = candidate;
        registered = true;
    }

    private bool Register(Hotkey key)
    {
        uint modifiers = 0x4000;
        foreach (int modifier in key.Modifiers)
            modifiers |= modifier switch { 0xA4 or 0xA5 => 1u, 0xA2 or 0xA3 => 2u, 0xA0 or 0xA1 => 4u, _ => 8u };
        return Native.RegisterHotKey(Handle, 1, modifiers, (uint)key.Key);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x312 && !Capturing)
        {
            List<int> held = HeldModifiers();
            if (held.Order().SequenceEqual(hotkey.Modifiers.Order())) Triggered?.Invoke();
        }
        base.WndProc(ref message);
    }

    public bool PreFilterMessage(ref Message message)
    {
        if (!Capturing || (message.Msg != 0x100 && message.Msg != 0x104)) return false;
        int key = (int)message.WParam;
        if (ModifierKeys.Contains(key) || key is 0x10 or 0x11 or 0x12) return false;
        if (key == (int)Keys.Escape)
        {
            Capturing = false;
            Captured?.Invoke(null);
            return true;
        }
        Hotkey candidate = new() { Key = key, Modifiers = HeldModifiers() };
        if (!candidate.IsValid()) return true;
        Capturing = false;
        Captured?.Invoke(candidate);
        return true;
    }

    private static List<int> HeldModifiers() => ModifierKeys.Where(key => Native.GetAsyncKeyState(key) < 0).ToList();

    public void Dispose()
    {
        Application.RemoveMessageFilter(this);
        if (registered) Native.UnregisterHotKey(Handle, 1);
        registered = false;
        DestroyHandle();
    }
}
