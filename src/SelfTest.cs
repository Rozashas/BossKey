using System.Diagnostics;
using System.Text.Json;

namespace BossKey;

internal static class SelfTest
{
    internal static int Run()
    {
        List<string> checks = [];
        int exitCode = 1;
        bool keyboardInputValidated = false;
        using Form host = new() { Text = "BossKey validation", Width = 400, Height = 120, ShowInTaskbar = false };
        host.Shown += async (_, _) =>
        {
            Process? helper = null;
            WindowManager manager = new();
            try
            {
                helper = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--test-window") { UseShellExecute = false });
                if (helper == null) throw new Exception("Helper launch failed");
                List<AppWindow> targets = [];
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    await Task.Delay(100);
                    targets = WindowManager.GetWindows().Where(window => window.ProcessId == helper.Id).ToList();
                    if (targets.Count == 3) break;
                }
                Check(targets.Count == 3, "Enumerate three real windows", checks);
                AppWindow normal = targets.Single(window => window.Title.EndsWith("Normal"));
                AppWindow maximized = targets.Single(window => window.Title.EndsWith("Maximized"));
                AppWindow minimized = targets.Single(window => window.Title.EndsWith("Minimized"));
                Native.Placement original = new() { Length = System.Runtime.InteropServices.Marshal.SizeOf<Native.Placement>() };
                Native.GetWindowPlacement(normal.Handle, ref original);
                Check(Native.IsZoomed(maximized.Handle) && Native.IsIconic(minimized.Handle), "Maximized and minimized baseline", checks);
                host.Activate();
                await Task.Delay(250);
                using HotkeyManager hook = new();
                int triggers = 0;
                hook.Triggered += () => host.BeginInvoke(() =>
                {
                    triggers++;
                    if (manager.IsHidden) manager.RestoreAll();
                    else manager.HideSelected(targets.Select(window => window.Program), helper.Id);
                });
                await Send(0xA5, 0xC0);
                await Task.Delay(180);
                if (triggers != 0) throw new Exception("Right Alt incorrectly triggered left Alt shortcut");
                await Send(0xA4, 0xC0, repeat: true);
                await Task.Delay(200);
                keyboardInputValidated = triggers > 0;
                if (keyboardInputValidated)
                {
                    Check(triggers == 1 && manager.Count == 3, "Left Alt + backtick hides windows once despite repeat", checks);
                    await Send(0xA4, 0xC0);
                    await Task.Delay(200);
                    Check(triggers == 2 && !manager.IsHidden, "Second shortcut restores windows", checks);
                }
                else
                {
                    checks.Add("UNVERIFIED: Injected keyboard events were not delivered in this session. Physical shortcut and capture require manual verification.");
                    Check(manager.HideSelected(["BossKey-no-such-program"], helper.Id) == 0, "Unselected programs remain visible", checks);
                    Check(manager.HideSelected(targets.Select(window => window.Program), helper.Id) == 3, "Hide all three selected real windows", checks);
                    Check(targets.All(window => !Native.IsWindowVisible(window.Handle)), "Hidden windows are not visible", checks);
                    manager.RestoreAll();
                }
                Check(!manager.IsHidden && targets.All(window => Native.IsWindowVisible(window.Handle)), "Restore all three real windows", checks);
                Check(Native.IsZoomed(maximized.Handle) && Native.IsIconic(minimized.Handle), "Preserve maximized and minimized state", checks);
                Native.Placement restored = new() { Length = original.Length };
                Native.GetWindowPlacement(normal.Handle, ref restored);
                Check(original.NormalPosition.Equals(restored.NormalPosition), "Preserve normal window bounds", checks);
                Hotkey custom = new() { Key = (int)Keys.F8, Modifiers = [0xA2] };
                hook.Hotkey = custom;
                Check(hook.Hotkey.Key == (int)Keys.F8, "Register custom left Ctrl + F8 with Windows", checks);
                bool conflictDetected = false;
                try { using HotkeyManager conflict = new(custom); }
                catch (System.ComponentModel.Win32Exception) { conflictDetected = true; }
                Check(conflictDetected, "Detect shortcut registration conflict", checks);
                using (HotkeyManager previous = new())
                {
                    bool refused = false;
                    try { hook.Hotkey = new(); }
                    catch (System.ComponentModel.Win32Exception) { refused = true; }
                    Check(refused && hook.Hotkey.Key == (int)Keys.F8, "Keep previous shortcut when replacement conflicts", checks);
                }
                manager.HideSelected(targets.Select(window => window.Program), helper.Id);
                manager.RestoreAll();
                Check(targets.All(window => Native.IsWindowVisible(window.Handle)), "Shutdown restoration path", checks);
                Settings sample = new() { Hotkey = custom, Programs = ["test-a", "test-b"] };
                Settings copy = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(sample))!;
                Check(copy.Hotkey.IsValid() && copy.Programs.SequenceEqual(sample.Programs), "Multi-selection and hotkey JSON round trip", checks);
                using MainForm preview = new();
                preview.Show();
                await Task.Delay(200);
                using Bitmap screenshot = new(preview.Width, preview.Height);
                preview.DrawToBitmap(screenshot, new Rectangle(Point.Empty, screenshot.Size));
                screenshot.Save(Path.Combine(AppContext.BaseDirectory, "ui-preview.png"));
                preview.Close();
                checks.Add("PASS: Main settings form renders");
                exitCode = 0;
            }
            catch (Exception error) { checks.Add("FAIL: " + error); }
            finally
            {
                manager.RestoreAll();
                if (helper != null)
                {
                    if (!helper.HasExited) helper.Kill(true);
                    helper.Dispose();
                }
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "self-test.json"), JsonSerializer.Serialize(new { passed = exitCode == 0, keyboardInputValidated, checks }, new JsonSerializerOptions { WriteIndented = true }));
                host.Close();
            }
        };
        Application.Run(host);
        return exitCode;
    }

    private static void Check(bool result, string name, List<string> checks)
    {
        if (!result) throw new Exception(name);
        checks.Add("PASS: " + name);
    }

    private static async Task Send(byte modifier, byte key, bool repeat = false)
    {
        uint extended = modifier == 0xA5 ? 1u : 0u;
        await Task.Run(() => Inject(modifier, extended));
        await Task.Delay(60);
        await Task.Run(() => Inject(key, 0));
        if (repeat) await Task.Run(() => Inject(key, 0));
        await Task.Delay(60);
        await Task.Run(() => { Inject(key, 2); Inject(modifier, extended | 2); });
    }

    private static void Inject(byte key, uint flags)
    {
        Native.Input input = new() { Type = 1, Data = new() { Keyboard = new() { Key = key, Flags = flags } } };
        uint sent = Native.SendInput(1, [input], System.Runtime.InteropServices.Marshal.SizeOf<Native.Input>());
        if (sent != 1) throw new Exception($"SendInput blocked: {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
    }
}
