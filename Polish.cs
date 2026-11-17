// PolisK+ (internal name: Polish) - select text anywhere, press a hotkey, get polished English (or another chosen language) from the Claude Code CLI.
// Compiled at runtime by polish.ps1 via Add-Type (C# 5 / .NET Framework 4.x - keep syntax old-style).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace PolishTool
{
    // ------------------------------------------------------------------ Win32
    internal static class Native
    {
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8, MOD_NOREPEAT = 0x4000;
        public const uint INPUT_KEYBOARD = 1, KEYEVENTF_KEYUP = 2;

        [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr hIcon);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int max);
        [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public InputUnion U; }
        [StructLayout(LayoutKind.Explicit)] public struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
        [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

        static INPUT Key(ushort vk, bool up)
        {
            INPUT i = new INPUT();
            i.type = INPUT_KEYBOARD;
            i.U.ki.wVk = vk;
            i.U.ki.dwFlags = up ? KEYEVENTF_KEYUP : 0;
            return i;
        }

        public static void SendCtrlCombo(char letter)
        {
            ushort vk = (ushort)char.ToUpperInvariant(letter);
            INPUT[] seq = { Key(0x11, false), Key(vk, false), Key(vk, true), Key(0x11, true) };
            SendInput((uint)seq.Length, seq, Marshal.SizeOf(typeof(INPUT)));
        }

        static readonly int[] Modifiers = { 0x10, 0x11, 0x12, 0x5B, 0x5C }; // Shift, Ctrl, Alt, LWin, RWin

        // The hotkey swallows the letter key, so the target app sees "Alt down, Alt up" on its own and
        // activates its menu bar (Notepad, Office...), which then eats our Ctrl+C. Injecting an
        // unassigned virtual key (0xE8) while Alt/Win is still held prevents that (same trick as AutoHotkey).
        public static void MaskAltWin()
        {
            bool alt = (GetAsyncKeyState(0x12) & 0x8000) != 0;
            bool win = (GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0;
            if (!alt && !win) return;
            INPUT[] seq = { Key(0xE8, false), Key(0xE8, true) };
            SendInput((uint)seq.Length, seq, Marshal.SizeOf(typeof(INPUT)));
        }

        // The user is still holding Ctrl+Alt when the hotkey fires; Ctrl+C would become Ctrl+Alt+C.
        public static void WaitForModifierRelease(int timeoutMs)
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (!AnyModifierDown()) return;
                Thread.Sleep(15);
            }
            List<INPUT> ups = new List<INPUT>();
            foreach (int vk in Modifiers)
                if ((GetAsyncKeyState(vk) & 0x8000) != 0) ups.Add(Key((ushort)vk, true));
            if (ups.Count > 0) SendInput((uint)ups.Count, ups.ToArray(), Marshal.SizeOf(typeof(INPUT)));
            Thread.Sleep(30);
        }

        // Some apps (Win11 Notepad and other XAML apps) still enter menu mode when Alt is released,
        // even with the mask key. If the focused element of the foreground app is a menu, press Esc
        // once to leave menu mode so our Ctrl+C reaches the text again.
        public static void LeaveMenuModeIfNeeded(IntPtr foreground)
        {
            Thread.Sleep(80); // menu activation happens asynchronously after Alt-up
            uint fgPid;
            GetWindowThreadProcessId(foreground, out fgPid);
            bool inMenu = false;
            Task t = Task.Run(delegate
            {
                try
                {
                    System.Windows.Automation.AutomationElement e = System.Windows.Automation.AutomationElement.FocusedElement;
                    if (e == null) return;
                    System.Windows.Automation.ControlType ct = e.Current.ControlType;
                    inMenu = e.Current.ProcessId == (int)fgPid &&
                        (ct == System.Windows.Automation.ControlType.MenuItem ||
                         ct == System.Windows.Automation.ControlType.MenuBar ||
                         ct == System.Windows.Automation.ControlType.Menu);
                }
                catch { }
            });
            if (!t.Wait(500) || !inMenu) return;
            INPUT[] esc = { Key(0x1B, false), Key(0x1B, true) };
            SendInput((uint)esc.Length, esc, Marshal.SizeOf(typeof(INPUT)));
            Thread.Sleep(80);
        }

        static bool AnyModifierDown()
        {
            foreach (int vk in Modifiers)
                if ((GetAsyncKeyState(vk) & 0x8000) != 0) return true;
            return false;
        }

        public static void ForceForeground(IntPtr hWnd)
        {
            if (IsIconic(hWnd)) ShowWindow(hWnd, 9); // SW_RESTORE
            IntPtr fg = GetForegroundWindow();
            uint pid;
            uint fgThread = GetWindowThreadProcessId(fg, out pid);
            uint me = GetCurrentThreadId();
            if (fg != IntPtr.Zero && fgThread != me)
            {
                AttachThreadInput(me, fgThread, true);
                SetForegroundWindow(hWnd);
                BringWindowToTop(hWnd);
                AttachThreadInput(me, fgThread, false);
            }
            else
            {
                SetForegroundWindow(hWnd);
                BringWindowToTop(hWnd);
            }
        }
    }

    // ------------------------------------------------------------------ Colours: Water theme
    // Water (navy / ocean blue) + Metal, which feeds Water (white / silver). No orange, yellow or brown.
    internal static class Theme
    {
        public static readonly Color Navy = Color.FromArgb(8, 27, 61);
        public static readonly Color Ocean = Color.FromArgb(22, 101, 192);
        public static readonly Color Accent = Color.FromArgb(18, 82, 160);
        public static readonly Color AccentHover = Color.FromArgb(30, 106, 196);
        public static readonly Color Text = Color.FromArgb(16, 32, 60);
        public static readonly Color Muted = Color.FromArgb(92, 108, 132);
        public static readonly Color Surface = Color.FromArgb(243, 247, 252);
        public static readonly Color Silver = Color.FromArgb(198, 210, 226);
        public static readonly Color SilverLight = Color.FromArgb(226, 234, 245);
        public static readonly Color Success = Color.FromArgb(14, 112, 150);
        public static readonly Color Error = Color.FromArgb(176, 36, 54);

        public static void Primary(Button b)
        {
            b.UseVisualStyleBackColor = false;
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = Accent;
            b.ForeColor = Color.White;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = AccentHover;
            b.FlatAppearance.MouseDownBackColor = Navy;
        }

        public static void Secondary(Button b)
        {
            b.UseVisualStyleBackColor = false;
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = Color.White;
            b.ForeColor = Navy;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Silver;
            b.FlatAppearance.MouseOverBackColor = SilverLight;
            b.FlatAppearance.MouseDownBackColor = Silver;
        }

        // Styles every button under root (the ones listed as primary get the accent colour) and tab pages.
        public static void Apply(Control root, params Button[] primary)
        {
            List<Button> prim = new List<Button>(primary);
            foreach (Control c in root.Controls)
            {
                Button b = c as Button;
                if (b != null) { if (prim.Contains(b)) Primary(b); else Secondary(b); }
                TabPage tp = c as TabPage;
                if (tp != null) { tp.UseVisualStyleBackColor = false; tp.BackColor = Surface; }
                Apply(c, primary);
            }
        }

        // Tray menu colours
        internal class MenuColors : ProfessionalColorTable
        {
            public override Color MenuItemSelected { get { return SilverLight; } }
            public override Color MenuItemBorder { get { return Silver; } }
            public override Color MenuBorder { get { return Silver; } }
            public override Color ToolStripDropDownBackground { get { return Color.White; } }
            public override Color ImageMarginGradientBegin { get { return Surface; } }
            public override Color ImageMarginGradientMiddle { get { return Surface; } }
            public override Color ImageMarginGradientEnd { get { return Surface; } }
            public override Color CheckBackground { get { return SilverLight; } }
            public override Color CheckSelectedBackground { get { return Silver; } }
            public override Color CheckPressedBackground { get { return Silver; } }
            public override Color SeparatorDark { get { return Silver; } }
        }
    }

    // ------------------------------------------------------------------ Logging (errors only, never content)
    internal static class Log
    {
        public static string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Polish");

        public static void Error(string message)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                string file = Path.Combine(Dir, "polish.log");
                FileInfo fi = new FileInfo(file);
                if (fi.Exists && fi.Length > 512 * 1024) fi.Delete();
                File.AppendAllText(file, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }
    }

    // ------------------------------------------------------------------ Config
    internal class Config
    {
        public string Hotkey = "Ctrl+Alt+P";
        public string Model = "sonnet";
        public string Effort = "";
        public int TimeoutSeconds = 60;
        public string ClaudePath = "";
        public List<string> ExtraArgs = new List<string>();
        public int PopupWidth = 540, PopupHeight = 280;
        public float FontSize = 10f;
        public int CopyWaitMs = 1000;
        public int PasteRestoreDelayMs = 700;
        public string DefaultProfile = "Auto";
        public bool AutoProfileByApp = true;
        // Output languages offered in the popup. The first one is the default for every hotkey press.
        public List<string> Languages = new List<string>(new string[] { "English", "Vietnamese" });
        public const int MaxLanguages = 8;
        // Which AI does the writing: "claude" (Claude Code CLI, default), "openai", "gemini" or "custom". "Model" above is Claude's.
        public string Provider = Providers.Claude;
        public string OpenAIModel = Providers.Get(Providers.OpenAI).DefaultModel;
        public string GeminiModel = Providers.Get(Providers.Gemini).DefaultModel;
        public string CustomModel = "";
        public string CustomBaseUrl = "";
        // provider id -> API key encrypted with Windows DPAPI (see Secrets). Never the plain key.
        public Dictionary<string, string> ApiKeys = new Dictionary<string, string>();

        public string DefaultLanguage { get { return Languages.Count > 0 ? Languages[0] : "English"; } }

        public Config Clone()
        {
            Config c = (Config)MemberwiseClone();
            c.ExtraArgs = new List<string>(ExtraArgs);
            c.Languages = new List<string>(Languages);
            c.ApiKeys = new Dictionary<string, string>(ApiKeys);
            return c;
        }

        public string GetModel(string provider)
        {
            if (provider == Providers.OpenAI) return OpenAIModel;
            if (provider == Providers.Gemini) return GeminiModel;
            if (provider == Providers.Custom) return CustomModel;
            return Model;
        }

        public void SetModel(string provider, string model)
        {
            if (provider == Providers.OpenAI) OpenAIModel = model;
            else if (provider == Providers.Gemini) GeminiModel = model;
            else if (provider == Providers.Custom) CustomModel = model;
            else Model = model;
        }

        public string ActiveModel { get { return GetModel(Providers.Get(Provider).Id); } }

        public bool HasSavedKey(string provider) { return ApiKeys.ContainsKey(provider) && Secrets.Unprotect(ApiKeys[provider]).Length > 0; }

        // The saved key, or the provider's usual environment variable (OPENAI_API_KEY, GEMINI_API_KEY) when none is saved.
        public string GetKey(string provider)
        {
            string stored;
            if (ApiKeys.TryGetValue(provider, out stored))
            {
                string k = Secrets.Unprotect(stored);
                if (k.Length > 0) return k;
            }
            foreach (string name in Providers.Get(provider).KeyEnv)
            {
                string v = (Environment.GetEnvironmentVariable(name) ?? "").Trim();
                if (v.Length > 0) return v;
            }
            return "";
        }

        public void SetKey(string provider, string plain)
        {
            if (string.IsNullOrEmpty(plain)) ApiKeys.Remove(provider);
            else ApiKeys[provider] = Secrets.Protect(plain);
        }

        // Letters (any script), spaces, ( ) ' - only, 2..40 chars. Keeps the prompt clean.
        public static bool IsValidLanguage(string s)
        {
            return s != null && Regex.IsMatch(s.Trim(), "^[\\p{L}\\p{M}][\\p{L}\\p{M} ()'\\-]{1,39}$");
        }

        // Trims, drops invalid entries and duplicates, caps the count, never returns an empty list.
        public static List<string> CleanLanguages(IEnumerable<string> items)
        {
            List<string> r = new List<string>();
            if (items != null)
                foreach (string raw in items)
                {
                    string s = Regex.Replace((raw ?? "").Trim(), "\\s+", " ");
                    if (!IsValidLanguage(s) || r.Exists(delegate(string x) { return string.Equals(x, s, StringComparison.OrdinalIgnoreCase); })) continue;
                    r.Add(s);
                    if (r.Count >= MaxLanguages) break;
                }
            if (r.Count == 0) r.Add("English");
            return r;
        }

        public void Save(string file)
        {
            JavaScriptSerializer js = new JavaScriptSerializer();
            List<string> extra = new List<string>();
            foreach (string a in ExtraArgs) extra.Add(js.Serialize(a));
            StringBuilder sb = new StringBuilder();
            sb.Append("{\r\n");
            sb.Append("  \"hotkey\": ").Append(js.Serialize(Hotkey)).Append(",\r\n");
            sb.Append("  \"provider\": ").Append(js.Serialize(Provider)).Append(",\r\n");
            sb.Append("  \"model\": ").Append(js.Serialize(Model)).Append(",\r\n");
            sb.Append("  \"openaiModel\": ").Append(js.Serialize(OpenAIModel)).Append(",\r\n");
            sb.Append("  \"geminiModel\": ").Append(js.Serialize(GeminiModel)).Append(",\r\n");
            sb.Append("  \"customModel\": ").Append(js.Serialize(CustomModel)).Append(",\r\n");
            sb.Append("  \"customBaseUrl\": ").Append(js.Serialize(CustomBaseUrl)).Append(",\r\n");
            List<string> keys = new List<string>();
            foreach (KeyValuePair<string, string> k in ApiKeys) keys.Add(js.Serialize(k.Key) + ": " + js.Serialize(k.Value));
            sb.Append("  \"apiKeys\": {").Append(string.Join(", ", keys.ToArray())).Append("},\r\n");
            sb.Append("  \"effort\": ").Append(js.Serialize(Effort)).Append(",\r\n");
            sb.Append("  \"timeoutSeconds\": ").Append(TimeoutSeconds).Append(",\r\n");
            sb.Append("  \"defaultProfile\": ").Append(js.Serialize(DefaultProfile)).Append(",\r\n");
            sb.Append("  \"autoProfileByApp\": ").Append(AutoProfileByApp ? "true" : "false").Append(",\r\n");
            List<string> langs = new List<string>();
            foreach (string l in Languages) langs.Add(js.Serialize(l));
            sb.Append("  \"languages\": [").Append(string.Join(", ", langs.ToArray())).Append("],\r\n");
            sb.Append("  \"claudePath\": ").Append(js.Serialize(ClaudePath)).Append(",\r\n");
            sb.Append("  \"extraArgs\": [").Append(string.Join(", ", extra.ToArray())).Append("],\r\n");
            sb.Append("  \"popupWidth\": ").Append(PopupWidth).Append(",\r\n");
            sb.Append("  \"popupHeight\": ").Append(PopupHeight).Append(",\r\n");
            sb.Append("  \"fontSize\": ").Append((int)FontSize).Append(",\r\n");
            sb.Append("  \"copyWaitMs\": ").Append(CopyWaitMs).Append(",\r\n");
            sb.Append("  \"pasteRestoreDelayMs\": ").Append(PasteRestoreDelayMs).Append("\r\n");
            sb.Append("}\r\n");
            File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
        }

        public static Config Load(string file)
        {
            Config c = new Config();
            if (!File.Exists(file)) return c;
            Dictionary<string, object> d = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(file, Encoding.UTF8)) as Dictionary<string, object>;
            if (d == null) return c;
            c.Hotkey = Str(d, "hotkey", c.Hotkey);
            c.Model = Str(d, "model", c.Model);
            c.Effort = Str(d, "effort", c.Effort);
            c.TimeoutSeconds = Int(d, "timeoutSeconds", c.TimeoutSeconds);
            c.ClaudePath = Str(d, "claudePath", c.ClaudePath);
            c.PopupWidth = Int(d, "popupWidth", c.PopupWidth);
            c.PopupHeight = Int(d, "popupHeight", c.PopupHeight);
            c.FontSize = Int(d, "fontSize", (int)c.FontSize);
            c.CopyWaitMs = Int(d, "copyWaitMs", c.CopyWaitMs);
            c.PasteRestoreDelayMs = Int(d, "pasteRestoreDelayMs", c.PasteRestoreDelayMs);
            c.DefaultProfile = Str(d, "defaultProfile", c.DefaultProfile);
            c.Provider = Providers.Get(Str(d, "provider", c.Provider)).Id;
            c.OpenAIModel = Str(d, "openaiModel", c.OpenAIModel);
            c.GeminiModel = Str(d, "geminiModel", c.GeminiModel);
            c.CustomModel = Str(d, "customModel", c.CustomModel);
            c.CustomBaseUrl = Str(d, "customBaseUrl", c.CustomBaseUrl);
            object keys;
            if (d.TryGetValue("apiKeys", out keys) && keys is Dictionary<string, object>)
                foreach (KeyValuePair<string, object> k in (Dictionary<string, object>)keys)
                    if (k.Value is string && ((string)k.Value).Length > 0) c.ApiKeys[k.Key] = (string)k.Value;
            object byApp;
            if (d.TryGetValue("autoProfileByApp", out byApp) && byApp is bool) c.AutoProfileByApp = (bool)byApp;
            object extra;
            if (d.TryGetValue("extraArgs", out extra) && extra is IEnumerable && !(extra is string))
                foreach (object o in (IEnumerable)extra) if (o != null) c.ExtraArgs.Add(o.ToString());
            object langs;
            if (d.TryGetValue("languages", out langs) && langs is IEnumerable && !(langs is string))
            {
                List<string> l = new List<string>();
                foreach (object o in (IEnumerable)langs) if (o != null) l.Add(o.ToString());
                c.Languages = CleanLanguages(l);
            }
            return c;
        }

        static string Str(Dictionary<string, object> d, string k, string def)
        {
            object v;
            return d.TryGetValue(k, out v) && v != null ? v.ToString() : def;
        }

        static int Int(Dictionary<string, object> d, string k, int def)
        {
            object v; int r;
            return d.TryGetValue(k, out v) && v != null && int.TryParse(v.ToString(), out r) ? r : def;
        }

        // Update only the "claudePath" value in place so the user's formatting is kept.
        public static void SaveClaudePath(string file, string path)
        {
            try
            {
                string json = File.ReadAllText(file, Encoding.UTF8);
                string val = new JavaScriptSerializer().Serialize(path);
                Regex re = new Regex("\"claudePath\"\\s*:\\s*\"(?:[^\"\\\\]|\\\\.)*\"");
                string updated = re.IsMatch(json)
                    ? re.Replace(json, "\"claudePath\": " + val.Replace("$", "$$"), 1)
                    : json.TrimEnd().TrimEnd('}').TrimEnd().TrimEnd(',') + ",\r\n  \"claudePath\": " + val + "\r\n}\r\n";
                File.WriteAllText(file, updated, new UTF8Encoding(false));
            }
            catch (Exception ex) { Log.Error("Could not save claudePath to config: " + ex.Message); }
        }
    }

    // ------------------------------------------------------------------ Clipboard
    internal class ClipboardSnapshot
    {
        class Entry { public string Format; public object Data; public bool IsStream; }
        readonly List<Entry> entries = new List<Entry>();

        public static ClipboardSnapshot Take()
        {
            ClipboardSnapshot s = new ClipboardSnapshot();
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    s.entries.Clear();
                    IDataObject src = Clipboard.GetDataObject();
                    if (src == null) return s;
                    foreach (string f in src.GetFormats(false))
                    {
                        try
                        {
                            object d = src.GetData(f, false);
                            if (d == null) continue;
                            Entry e = new Entry();
                            e.Format = f;
                            MemoryStream ms = d as MemoryStream;
                            if (ms != null) { e.Data = ms.ToArray(); e.IsStream = true; }
                            else e.Data = d;
                            s.entries.Add(e);
                        }
                        catch { }
                    }
                    return s;
                }
                catch (ExternalException) { Thread.Sleep(50); }
            }
            return s;
        }

        public void Restore()
        {
            try
            {
                if (entries.Count == 0) { ClipboardUtil.Retry(delegate { Clipboard.Clear(); }); return; }
                DataObject d = new DataObject();
                foreach (Entry e in entries)
                {
                    try { d.SetData(e.Format, false, e.IsStream ? new MemoryStream((byte[])e.Data) : e.Data); }
                    catch { }
                }
                ClipboardUtil.MarkPrivate(d);
                Clipboard.SetDataObject(d, true, 10, 50);
            }
            catch (Exception ex) { Log.Error("Clipboard restore failed: " + ex.GetType().Name); }
        }
    }

    internal static class ClipboardUtil
    {
        public static void Retry(Action a)
        {
            for (int i = 0; ; i++)
            {
                try { a(); return; }
                catch (ExternalException) { if (i >= 9) throw; Thread.Sleep(50); }
            }
        }

        // Keep temporary clipboard content out of Win+V history and cloud clipboard.
        public static void MarkPrivate(DataObject d)
        {
            d.SetData("ExcludeClipboardContentFromMonitorProcessing", false, new MemoryStream(new byte[] { 0, 0, 0, 0 }));
            d.SetData("CanIncludeInClipboardHistory", false, new MemoryStream(BitConverter.GetBytes(0)));
            d.SetData("CanUploadToCloudClipboard", false, new MemoryStream(BitConverter.GetBytes(0)));
        }

        public static void SetText(string text, bool isPrivate)
        {
            DataObject d = new DataObject();
            d.SetData(DataFormats.UnicodeText, false, text);
            if (isPrivate) MarkPrivate(d);
            Clipboard.SetDataObject(d, true, 10, 50);
        }

        // Sends Ctrl+C to the foreground app and returns the copied text, or null if nothing was copied.
        public static string CaptureSelection(int waitMs)
        {
            uint before = Native.GetClipboardSequenceNumber();
            Native.SendCtrlCombo('C');
            Stopwatch sw = Stopwatch.StartNew();
            bool changed = false;
            while (sw.ElapsedMilliseconds < waitMs)
            {
                Thread.Sleep(20);
                if (Native.GetClipboardSequenceNumber() != before) { changed = true; break; }
            }
            if (!changed) return null;
            Thread.Sleep(60); // let the source app finish adding all formats
            string text = null;
            for (int i = 0; i < 10 && text == null; i++)
            {
                try { text = Clipboard.ContainsText(TextDataFormat.UnicodeText) ? Clipboard.GetText(TextDataFormat.UnicodeText) : ""; }
                catch (ExternalException) { Thread.Sleep(50); }
            }
            return text;
        }
    }

    // ------------------------------------------------------------------ Claude CLI runner
    internal class ClaudeJob
    {
        public string Output = "", Error = "";
        public int ExitCode = -1;
        public bool TimedOut, Cancelled, Done;
        public long ElapsedMs;
        public bool IsHttp;          // API provider (OpenAI, Gemini, custom) instead of the Claude CLI
        public string Provider = "Claude";
        Process proc;
        System.Net.HttpWebRequest request;

        // API providers: one HTTPS request. ExitCode is 0 on success, the HTTP status on an API error, -1 on a network error.
        public void StartHttp(HttpSpec spec, int timeoutMs, Action<ClaudeJob> onDone)
        {
            IsHttp = true;
            Provider = spec.Provider.Short;
            ThreadPool.QueueUserWorkItem(delegate
            {
                Stopwatch sw = Stopwatch.StartNew();
                try
                {
                    if (!Cancelled)
                    {
                        string body;
                        int status = Http.Send(spec, timeoutMs, delegate(System.Net.HttpWebRequest r) { request = r; if (Cancelled) r.Abort(); }, out body);
                        if (status >= 200 && status < 300) { Output = Api.ParseText(spec, body); ExitCode = 0; }
                        else { Error = Api.ErrorMessage(spec, status, body); ExitCode = status; }
                    }
                }
                catch (System.Net.WebException ex)
                {
                    if (ex.Status == System.Net.WebExceptionStatus.Timeout) TimedOut = true;
                    else if (!Cancelled) { Error = Api.NetworkMessage(spec, ex); ExitCode = -1; }
                }
                catch (Exception ex) { Error = ex.Message; ExitCode = -1; }
                ElapsedMs = sw.ElapsedMilliseconds;
                Done = true;
                onDone(this);
            });
        }

        public void Start(ProcessStartInfo psi, string input, int timeoutMs, Action<ClaudeJob> onDone)
        {
            byte[] stdin = new UTF8Encoding(false).GetBytes(input);
            ThreadPool.QueueUserWorkItem(delegate
            {
                Stopwatch sw = Stopwatch.StartNew();
                try
                {
                    proc = new Process();
                    proc.StartInfo = psi;
                    if (!Cancelled)
                    {
                        proc.Start();
                        Task<string> outT = proc.StandardOutput.ReadToEndAsync();
                        Task<string> errT = proc.StandardError.ReadToEndAsync();
                        try
                        {
                            Stream s = proc.StandardInput.BaseStream;
                            s.Write(stdin, 0, stdin.Length);
                            s.Flush();
                            proc.StandardInput.Close();
                        }
                        catch (IOException) { }
                        if (!proc.WaitForExit(timeoutMs)) { TimedOut = true; KillTree(); }
                        proc.WaitForExit(3000);
                        Output = outT.Wait(3000) ? outT.Result : "";
                        Error = errT.Wait(1000) ? errT.Result : "";
                        ExitCode = proc.HasExited ? proc.ExitCode : -1;
                    }
                }
                catch (Exception ex) { Error = ex.Message; ExitCode = -1; }
                ElapsedMs = sw.ElapsedMilliseconds;
                Done = true;
                onDone(this);
            });
        }

        public void Cancel()
        {
            Cancelled = true;
            KillTree();
            try { if (request != null) request.Abort(); } catch { }
        }

        void KillTree()
        {
            try
            {
                if (proc == null || proc.HasExited) return;
                ProcessStartInfo k = new ProcessStartInfo("taskkill.exe", "/T /F /PID " + proc.Id);
                k.CreateNoWindow = true;
                k.UseShellExecute = false;
                Process.Start(k).WaitForExit(3000);
                if (!proc.HasExited) proc.Kill();
            }
            catch { }
        }
    }

    // ------------------------------------------------------------------ Popup window
    internal class Popup : Form
    {
        readonly App app;
        readonly IntPtr sourceHwnd;
        readonly TextBox txt;
        readonly Label status;
        readonly Button bCopy, bReplace, bClose, bCancel, bRetry;
        readonly System.Windows.Forms.Timer tick;
        readonly Stopwatch sw = new Stopwatch();
        readonly ComboBox styleBox;
        readonly Label reasonLabel;
        readonly List<Profile> profiles;
        readonly List<string> languages;
        readonly ComboBox langBox;
        string input;
        ClaudeJob job;
        bool replacing;
        bool settingStyle;

        public bool Busy { get { return job != null && !job.Done; } }

        string CurrentLanguage
        {
            get { int i = langBox != null ? langBox.SelectedIndex : 0; return i >= 0 && i < languages.Count ? languages[i] : "English"; }
        }

        Profile CurrentProfile
        {
            get { int i = styleBox.SelectedIndex; return i >= 0 && i < profiles.Count ? profiles[i] : null; }
        }

        public Popup(App app, IntPtr sourceHwnd, Point near, Config cfg, List<Profile> profiles, Profile chosen, string reason)
        {
            this.app = app;
            this.sourceHwnd = sourceHwnd;
            this.profiles = profiles;
            Text = App.AppName;
            if (app.AppIcon != null) Icon = app.AppIcon;
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            Font = new Font("Segoe UI", 9f);
            Size = new Size(Math.Max(420, cfg.PopupWidth), Math.Max(220, cfg.PopupHeight + 32));
            MinimumSize = new Size(420, 220);

            Rectangle wa = Screen.FromPoint(near).WorkingArea;
            int x = near.X + 12, y = near.Y + 18;
            if (x + Width > wa.Right) x = Math.Max(wa.Left, near.X - Width - 12);
            if (y + Height > wa.Bottom) y = Math.Max(wa.Top, near.Y - Height - 12);
            Location = new Point(Math.Max(wa.Left, x), Math.Max(wa.Top, y));

            txt = new TextBox();
            txt.Multiline = true;
            txt.WordWrap = true;
            txt.ScrollBars = ScrollBars.Vertical;
            txt.AcceptsReturn = true;
            txt.Dock = DockStyle.Fill;
            txt.Font = new Font("Segoe UI", cfg.FontSize);
            txt.BorderStyle = BorderStyle.FixedSingle;
            txt.BackColor = Color.White;
            BackColor = Theme.Surface;
            ForeColor = Theme.Text;

            status = new Label();
            status.Dock = DockStyle.Top;
            status.Height = 26;
            status.TextAlign = ContentAlignment.MiddleLeft;
            status.Padding = new Padding(6, 0, 6, 0);

            FlowLayoutPanel bar = new FlowLayoutPanel();
            bar.Dock = DockStyle.Bottom;
            bar.FlowDirection = FlowDirection.RightToLeft;
            bar.Height = 38;
            bar.Padding = new Padding(4);

            bClose = MakeButton("Close");
            bReplace = MakeButton("Replace");
            bCopy = MakeButton("Copy");
            bCancel = MakeButton("Cancel");
            bRetry = MakeButton("Retry");
            bar.Controls.AddRange(new Control[] { bClose, bReplace, bCopy, bRetry, bCancel });

            Panel pad = new Panel();
            pad.Dock = DockStyle.Fill;
            pad.Padding = new Padding(6, 0, 6, 0);
            pad.Controls.Add(txt);

            // Style picker: "1. Auto", "2. Chat"... (Ctrl+1..9 switches and re-polishes)
            FlowLayoutPanel top = new FlowLayoutPanel();
            top.Dock = DockStyle.Top;
            top.Height = 32;
            top.Padding = new Padding(4, 4, 4, 0);
            top.WrapContents = false;
            Label lbl = new Label();
            lbl.Text = "Style:";
            lbl.AutoSize = true;
            lbl.Margin = new Padding(2, 6, 2, 0);
            styleBox = new ComboBox();
            styleBox.DropDownStyle = ComboBoxStyle.DropDownList;
            styleBox.Width = 170;
            for (int i = 0; i < profiles.Count; i++)
                styleBox.Items.Add((i < 9 ? (i + 1) + ". " : "") + profiles[i].Name);
            reasonLabel = new Label();
            reasonLabel.AutoSize = true;
            reasonLabel.ForeColor = Theme.Muted;
            reasonLabel.Margin = new Padding(6, 6, 2, 0);
            top.Controls.AddRange(new Control[] { lbl, styleBox });

            // Language picker: only the languages from Settings (max 8). Every hotkey press starts in the first one.
            languages = cfg.Languages.Count > 0 ? new List<string>(cfg.Languages) : new List<string>(new string[] { "English" });
            if (languages.Count > 1)
            {
                Label ll = new Label();
                ll.Text = "Language:";
                ll.AutoSize = true;
                ll.Margin = new Padding(10, 6, 2, 0);
                langBox = new ComboBox();
                langBox.DropDownStyle = ComboBoxStyle.DropDownList;
                langBox.Width = 130;
                langBox.MaxDropDownItems = Config.MaxLanguages;
                foreach (string l in languages) langBox.Items.Add(l);
                langBox.SelectedIndex = 0;
                langBox.SelectedIndexChanged += delegate
                {
                    if (!string.IsNullOrEmpty(input)) Start(input);
                    if (IsHandleCreated) BeginInvoke(new Action(delegate { if (!IsDisposed) txt.Focus(); }));
                };
                top.Controls.AddRange(new Control[] { ll, langBox });
            }
            top.Controls.Add(reasonLabel);
            settingStyle = true;
            int idx = chosen != null ? profiles.IndexOf(chosen) : -1;
            styleBox.SelectedIndex = idx >= 0 ? idx : (profiles.Count > 0 ? 0 : -1);
            settingStyle = false;
            reasonLabel.Text = DescribeReason(reason);
            ToolTip tips = new ToolTip();
            Action setTip = delegate { Profile cp = CurrentProfile; tips.SetToolTip(styleBox, cp != null ? cp.Description : ""); };
            setTip();
            styleBox.SelectedIndexChanged += delegate
            {
                if (settingStyle) return;
                setTip();
                reasonLabel.Text = "";
                if (!string.IsNullOrEmpty(input)) Start(input);
                // Give focus back to the text so a stray key press cannot change the style again.
                if (IsHandleCreated) BeginInvoke(new Action(delegate { if (!IsDisposed) txt.Focus(); }));
            };

            Controls.Add(pad);     // fill first, then edges
            Controls.Add(status);
            Controls.Add(top);
            Controls.Add(bar);

            bCopy.Click += delegate { DoCopy(); };
            bReplace.Click += delegate { DoReplace(); };
            bClose.Click += delegate { Close(); };
            bCancel.Click += delegate { CancelJob(); Close(); };
            bRetry.Click += delegate { Start(input); };
            Theme.Apply(this, bCopy, bRetry);

            tick = new System.Windows.Forms.Timer();
            tick.Interval = 250;
            tick.Tick += delegate { if (Busy) status.Text = "Polishing...  " + (sw.ElapsedMilliseconds / 1000.0).ToString("0.0") + "s   (Esc to cancel)"; };

            KeyDown += OnKey;
            FormClosing += delegate
            {
                CancelJob();
                tick.Stop();
                // Hand focus back to the app the text came from (otherwise Windows picks any window).
                if (!replacing && Visible && Native.IsWindow(sourceHwnd)) Native.ForceForeground(sourceHwnd);
            };
        }

        Button MakeButton(string label)
        {
            Button b = new Button();
            b.Text = label;
            b.AutoSize = true;
            b.MinimumSize = new Size(76, 28);
            b.Margin = new Padding(4, 2, 0, 2);
            return b;
        }

        string DescribeReason(string reason)
        {
            Profile p = CurrentProfile;
            string desc = p != null && p.Description.Length > 0 ? p.Description : "";
            if (langBox != null) desc = "";   // no room next to the Language box; the description is in the tooltip
            if (desc.Length > 48) desc = desc.Substring(0, 46) + "...";
            return (reason != null && reason.StartsWith("auto:") ? "(" + reason + ")  " : "") + desc;
        }

        void OnKey(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { e.Handled = e.SuppressKeyPress = true; CancelJob(); Close(); }
            else if (e.KeyCode == Keys.Enter && e.Control) { e.Handled = e.SuppressKeyPress = true; if (bCopy.Visible) DoCopy(); }
            else if (e.Control && !e.Alt && e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D9)
            {
                int i = e.KeyCode - Keys.D1;
                e.Handled = e.SuppressKeyPress = true;
                if (i < profiles.Count && i != styleBox.SelectedIndex) styleBox.SelectedIndex = i; // triggers re-polish
            }
            else if (e.Alt && !e.Control && langBox != null && e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D9)
            {
                int i = e.KeyCode - Keys.D1;
                e.Handled = e.SuppressKeyPress = true;
                if (i < languages.Count && i != langBox.SelectedIndex) langBox.SelectedIndex = i; // triggers re-polish
            }
        }

        void SetMode(string mode)
        {
            bool result = mode == "result", busy = mode == "busy", error = mode == "error";
            bCopy.Visible = bReplace.Visible = result;
            bCancel.Visible = busy;
            bRetry.Visible = error && !string.IsNullOrEmpty(input);
            bClose.Visible = !busy;
            txt.ReadOnly = !result;
            txt.ForeColor = result ? Theme.Text : Theme.Muted;
            status.ForeColor = error ? Theme.Error : Theme.Navy;
        }

        public void ShowAndFocus()
        {
            Show();
            Native.ForceForeground(Handle);
            Activate();
            txt.Focus();
        }

        public void ShowError(string message)
        {
            SetMode("error");
            status.Text = "Error";
            txt.Text = message;
            txt.Select(0, 0);
            if (bRetry.Visible) bRetry.Focus(); else bClose.Focus();
        }

        public void Start(string text)
        {
            CancelJob();
            input = text;
            SetMode("busy");
            txt.Text = "";
            status.Text = "Polishing...";
            sw.Restart();
            tick.Start();
            bCancel.Focus();
            string err = null;
            Profile p = CurrentProfile;
            string prompt = p == null ? null : app.ComposePrompt(p, CurrentLanguage, out err);
            if (p == null) err = "No styles found. Open " + App.AppName + " Settings > Styles > Restore default styles.";
            if (prompt == null) { tick.Stop(); ShowError(err); return; }
            ClaudeJob j = new ClaudeJob();
            job = j;
            bool started = app.RunJob(j, prompt, text, app.Cfg, delegate(ClaudeJob done)
            {
                try { if (!IsDisposed) BeginInvoke(new Action(delegate { OnDone(done); })); }
                catch (InvalidOperationException) { }
            }, out err);
            if (!started) { job = null; tick.Stop(); ShowError(err); }
        }

        void OnDone(ClaudeJob j)
        {
            if (j != job || IsDisposed) return;
            tick.Stop();
            if (j.Cancelled) return;
            string secs = (j.ElapsedMs / 1000.0).ToString("0.0") + "s";
            string output = App.CleanOutput(j.Output, input);
            string failure = App.FailureMessage(j, app.Cfg, output);
            if (failure != null) { ShowError(failure); return; }
            SetMode("result");
            status.Text = "Done in " + secs + "  -  Ctrl+Enter = Copy, Esc = Close, Ctrl+1..9 = style" + (langBox != null ? ", Alt+1.." + languages.Count + " = language" : "");
            txt.Text = output;
            txt.SelectAll();
            txt.Focus();
        }

        void CancelJob()
        {
            if (Busy) job.Cancel();
        }

        void DoCopy()
        {
            try { ClipboardUtil.SetText(App.KeepListIndent(txt.Text), false); Close(); }
            catch (Exception ex) { Log.Error("Copy failed: " + ex.GetType().Name); ShowError("Could not write to the clipboard. Try again."); }
        }

        void DoReplace()
        {
            string text = App.KeepListIndent(txt.Text.TrimEnd('\r', '\n'));
            // The selection ended with a line break (whole paragraph selected): keep it, or the last line
            // would join the paragraph that follows.
            if (input != null && input.EndsWith("\n")) text += "\r\n";
            if (!Native.IsWindow(sourceHwnd)) { ShowError("The original window is gone. Use Copy instead."); return; }
            replacing = true;
            Hide();
            app.PasteInto(sourceHwnd, text);
            Close();
        }
    }

    // ------------------------------------------------------------------ Hotkey sink
    internal class HotkeyWindow : NativeWindow, IDisposable
    {
        public event Action Pressed;
        public HotkeyWindow()
        {
            CreateParams cp = new CreateParams();
            cp.Parent = new IntPtr(-3); // HWND_MESSAGE
            CreateHandle(cp);
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && Pressed != null) Pressed();
            base.WndProc(ref m);
        }
        public void Dispose() { DestroyHandle(); }
    }

    // ------------------------------------------------------------------ App
    public class App : ApplicationContext
    {
        public const string Version = "1.3.3";
        // Display name. Internal ids (folders, mutex, event, namespace) keep "Polish" so upgrades keep working.
        public const string AppName = "PolisK+";
        public const string SettingsEventName = "Local\\PolishTool.ShowSettings";
        const int HotkeyId = 0x5150;
        internal readonly string appDir, configFile, promptDir, profilesDir, defaultsDir, workDir;
        internal Config Cfg;
        internal Icon AppIcon;
        readonly HotkeyWindow hk;
        readonly NotifyIcon tray;
        readonly Control invoker;
        ToolStripMenuItem styleMenu;
        Popup popup;
        SettingsForm settings;
        bool capturing;
        bool hotkeyOk;
        bool hotkeySuspended;

        [DllImport("shell32.dll")] static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string id);

        public static void Run(string appDir, bool showSettings)
        {
            bool created;
            using (Mutex m = new Mutex(true, "Local\\PolishTool.SingleInstance", out created))
            {
                if (!created)
                {
                    // Already running: ask that instance to show its Settings window.
                    try { EventWaitHandle ev = EventWaitHandle.OpenExisting(SettingsEventName); if (showSettings) ev.Set(); } catch { }
                    return;
                }
                try { SetCurrentProcessExplicitAppUserModelID("Polish.TextPolisher"); } catch { }
                Application.EnableVisualStyles();
                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Log.Error("UI exception: " + e.Exception.GetType().Name + ": " + e.Exception.Message); };
                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Log.Error("Fatal: " + e.ExceptionObject); };
                App app = new App(appDir);
                if (showSettings) app.invoker.BeginInvoke(new Action(app.ShowSettings));
                Application.Run(app);
                GC.KeepAlive(m);
            }
        }

        App(string appDir)
        {
            this.appDir = appDir;
            configFile = Path.Combine(appDir, "config.json");
            promptDir = Path.Combine(appDir, "prompt");
            profilesDir = Path.Combine(appDir, "profiles");
            defaultsDir = Path.Combine(appDir, "defaults");
            workDir = Path.Combine(Log.Dir, "work");
            Directory.CreateDirectory(workDir);

            invoker = new Control();
            invoker.CreateControl();
            IntPtr force = invoker.Handle; // create the handle on the UI thread

            string ico = Path.Combine(appDir, "polish.ico");
            try { if (File.Exists(ico)) AppIcon = new Icon(ico); } catch (Exception ex) { Log.Error("Icon load failed: " + ex.Message); }

            hk = new HotkeyWindow();
            hk.Pressed += OnHotkey;

            tray = new NotifyIcon();
            try { tray.Icon = AppIcon != null ? new Icon(AppIcon, SystemInformation.SmallIconSize) : MakeIcon(); }
            catch { tray.Icon = MakeIcon(); }
            tray.Visible = true;
            tray.DoubleClick += delegate { ShowSettings(); };
            BuildMenu();

            Reload(false);
            StartSettingsListener();
        }

        // A second launch (Start Menu / Desktop icon) signals this event to open Settings here.
        void StartSettingsListener()
        {
            EventWaitHandle ev = new EventWaitHandle(false, EventResetMode.AutoReset, SettingsEventName);
            Thread t = new Thread(delegate()
            {
                while (true)
                {
                    ev.WaitOne();
                    try { invoker.BeginInvoke(new Action(ShowSettings)); } catch { return; }
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        void BuildMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem title = new ToolStripMenuItem(AppName + " " + Version);
            title.Enabled = false;
            menu.Items.Add(title);
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem open = new ToolStripMenuItem("Settings...", null, delegate { ShowSettings(); });
            open.Font = new Font(open.Font, FontStyle.Bold);
            menu.Items.Add(open);
            styleMenu = new ToolStripMenuItem("Default style");
            menu.Items.Add(styleMenu);
            menu.Opening += delegate { FillStyleMenu(); };
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Open settings folder", null, delegate { OpenFile(appDir); });
            menu.Items.Add("Open error log folder", null, delegate { OpenFile(Log.Dir); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Quit", null, delegate { Quit(); });
            menu.Renderer = new ToolStripProfessionalRenderer(new Theme.MenuColors());
            menu.ForeColor = Theme.Text;
            tray.ContextMenuStrip = menu;
        }

        void FillStyleMenu()
        {
            styleMenu.DropDownItems.Clear();
            foreach (Profile p in ProfileStore.Load(profilesDir))
            {
                string name = p.Name;
                ToolStripMenuItem it = new ToolStripMenuItem(name, null, delegate
                {
                    Cfg.DefaultProfile = name;
                    try { Cfg.Save(configFile); } catch (Exception ex) { Log.Error("Save config failed: " + ex.Message); }
                });
                it.Checked = string.Equals(name, Cfg.DefaultProfile, StringComparison.OrdinalIgnoreCase);
                it.ToolTipText = p.Description;
                styleMenu.DropDownItems.Add(it);
            }
            styleMenu.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem byApp = new ToolStripMenuItem("Pick style automatically by app", null, delegate
            {
                Cfg.AutoProfileByApp = !Cfg.AutoProfileByApp;
                try { Cfg.Save(configFile); } catch (Exception ex) { Log.Error("Save config failed: " + ex.Message); }
            });
            byApp.Checked = Cfg.AutoProfileByApp;
            styleMenu.DropDownItems.Add(byApp);
        }

        internal static void OpenFile(string path)
        {
            try
            {
                if (path.EndsWith(".json") || path.EndsWith(".md")) Process.Start("notepad.exe", "\"" + path + "\"");
                else Process.Start("explorer.exe", "\"" + path + "\"");
            }
            catch (Exception ex) { Log.Error("Open failed: " + ex.Message); }
        }

        internal void ShowSettings()
        {
            if (settings != null && !settings.IsDisposed)
            {
                if (settings.WindowState == FormWindowState.Minimized) settings.WindowState = FormWindowState.Normal;
                Native.ForceForeground(settings.Handle);
                return;
            }
            settings = new SettingsForm(this);
            settings.FormClosed += delegate { settings = null; };
            settings.Show();
            Native.ForceForeground(settings.Handle);
        }

        // Called by the Settings window after saving.
        internal void ApplySettings(Config c)
        {
            c.Save(configFile);
            Reload(false);
        }

        // Hotkey capture box in Settings must receive the keys itself.
        internal void SuspendHotkey() { if (!hotkeySuspended) { Native.UnregisterHotKey(hk.Handle, HotkeyId); hotkeySuspended = true; } }
        internal void ResumeHotkey() { if (hotkeySuspended) { hotkeySuspended = false; RegisterHotkey(false); } }

        internal string RulesFile { get { return Path.Combine(promptDir, "rules.md"); } }

        // System prompt = core rules + the chosen style. Files are re-read on every request.
        internal string ComposePrompt(Profile p, out string err)
        {
            return ComposePrompt(p, "English", out err);
        }

        // System prompt = core rules + the chosen style + (for languages other than English) an output-language section.
        internal string ComposePrompt(Profile p, string language, out string err)
        {
            string prompt = ComposeBase(p, out err);
            if (prompt == null) return null;
            string lang = Config.IsValidLanguage(language) ? language.Trim() : "English";
            if (string.Equals(lang, "English", StringComparison.OrdinalIgnoreCase)) return prompt;
            return prompt + "\n\n" + LanguageSection(lang);
        }

        internal static string LanguageSection(string lang)
        {
            return "# Output language\n\n" +
                "Write the output in " + lang + ", whatever language the input is in. This overrides every mention of English above: " +
                "wherever the rules or style say English, read " + lang + ".\n" +
                "- Write natural, professional " + lang + " the way a competent native-speaking colleague would. Avoid that language's equivalents of the AI writing patterns listed above.\n" +
                "- Hard rules 1 to 4 still apply. Keep names, product/feature names, technical terms, numbers, links, IDs and abbreviations exactly as written; do not translate them.\n" +
                "- Write dates in the usual unambiguous form for " + lang + ".\n" +
                "- If the input is already in " + lang + ", polish it in " + lang + "; do not translate it into another language.";
        }

        string ComposeBase(Profile p, out string err)
        {
            err = null;
            string rules = "";
            if (File.Exists(RulesFile))
            {
                try { rules = Regex.Replace(File.ReadAllText(RulesFile, Encoding.UTF8), "<!--[\\s\\S]*?-->", "").Trim(); }
                catch (Exception ex) { err = "Cannot read prompt\\rules.md: " + ex.Message; return null; }
            }
            if (rules.Length == 0) { err = "prompt\\rules.md is missing or empty.\r\n\r\nOpen " + AppName + " Settings > Advanced > Restore default core rules."; return null; }
            string style = p != null ? p.PromptText() : "";
            return style.Length > 0 ? rules + "\n\n" + style : rules;
        }

        void Quit()
        {
            Native.UnregisterHotKey(hk.Handle, HotkeyId);
            if (popup != null && !popup.IsDisposed) popup.Close();
            if (settings != null && !settings.IsDisposed) settings.Close();
            tray.Visible = false;
            tray.Dispose();
            hk.Dispose();
            ExitThread();
        }

        void Reload(bool announce)
        {
            try { Cfg = Config.Load(configFile); }
            catch (Exception ex)
            {
                Log.Error("config.json is invalid: " + ex.Message);
                Cfg = new Config();
                tray.ShowBalloonTip(5000, AppName, "config.json is invalid - using defaults.\n" + ex.Message, ToolTipIcon.Warning);
            }
            string err = null;
            ProviderInfo p = Providers.Get(Cfg.Provider);
            if (!p.IsApi) ResolveClaudePath(out err);   // API providers do not need the Claude CLI
            RegisterHotkey(true);
            string tip = AppName + " - " + Cfg.Hotkey + " (" + p.Short + ": " + Cfg.ActiveModel + ")";
            tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
            if (announce && hotkeyOk)
                tray.ShowBalloonTip(2500, AppName, "Reloaded. Hotkey " + Cfg.Hotkey + ", " + p.Short + " " + Cfg.ActiveModel + "." + (err != null ? "\n" + err : ""), ToolTipIcon.Info);
        }

        // Returns null when OK, otherwise a message.
        internal string RegisterHotkey(bool showBalloon)
        {
            Native.UnregisterHotKey(hk.Handle, HotkeyId);
            hotkeyOk = false;
            uint mods, vk;
            string msg = null;
            if (!ParseHotkey(Cfg.Hotkey, out mods, out vk)) msg = "Cannot understand hotkey \"" + Cfg.Hotkey + "\".";
            else if (!Native.RegisterHotKey(hk.Handle, HotkeyId, mods | Native.MOD_NOREPEAT, vk))
            {
                Log.Error("RegisterHotKey failed for " + Cfg.Hotkey + " (win32 error " + Marshal.GetLastWin32Error() + ")");
                msg = Cfg.Hotkey + " is already used by another app. Pick another hotkey in " + AppName + " Settings.";
            }
            else hotkeyOk = true;
            if (msg != null && showBalloon) tray.ShowBalloonTip(5000, AppName, msg, ToolTipIcon.Error);
            return msg;
        }

        // Test whether a hotkey can be registered (used by Settings before saving).
        internal bool CanRegister(string hotkey)
        {
            uint mods, vk;
            if (!ParseHotkey(hotkey, out mods, out vk)) return false;
            if (string.Equals(hotkey, Cfg.Hotkey, StringComparison.OrdinalIgnoreCase)) return true;
            const int probe = 0x5151;
            bool ok = Native.RegisterHotKey(hk.Handle, probe, mods | Native.MOD_NOREPEAT, vk);
            if (ok) Native.UnregisterHotKey(hk.Handle, probe);
            return ok;
        }

        // ---- Startup shortcut (no admin): %APPDATA%\...\Startup\PolisK+.lnk (Polish.lnk before 1.1.2)
        internal static string StartupLink { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), AppName + ".lnk"); } }
        static string OldStartupLink { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Polish.lnk"); } }
        internal bool StartWithWindows { get { return File.Exists(StartupLink) || File.Exists(OldStartupLink); } }

        internal void SetStartWithWindows(bool on)
        {
            if (File.Exists(OldStartupLink)) File.Delete(OldStartupLink);
            if (!on) { if (File.Exists(StartupLink)) File.Delete(StartupLink); return; }
            CreateShortcut(StartupLink, false);
        }

        internal void CreateShortcut(string path, bool openSettings)
        {
            string ps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell\\v1.0\\powershell.exe");
            string args = "-NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File \"" + Path.Combine(appDir, "polish.ps1") + "\"" + (openSettings ? " -Settings" : "");
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            object sh = Activator.CreateInstance(t);
            object lnk = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, sh, new object[] { path });
            Type lt = lnk.GetType();
            lt.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, lnk, new object[] { ps });
            lt.InvokeMember("Arguments", System.Reflection.BindingFlags.SetProperty, null, lnk, new object[] { args });
            lt.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, lnk, new object[] { appDir });
            lt.InvokeMember("WindowStyle", System.Reflection.BindingFlags.SetProperty, null, lnk, new object[] { 7 });
            lt.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, lnk, new object[] { AppName + " - select text, press " + Cfg.Hotkey });
            string ico = Path.Combine(appDir, "polish.ico");
            if (File.Exists(ico)) lt.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, lnk, new object[] { ico + ",0" });
            lt.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, lnk, null);
        }

        // ---- Restore defaults (shipped copies live in defaults\)
        internal int RestoreDefaultStyles()
        {
            string src = Path.Combine(defaultsDir, "profiles");
            if (!Directory.Exists(src)) throw new DirectoryNotFoundException("Default styles not found in " + src + ". Run Setup again.");
            Directory.CreateDirectory(profilesDir);
            int n = 0;
            foreach (string f in Directory.GetFiles(src, "*.md"))
            {
                string dst = Path.Combine(profilesDir, Path.GetFileName(f));
                if (File.Exists(dst)) File.Copy(dst, dst + ".bak", true);
                File.Copy(f, dst, true);
                n++;
            }
            return n;
        }

        internal void RestoreDefaultRules()
        {
            string src = Path.Combine(defaultsDir, "prompt\\rules.md");
            if (!File.Exists(src)) throw new FileNotFoundException("Default rules not found: " + src + ". Run Setup again.");
            Directory.CreateDirectory(promptDir);
            if (File.Exists(RulesFile)) File.Copy(RulesFile, RulesFile + ".bak", true);
            File.Copy(src, RulesFile, true);
        }

        internal static bool ParseHotkey(string s, out uint mods, out uint vk)
        {
            mods = 0; vk = 0;
            if (string.IsNullOrEmpty(s)) return false;
            foreach (string raw in s.Split('+'))
            {
                string p = raw.Trim();
                string l = p.ToLowerInvariant();
                if (l == "ctrl" || l == "control") mods |= Native.MOD_CONTROL;
                else if (l == "alt") mods |= Native.MOD_ALT;
                else if (l == "shift") mods |= Native.MOD_SHIFT;
                else if (l == "win" || l == "windows") mods |= Native.MOD_WIN;
                else
                {
                    if (p.Length == 1 && char.IsDigit(p[0])) p = "D" + p;
                    Keys k;
                    if (!Enum.TryParse(p, true, out k)) return false;
                    vk = (uint)k;
                }
            }
            return vk != 0;
        }

        // --------------------------------------------------- claude.exe discovery
        bool ResolveClaudePath(out string err)
        {
            err = null;
            if (!string.IsNullOrEmpty(Cfg.ClaudePath) && File.Exists(Cfg.ClaudePath)) return true;
            string found = FindClaude();
            if (found == null)
            {
                err = "Claude Code (claude.exe) was not found on this computer.\r\n\r\nInstall Claude Code and run Setup again, or pick another AI (OpenAI, Gemini...) in " + AppName + " Settings > AI provider.";
                return false;
            }
            Cfg.ClaudePath = found;
            Config.SaveClaudePath(configFile, found);
            return true;
        }

        static string FindOnPath(string file)
        {
            string pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string dir in pathVar.Split(';'))
            {
                try
                {
                    if (dir.Trim().Length == 0) continue;
                    string c = Path.Combine(Environment.ExpandEnvironmentVariables(dir.Trim()), file);
                    if (File.Exists(c)) return c;
                }
                catch { }
            }
            return null;
        }

        static string FindClaude()
        {
            string onPath = FindOnPath("claude.exe");
            if (onPath != null) return onPath;
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string local = Path.Combine(home, ".local\\bin\\claude.exe");
            if (File.Exists(local)) return local;
            // CLI bundled with the Claude desktop app: %APPDATA%\Claude\claude-code\<version>\<build>\claude.exe
            string bundled = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude\\claude-code");
            if (Directory.Exists(bundled))
            {
                FileInfo best = null;
                foreach (string f in Directory.GetFiles(bundled, "claude.exe", SearchOption.AllDirectories))
                {
                    FileInfo fi = new FileInfo(f);
                    if (best == null || fi.LastWriteTimeUtc > best.LastWriteTimeUtc) best = fi;
                }
                if (best != null) return best.FullName;
            }
            // npm install: %APPDATA%\npm\node_modules\@anthropic-ai\claude-code\cli.js (run with node)
            string npmJs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm\\node_modules\\@anthropic-ai\\claude-code\\cli.js");
            if (File.Exists(npmJs) && FindOnPath("node.exe") != null) return npmJs;
            return null;
        }

        // Sends the text to the chosen AI: the Claude Code CLI, or an API provider over HTTPS.
        // Returns false with a readable message when the request cannot even start (no key, no CLI...).
        internal bool RunJob(ClaudeJob j, string prompt, string input, Config c, Action<ClaudeJob> onDone, out string err)
        {
            int timeoutMs = Math.Max(5, c.TimeoutSeconds) * 1000;
            ProviderInfo p = Providers.Get(c.Provider);
            j.Provider = p.Short;
            if (p.IsApi)
            {
                HttpSpec spec = Api.BuildPolish(c, prompt, input, out err);
                if (spec == null) return false;
                j.StartHttp(spec, timeoutMs, onDone);
                return true;
            }
            ProcessStartInfo psi = BuildClaudeStartInfo(prompt, c, out err);
            if (psi == null) return false;
            j.Start(psi, input, timeoutMs, onDone);
            return true;
        }

        // null when the job produced usable text; otherwise what to tell the user. Logs the failure without any content.
        internal static string FailureMessage(ClaudeJob j, Config c, string output)
        {
            string secs = (j.ElapsedMs / 1000.0).ToString("0.0") + "s";
            if (j.TimedOut)
            {
                Log.Error(j.Provider + " timed out after " + secs);
                return j.Provider + " did not answer within " + c.TimeoutSeconds + " seconds.\r\n\r\nTry again, or raise the timeout in " + AppName + " Settings > AI provider.";
            }
            if (j.ExitCode == 0 && output.Length > 0) return null;
            if (j.IsHttp)
            {
                Log.Error(j.Provider + " API failed: status=" + j.ExitCode);
                if (j.Error.Length > 0) return j.Error;
                return j.Provider + " returned an empty answer.";
            }
            string detail = (j.Error + "\n" + j.Output).Trim();
            if (detail.Length > 600) detail = detail.Substring(0, 600) + "...";
            Log.Error("Claude failed: exit=" + j.ExitCode + " stderrLen=" + j.Error.Length + " stdoutLen=" + j.Output.Length);
            if (detail.IndexOf("not logged in", StringComparison.OrdinalIgnoreCase) >= 0 || detail.IndexOf("/login", StringComparison.OrdinalIgnoreCase) >= 0)
                return "The Claude Code CLI is not signed in.\r\n\r\nOpen " + AppName + " Settings > Advanced > Sign in..., then Check.\r\nNo Claude plan? Pick another AI in Settings > AI provider.";
            if (detail.Length > 0) return "Claude failed (exit code " + j.ExitCode + ").\r\n\r\n" + detail.Replace("\r\n", "\n").Replace("\n", "\r\n");
            if (j.ExitCode == 0) return "Claude returned an empty answer.";
            return "Claude failed (exit code " + j.ExitCode + ").";
        }

        internal ProcessStartInfo BuildClaudeStartInfo(string prompt, out string err)
        {
            return BuildClaudeStartInfo(prompt, Cfg, out err);
        }

        internal ProcessStartInfo BuildClaudeStartInfo(string prompt, Config c, out string err)
        {
            err = null;
            if (!string.IsNullOrEmpty(c.ClaudePath) && !File.Exists(c.ClaudePath) && c == Cfg) c.ClaudePath = "";
            if (c == Cfg) { if (!ResolveClaudePath(out err)) return null; }
            else if (string.IsNullOrEmpty(c.ClaudePath) || !File.Exists(c.ClaudePath)) { err = "claude.exe not found" + (string.IsNullOrEmpty(c.ClaudePath) ? "." : ": " + c.ClaudePath) + "\r\nSet it on the Advanced tab, or pick another AI on the AI provider tab."; return null; }

            List<string> a = new List<string>();
            string exe = c.ClaudePath;
            if (exe.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
            {
                // npm install of Claude Code: run "node cli.js ..."
                string node = FindOnPath("node.exe");
                if (node == null) { err = "node.exe not found on PATH (needed for the npm install of Claude Code)."; return null; }
                a.Add(exe);
                exe = node;
            }
            a.Add("-p");
            a.Add("--model"); a.Add(c.Model);
            a.Add("--system-prompt"); a.Add(prompt);
            a.Add("--tools"); a.Add("");                 // no tools at all
            a.Add("--strict-mcp-config");                // ignore every configured MCP server
            a.Add("--disable-slash-commands");           // no skills / commands
            a.Add("--setting-sources"); a.Add("");      // ignore user/project/local settings (hooks, plugins)
            a.Add("--no-session-persistence");           // keep these runs out of session history
            a.Add("--no-chrome");
            a.Add("--output-format"); a.Add("text");
            if (!string.IsNullOrEmpty(c.Effort)) { a.Add("--effort"); a.Add(c.Effort); }
            a.AddRange(c.ExtraArgs);

            StringBuilder sb = new StringBuilder();
            foreach (string s in a) { if (sb.Length > 0) sb.Append(' '); sb.Append(QuoteArg(s)); }
            if (sb.Length > 30000) { err = "The style is too long for the command line (max ~30,000 characters including the core rules). Shorten its tone / structure text."; return null; }

            ProcessStartInfo psi = new ProcessStartInfo(exe, sb.ToString());
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = new UTF8Encoding(false);
            psi.StandardErrorEncoding = new UTF8Encoding(false);
            psi.WorkingDirectory = workDir; // empty folder: no CLAUDE.md, no project files

            // Do not inherit variables from a parent Claude session (only matters when started from inside Claude Code).
            List<string> drop = new List<string>();
            foreach (string k in psi.EnvironmentVariables.Keys)
            {
                string u = k.ToUpperInvariant();
                if ((u.StartsWith("CLAUDE") && u != "CLAUDE_CODE_OAUTH_TOKEN" && u != "CLAUDE_CONFIG_DIR") || u == "CLAUDECODE") drop.Add(k);
            }
            foreach (string k in drop) psi.EnvironmentVariables.Remove(k);
            psi.EnvironmentVariables["CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC"] = "1";
            psi.EnvironmentVariables["DISABLE_AUTOUPDATER"] = "1";
            return psi;
        }

        // Windows command-line quoting (CommandLineToArgvW rules).
        internal static string QuoteArg(string s)
        {
            if (s.Length > 0 && s.IndexOfAny(new char[] { ' ', '\t', '\n', '\r', '\v', '"' }) < 0) return s;
            StringBuilder sb = new StringBuilder("\"");
            int bs = 0;
            foreach (char ch in s)
            {
                if (ch == '\\') { bs++; continue; }
                if (ch == '"') { sb.Append('\\', bs * 2 + 1); sb.Append('"'); bs = 0; continue; }
                sb.Append('\\', bs); bs = 0;
                sb.Append(ch);
            }
            sb.Append('\\', bs * 2);
            sb.Append('"');
            return sb.ToString();
        }

        // Sub-points are indented with spaces ("   - detail"). Web editors and chat apps (Gmail, SeaTalk...) often
        // collapse leading spaces, which pulls the sub-point out from under its number. Non-breaking spaces survive that.
        static readonly Regex ListIndent = new Regex(@"^[ \t]+(?=(?:[-*•]|\d{1,3}[.)])[ \t])", RegexOptions.Multiline);

        internal static string KeepListIndent(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return ListIndent.Replace(text, delegate(Match m) { return new string(' ', m.Value.Replace("\t", "    ").Length); });
        }

        internal static string CleanOutput(string raw, string input)
        {
            string s = (raw ?? "").Replace("\r\n", "\n").Trim();
            if (s.StartsWith("```") && s.EndsWith("```") && s.Length >= 6)
            {
                int nl = s.IndexOf('\n');
                s = nl > 0 ? s.Substring(nl + 1, s.Length - nl - 1 - 3).Trim() : s.Trim('`').Trim();
            }
            string inTrim = (input ?? "").Trim();
            string[][] pairs = { new[] { "\"", "\"" }, new[] { "“", "”" }, new[] { "'", "'" } };
            foreach (string[] q in pairs)
            {
                if (s.Length >= 2 && s.StartsWith(q[0]) && s.EndsWith(q[1]) && !inTrim.StartsWith(q[0]) && s.IndexOf(q[1], 1, s.Length - 2) < 0)
                {
                    s = s.Substring(1, s.Length - 2).Trim();
                    break;
                }
            }
            return s.Replace("\n", "\r\n");
        }

        // --------------------------------------------------- hotkey flow
        void OnHotkey()
        {
            Native.MaskAltWin();
            if (capturing) return;
            IntPtr fg = Native.GetForegroundWindow();
            if (popup != null && !popup.IsDisposed && popup.Visible)
            {
                if (fg == popup.Handle || popup.Busy) { Native.ForceForeground(popup.Handle); return; }
                popup.Close();
            }
            capturing = true;
            string text = null;
            Point cursor = Cursor.Position;
            try
            {
                Native.WaitForModifierRelease(1500);
                Native.LeaveMenuModeIfNeeded(fg);
                ClipboardSnapshot saved = ClipboardSnapshot.Take();
                try { text = ClipboardUtil.CaptureSelection(Cfg.CopyWaitMs); }
                finally { saved.Restore(); }
            }
            catch (Exception ex) { Log.Error("Capture failed: " + ex.GetType().Name + ": " + ex.Message); }
            finally { capturing = false; }

            // Pick the style: app rule (e.g. Outlook -> Email) or the default style.
            string procName = "", title = "";
            try
            {
                uint pid;
                Native.GetWindowThreadProcessId(fg, out pid);
                procName = Process.GetProcessById((int)pid).ProcessName;
                StringBuilder tb = new StringBuilder(512);
                Native.GetWindowText(fg, tb, tb.Capacity);
                title = tb.ToString();
            }
            catch { }
            List<Profile> profiles = ProfileStore.Load(profilesDir);
            string reason;
            Profile chosen = ProfileStore.Pick(profiles, Cfg.DefaultProfile, Cfg.AutoProfileByApp, procName, title, out reason);

            popup = new Popup(this, fg, cursor, Cfg, profiles, chosen, reason);
            popup.ShowAndFocus();
            if (text == null || text.Trim().Length == 0)
                popup.ShowError("Nothing selected.\r\n\r\nSelect some text first, then press " + Cfg.Hotkey + ".");
            else
                popup.Start(text);
        }

        internal void PasteInto(IntPtr target, string text)
        {
            ClipboardSnapshot saved = ClipboardSnapshot.Take();
            try
            {
                ClipboardUtil.SetText(text, true);
                Native.ForceForeground(target);
                Thread.Sleep(120);
                Native.WaitForModifierRelease(1000);
                Native.SendCtrlCombo('V');
            }
            catch (Exception ex)
            {
                Log.Error("Replace failed: " + ex.GetType().Name);
                saved.Restore();
                return;
            }
            // Give the target app time to read the clipboard before putting the old content back.
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
            t.Interval = Math.Max(200, Cfg.PasteRestoreDelayMs);
            t.Tick += delegate { t.Stop(); t.Dispose(); saved.Restore(); };
            t.Start();
        }

        static Icon MakeIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    using (GraphicsPath gp = new GraphicsPath())
                    {
                        gp.AddArc(0, 0, 12, 12, 180, 90); gp.AddArc(19, 0, 12, 12, 270, 90);
                        gp.AddArc(19, 19, 12, 12, 0, 90); gp.AddArc(0, 19, 12, 12, 90, 90);
                        gp.CloseFigure();
                        using (Brush b = new LinearGradientBrush(new Point(0, 0), new Point(32, 32), Theme.Navy, Theme.Ocean)) g.FillPath(b, gp);
                    }
                    using (Font f = new Font("Segoe UI", 17f, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (StringFormat sf = new StringFormat())
                    {
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        g.DrawString("P", f, Brushes.White, new RectangleF(0, 1, 32, 32), sf);
                    }
                }
                IntPtr h = bmp.GetHicon();
                Icon tmp = Icon.FromHandle(h);
                Icon copy = (Icon)tmp.Clone();
                Native.DestroyIcon(h);
                return copy;
            }
        }
    }
}
