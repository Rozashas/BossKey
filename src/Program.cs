using System.Diagnostics;

namespace BossKey;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--position-test")) return SelfTest.RunPositionTest();
        if (args.Contains("--self-test")) return SelfTest.Run();
        if (args.Contains("--test-window"))
        {
            Form first = new() { Text = "BossKey TEST Normal", Width = 450, Height = 250, StartPosition = FormStartPosition.Manual, Location = new(70, 70) };
            Form second = new() { Text = "BossKey TEST Maximized", Width = 400, Height = 240 };
            Form third = new() { Text = "BossKey TEST Minimized", Width = 400, Height = 240 };
            first.Shown += (_, _) => { second.Show(); second.WindowState = FormWindowState.Maximized; third.Show(); third.WindowState = FormWindowState.Minimized; };
            Application.Run(first);
            second.Dispose(); third.Dispose();
            return 0;
        }
        using Mutex instance = new(true, "Local\\BossKey.Desktop.SingleInstance", out bool created);
        if (!created)
        {
            MessageBox.Show("BossKey 실행 중. 작업 표시줄의 트레이 아이콘에서 설정 열기.", "BossKey");
            return 0;
        }
        try
        {
            using MainForm form = new();
            Application.Run(form);
            return 0;
        }
        catch (Exception error)
        {
            MessageBox.Show("실행 실패: " + error.Message, "BossKey", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}

internal sealed class MainForm : Form
{
    private readonly Settings settings;
    private readonly WindowManager windows = new();
    private readonly HotkeyManager keyboard;
    private readonly ListView programs = new();
    private readonly Label status = new();
    private readonly Label shortcut = new();
    private readonly Button capture = new();
    private readonly Button toggle = new();
    private readonly NotifyIcon tray;
    private readonly ContextMenuStrip trayMenu = new();
    private readonly System.Windows.Forms.Timer refreshTimer = new() { Interval = 3000 };
    private bool refreshing;
    private bool exitRequested;

    internal MainForm()
    {
        settings = Settings.Load(out string warning);
        Text = "BossKey";
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? SystemIcons.Application;
        Font = new Font("맑은 고딕", 10);
        BackColor = Color.FromArgb(20, 23, 30);
        ForeColor = Color.FromArgb(225, 230, 240);
        ClientSize = new Size(840, 640);
        MinimumSize = new Size(760, 620);
        StartPosition = FormStartPosition.CenterScreen;
        BuildLayout();
        keyboard = new(settings.Hotkey);
        keyboard.Triggered += () => BeginInvoke(ToggleWindows);
        keyboard.Captured += key => BeginInvoke(() => ApplyHotkey(key));
        trayMenu.Items.Add("설정 열기", null, (_, _) => OpenSettings());
        trayMenu.Items.Add("숨기기 / 복원", null, (_, _) => ToggleWindows());
        trayMenu.Items.Add("모든 창 복원", null, (_, _) => RestoreWindows());
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("종료 (창 복원)", null, (_, _) => ExitApplication());
        tray = new() { Icon = Icon, Text = "BossKey · " + settings.Hotkey, ContextMenuStrip = trayMenu, Visible = true };
        tray.DoubleClick += (_, _) => OpenSettings();
        refreshTimer.Tick += (_, _) => { if (!windows.IsHidden) RefreshPrograms(); };
        refreshTimer.Start();
        RefreshPrograms();
        UpdateShortcut();
        SetStatus(warning.Length > 0 ? warning : "준비 완료. 체크한 프로그램의 모든 창에 적용.");
        Application.ThreadException += OnThreadException;
    }

    private void BuildLayout()
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 7 };
        layout.RowStyles.Add(new(SizeType.Absolute, 66));
        layout.RowStyles.Add(new(SizeType.Absolute, 80));
        layout.RowStyles.Add(new(SizeType.Absolute, 38));
        layout.RowStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.Absolute, 48));
        layout.RowStyles.Add(new(SizeType.Absolute, 58));
        layout.RowStyles.Add(new(SizeType.Absolute, 44));
        Label title = new() { Text = "B  BossKey", Font = new Font("Segoe UI", 26, FontStyle.Bold), ForeColor = ForeColor, Dock = DockStyle.Fill };
        layout.Controls.Add(title);
        FlowLayoutPanel hotkeys = new() { Dock = DockStyle.Fill, WrapContents = true };
        shortcut.AutoSize = false; shortcut.Size = new Size(330, 34); shortcut.TextAlign = ContentAlignment.MiddleLeft;
        capture.Text = "단축키 변경"; StyleButton(capture, 140); capture.Click += (_, _) => StartCapture();
        Button defaults = MakeButton("기본키 복원", () => ApplyHotkey(new()), 140);
        hotkeys.Controls.AddRange([shortcut, capture, defaults, new Label { Text = "변경: Ctrl / Alt / Shift / Win + 일반 키 입력. 좌·우 구분. Esc 취소.", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }]);
        layout.Controls.Add(hotkeys);
        layout.Controls.Add(new Label { Text = "대상 프로그램   ·   여러 개 체크 가능   ·   선택 자동 저장", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft });
        programs.Dock = DockStyle.Fill; programs.View = View.Details; programs.CheckBoxes = true;
        programs.FullRowSelect = true; programs.HideSelection = false;
        programs.BackColor = Color.FromArgb(27, 31, 40); programs.ForeColor = ForeColor;
        programs.OwnerDraw = true;
        programs.DrawColumnHeader += (_, e) =>
        {
            using SolidBrush brush = new(Color.FromArgb(40, 46, 60));
            e.Graphics.FillRectangle(brush, e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.Header!.Text, Font, e.Bounds, ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        };
        programs.DrawItem += (_, e) => e.DrawDefault = true;
        programs.DrawSubItem += (_, e) => e.DrawDefault = true;
        programs.BorderStyle = BorderStyle.None; programs.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        programs.Columns.Add("프로그램", 220); programs.Columns.Add("실행 상태 / 창 제목", 490);
        programs.ItemChecked += (_, _) => { if (!refreshing) BeginInvoke(SaveSelection); };
        layout.Controls.Add(programs);
        FlowLayoutPanel listButtons = new() { Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 0) };
        listButtons.Controls.Add(MakeButton("목록 새로고침", RefreshPrograms, 150));
        listButtons.Controls.Add(MakeButton("전체 선택", () => SelectAll(true), 120));
        listButtons.Controls.Add(MakeButton("선택 해제", () => SelectAll(false), 120));
        layout.Controls.Add(listButtons);
        FlowLayoutPanel actions = new() { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
        toggle.Text = "선택 프로그램 숨기기"; StyleButton(toggle, 220); toggle.BackColor = Color.FromArgb(65, 102, 220);
        toggle.Click += (_, _) => ToggleWindows();
        actions.Controls.Add(toggle);
        actions.Controls.Add(MakeButton("모든 창 복원", RestoreWindows, 160));
        actions.Controls.Add(MakeButton("트레이로 보내기", HideToTray, 170));
        layout.Controls.Add(actions);
        status.Dock = DockStyle.Fill; status.TextAlign = ContentAlignment.MiddleLeft;
        layout.Controls.Add(status);
        Controls.Add(layout);
    }

    private static void StyleButton(Button button, int width)
    {
        button.Size = new Size(width, 34);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = Color.FromArgb(40, 46, 60);
        button.ForeColor = Color.FromArgb(225, 230, 240);
        button.Cursor = Cursors.Hand;
    }

    private static Button MakeButton(string text, Action action, int width)
    {
        Button button = new() { Text = text };
        StyleButton(button, width);
        button.Click += (_, _) => action();
        return button;
    }

    private void RefreshPrograms()
    {
        if (windows.IsHidden) { SetStatus("창 복원 후 목록 갱신 가능."); return; }
        string? focused = programs.FocusedItem?.Text;
        var running = WindowManager.GetWindows().GroupBy(window => window.Program, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => string.Join(" | ", group.Select(window => window.Title)), StringComparer.OrdinalIgnoreCase);
        string[] names = running.Keys.Concat(settings.Programs).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        refreshing = true;
        programs.BeginUpdate();
        programs.Items.Clear();
        foreach (string name in names)
        {
            ListViewItem row = new(name) { Checked = settings.Programs.Contains(name, StringComparer.OrdinalIgnoreCase) };
            row.SubItems.Add(running.GetValueOrDefault(name, "실행 대기 · 선택 유지"));
            programs.Items.Add(row);
            if (name == focused) row.Focused = true;
        }
        programs.EndUpdate();
        refreshing = false;
    }

    private void SelectAll(bool selected)
    {
        if (windows.IsHidden) { SetStatus("창 복원 후 대상 변경 가능."); return; }
        refreshing = true;
        foreach (ListViewItem row in programs.Items) row.Checked = selected;
        refreshing = false;
        SaveSelection();
    }

    private void SaveSelection()
    {
        settings.Programs = programs.CheckedItems.Cast<ListViewItem>().Select(row => row.Text).ToList();
        SaveSettings();
    }

    private void SaveSettings()
    {
        try { settings.Save(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            SetStatus("설정 저장 실패. 현재 실행에는 적용됨: " + error.Message);
        }
    }

    private void StartCapture()
    {
        keyboard.Capturing = true;
        capture.Text = "키 입력 대기…";
        SetStatus("원하는 조합 입력. Ctrl / Alt / Shift / Win 중 하나 이상 필요. Esc 취소.");
    }

    private void ApplyHotkey(Hotkey? key)
    {
        keyboard.Capturing = false;
        capture.Text = "단축키 변경";
        if (key == null) { SetStatus("단축키 변경 취소."); return; }
        try { keyboard.Hotkey = key; }
        catch (System.ComponentModel.Win32Exception error) { SetStatus(error.Message); return; }
        settings.Hotkey = key;
        UpdateShortcut();
        SetStatus("단축키 적용: " + key);
        SaveSettings();
    }

    private void UpdateShortcut()
    {
        shortcut.Text = "현재 단축키   " + settings.Hotkey;
        if (tray != null) tray.Text = ("BossKey · " + settings.Hotkey)[..Math.Min(63, ("BossKey · " + settings.Hotkey).Length)];
    }

    private void ToggleWindows()
    {
        if (windows.IsHidden) { RestoreWindows(); return; }
        int count = windows.HideSelected(settings.Programs);
        programs.Enabled = !windows.IsHidden;
        toggle.Text = windows.IsHidden ? "숨긴 창 복원" : "선택 프로그램 숨기기";
        SetStatus(count > 0 ? $"{count}개 창 숨김. 같은 단축키로 복원." : "숨길 창 없음. 실행 중인 대상 프로그램 체크 필요.");
    }

    private void RestoreWindows()
    {
        windows.RestoreAll();
        programs.Enabled = !windows.IsHidden;
        toggle.Text = windows.IsHidden ? "숨긴 창 복원" : "선택 프로그램 숨기기";
        SetStatus(windows.IsHidden ? "일부 창 복원 실패. 대상 프로그램 권한 확인 후 복원 재시도." : "모든 창 복원 완료.");
        if (!windows.IsHidden) RefreshPrograms();
    }

    private void OpenSettings()
    {
        Show(); WindowState = FormWindowState.Normal; Activate();
    }

    private void HideToTray()
    {
        keyboard.Capturing = false;
        capture.Text = "단축키 변경";
        Hide();
    }

    internal void ExitApplication()
    {
        exitRequested = true;
        Close();
    }

    private void SetStatus(string text) => status.Text = text;

    private void OnThreadException(object sender, ThreadExceptionEventArgs args)
    {
        RestoreWindows();
        MessageBox.Show("오류 발생. 창 복원 실행.\n" + args.Exception.Message, "BossKey");
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && !exitRequested)
        {
            e.Cancel = true;
            HideToTray();
            base.OnFormClosing(e);
            return;
        }
        keyboard.Capturing = false;
        windows.RestoreAll();
        if (windows.IsHidden && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            exitRequested = false;
            SetStatus("일부 창 복원 실패. 권한 확인 후 재시도 필요.");
        }
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            windows.RestoreAll();
            refreshTimer.Dispose(); keyboard.Dispose(); tray.Dispose(); trayMenu.Dispose();
            Application.ThreadException -= OnThreadException;
        }
        base.Dispose(disposing);
    }
}
