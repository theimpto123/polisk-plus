// PolisK+ Settings window: General, Styles (tone + structure per situation), Advanced.
// C# 5 / .NET Framework 4.x syntax only (compiled at runtime).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace PolishTool
{
    internal class SettingsForm : Form
    {
        readonly App app;
        readonly Config cfg;
        List<Profile> profiles;
        readonly List<string> originalNames = new List<string>();
        bool dirty, loading;

        // General
        TextBox hotkeyBox; Label hotkeyHint; ComboBox modelBox, effortBox, defaultStyleBox; NumericUpDown timeoutBox; CheckBox byAppBox, startupBox;
        // AI provider tab
        TabControl tabs; TabPage providerPage;
        ComboBox providerBox; Label providerInfo, keyStatus, testResult, modelHint; TextBox keyBox, baseUrlBox; LinkLabel keyLink; Button loadModelsBtn, testBtn;
        Control[] effortRow, keyRow, baseUrlRow;
        readonly Dictionary<string, string> modelByProvider = new Dictionary<string, string>();
        readonly Dictionary<string, string> typedKeys = new Dictionary<string, string>();   // plain text, in memory only until Save encrypts them
        readonly List<string> clearedKeys = new List<string>();
        string shownProvider;
        ClaudeJob testJob;
        // Languages (General tab)
        List<string> langs = new List<string>();
        ListBox langList; TextBox langInput; Label langMsg;
        // Styles
        ListBox styleList; TextBox nameBox, descBox, appsBox, toneBox, structBox, tryIn, tryOut; Button tryBtn; Label tryInfo; ComboBox tryLang;
        Profile current;
        ClaudeJob tryJob;
        // Advanced
        TextBox claudeBox; Label claudeStatus; NumericUpDown copyWaitBox, pasteDelayBox, fontBox;
        // Bottom
        Label saveStatus;

        const string SampleText = "Hi team, em update tình hình tuần này: 1/ Hệ thống quản lý hồ sơ mới đã go-live thứ 3, thời gian xử lý mỗi hồ sơ giảm từ 12 phút xuống 7,5 phút. 2/ Còn khoảng 300 hồ sơ cũ bị lỗi khi chuyển dữ liệu, team đang fix, dự kiến xong 20/11. 3/ Cần anh Minh confirm budget phase 2 trước thứ 6 nhé.";

        public SettingsForm(App app)
        {
            this.app = app;
            cfg = app.Cfg.Clone();
            Text = App.AppName + " Settings";
            if (app.AppIcon != null) Icon = app.AppIcon;
            Font = new Font("Segoe UI", 9f);
            AutoScaleMode = AutoScaleMode.Font;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(940, 720);
            MinimumSize = new Size(820, 600);
            KeyPreview = true;
            BackColor = Theme.Surface;
            ForeColor = Theme.Text;

            tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Padding = new Point(14, 5);
            tabs.TabPages.Add(BuildGeneral());
            tabs.TabPages.Add(BuildProvider());
            tabs.TabPages.Add(BuildStyles());
            tabs.TabPages.Add(BuildAdvanced());

            FlowLayoutPanel bottom = new FlowLayoutPanel();
            bottom.Dock = DockStyle.Bottom;
            bottom.FlowDirection = FlowDirection.RightToLeft;
            bottom.Height = 46;
            bottom.Padding = new Padding(8);
            Button close = Btn("Close", 90);
            Button save = Btn("Save", 90);
            saveStatus = new Label();
            saveStatus.AutoSize = true;
            saveStatus.Margin = new Padding(8, 8, 8, 0);
            saveStatus.ForeColor = Theme.Success;
            bottom.Controls.AddRange(new Control[] { close, save, saveStatus });
            save.Click += delegate { Save(); };
            close.Click += delegate { Close(); };
            AcceptButton = null;

            Controls.Add(tabs);
            Controls.Add(bottom);
            Controls.Add(BuildHeader());
            Theme.Apply(this, save, tryBtn);

            LoadAll();
            // First run, or the chosen AI is not ready (no key, no Claude CLI): open on the AI provider tab and say why.
            string notReady = ProviderProblem(cfg);
            if (notReady != null) { tabs.SelectedTab = providerPage; SetTest(notReady, Theme.Error); }
            FormClosing += OnClosingAsk;
            Deactivate += delegate { app.ResumeHotkey(); };
            Activated += delegate { if (hotkeyBox.Focused) app.SuspendHotkey(); };
            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Control && e.KeyCode == Keys.S) { e.SuppressKeyPress = true; Save(); }
            };
        }

        // ------------------------------------------------------------ helpers
        // Navy-to-ocean band with the app icon (purely decorative).
        Control BuildHeader()
        {
            Panel h = new Panel();
            h.Dock = DockStyle.Top;
            h.Height = 58;
            Icon big = null;
            try { if (app.AppIcon != null) big = new Icon(app.AppIcon, 32, 32); } catch { }
            h.Paint += delegate(object s, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                Rectangle r = h.ClientRectangle;
                if (r.Width <= 0 || r.Height <= 0) return;
                using (System.Drawing.Drawing2D.LinearGradientBrush b = new System.Drawing.Drawing2D.LinearGradientBrush(r, Theme.Navy, Theme.Ocean, 0f))
                    g.FillRectangle(b, r);
                using (Pen line = new Pen(Theme.Silver)) g.DrawLine(line, 0, r.Bottom - 1, r.Right, r.Bottom - 1);
                int x = 16;
                if (big != null) { g.DrawIcon(big, new Rectangle(x, (r.Height - 32) / 2, 32, 32)); x += 44; }
                using (Font f1 = new Font("Segoe UI Semibold", 13f))
                using (Font f2 = new Font("Segoe UI", 9f))
                using (Brush white = new SolidBrush(Color.White))
                using (Brush silver = new SolidBrush(Theme.SilverLight))
                {
                    g.DrawString(App.AppName, f1, white, x, 7);
                    g.DrawString("Tone + structure for every situation  -  version " + App.Version, f2, silver, x + 2, 33);
                }
            };
            h.Resize += delegate { h.Invalidate(); };
            return h;
        }

        static Button Btn(string text, int width)
        {
            Button b = new Button();
            b.Text = text;
            b.Width = width;
            b.Height = 30;
            b.Margin = new Padding(4, 2, 4, 2);
            return b;
        }

        static Label Hint(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.MaximumSize = new Size(560, 0);
            l.ForeColor = Theme.Muted;
            l.Margin = new Padding(3, 0, 3, 10);
            return l;
        }

        static Label Caption(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Margin = new Padding(3, 7, 12, 0);
            return l;
        }

        static TableLayoutPanel Grid()
        {
            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Top;
            t.AutoSize = true;
            t.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            t.ColumnCount = 2;
            t.Padding = new Padding(16, 14, 16, 8);
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return t;
        }

        // Scrolls only when the window is too small for the content.
        static Control Scroller(Control content)
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.AutoScroll = true;
            p.Controls.Add(content);
            return p;
        }

        // Returns { caption, control, hint } so a caller can hide the whole row or change its hint.
        static Control[] Row(TableLayoutPanel t, string caption, Control c, string hint)
        {
            int r = t.RowCount;
            t.RowCount = r + 2;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Label cap = Caption(caption), h = Hint(hint ?? "");
            t.Controls.Add(cap, 0, r);
            t.Controls.Add(c, 1, r);
            t.Controls.Add(h, 1, r + 1);
            return new Control[] { cap, c, h };
        }

        static void ShowRow(Control[] row, bool visible) { foreach (Control c in row) c.Visible = visible; }

        void MarkDirty() { if (!loading) { dirty = true; saveStatus.Text = ""; } }

        // ------------------------------------------------------------ General tab
        TabPage BuildGeneral()
        {
            TabPage page = new TabPage("General");
            TableLayoutPanel t = Grid();

            hotkeyBox = new TextBox();
            hotkeyBox.ReadOnly = true;
            hotkeyBox.BackColor = SystemColors.Window;
            hotkeyBox.Width = 220;
            hotkeyBox.Enter += delegate { app.SuspendHotkey(); hotkeyHint.Text = "Press the new key combination now (Backspace = clear)."; };
            hotkeyBox.Leave += delegate { app.ResumeHotkey(); hotkeyHint.Text = HotkeyHintText; };
            hotkeyBox.KeyDown += OnHotkeyKey;
            Row(t, "Hotkey", hotkeyBox, HotkeyHintText);
            hotkeyHint = (Label)t.GetControlFromPosition(1, t.RowCount - 1);

            Row(t, "Output languages", BuildLanguages(),
                "The first language is the default: every hotkey press writes in it. The popup lists only these languages (max " + Config.MaxLanguages + "), " +
                "so there is nothing long to scroll. Switch there with the Language box or Alt+1.." + Config.MaxLanguages + ".\n" +
                "Type any language, e.g. Japanese, Chinese (Traditional), Portuguese (Brazil), English (British).");

            defaultStyleBox = new ComboBox();
            defaultStyleBox.DropDownStyle = ComboBoxStyle.DropDownList;
            defaultStyleBox.Width = 220;
            defaultStyleBox.SelectedIndexChanged += delegate { MarkDirty(); };
            Row(t, "Default style", defaultStyleBox, "Used when no app rule matches. \"Auto\" detects chat / email / report by itself (recommended).");

            byAppBox = new CheckBox();
            byAppBox.Text = "Pick the style automatically from the app you are in";
            byAppBox.AutoSize = true;
            byAppBox.CheckedChanged += delegate { MarkDirty(); };
            Row(t, "", byAppBox, "Uses each style's \"Apps\" list (Styles tab): Outlook -> Email, SeaTalk / Teams -> Chat, Word -> Document. You can always switch style in the popup with Ctrl+1..9.");

            startupBox = new CheckBox();
            startupBox.Text = "Start " + App.AppName + " when I sign in to Windows";
            startupBox.AutoSize = true;
            startupBox.CheckedChanged += delegate { MarkDirty(); };
            Row(t, "", startupBox, "Recommended. Windows may take a few minutes after sign-in to start it (it starts apps one by one).");

            Button rec = Btn("Use recommended settings", 200);
            rec.Click += delegate
            {
                hotkeyBox.Text = "Ctrl+Alt+P"; effortBox.SelectedIndex = 0; timeoutBox.Value = 60;
                if (!CurrentProvider.IsApi) modelBox.Text = "sonnet";
                SelectDefaultStyle("Auto"); byAppBox.Checked = true; startupBox.Checked = true;
                int en = IndexOfLanguage("English");
                if (en < 0) { langs.Insert(0, "English"); if (langs.Count > Config.MaxLanguages) langs.RemoveAt(langs.Count - 1); }
                else if (en > 0) { langs.RemoveAt(en); langs.Insert(0, "English"); }
                RefreshLanguages(0);
                MarkDirty();
            };
            Row(t, "", rec, "Hotkey Ctrl+Alt+P, English as the default language (your other languages are kept), Auto style, pick by app, start with Windows, 60 s timeout. Your AI provider is kept (with Claude: model sonnet, default effort). Click Save to apply.");

            page.Controls.Add(Scroller(t));
            return page;
        }

        // ------------------------------------------------------------ AI provider tab
        ProviderInfo CurrentProvider
        {
            get { int i = providerBox.SelectedIndex; return Providers.All[i >= 0 && i < Providers.All.Length ? i : 0]; }
        }

        TabPage BuildProvider()
        {
            TabPage page = new TabPage("AI provider");
            providerPage = page;
            TableLayoutPanel t = Grid();

            providerBox = new ComboBox();
            providerBox.DropDownStyle = ComboBoxStyle.DropDownList;
            providerBox.Width = 400;
            foreach (ProviderInfo p in Providers.All) providerBox.Items.Add(p.Name);
            providerBox.SelectedIndexChanged += delegate { if (!loading) { ShowProvider(); MarkDirty(); } };
            providerInfo = (Label)Row(t, "AI provider", providerBox, "")[2];

            baseUrlBox = new TextBox();
            baseUrlBox.Width = 400;
            baseUrlBox.TextChanged += delegate { MarkDirty(); };
            baseUrlRow = Row(t, "Base URL", baseUrlBox,
                "The service's address ending in /v1, for example https://openrouter.ai/api/v1, https://api.groq.com/openai/v1, https://api.deepseek.com/v1, " +
                "or http://localhost:11434/v1 for Ollama on this computer (no key needed).");

            FlowLayoutPanel kp = new FlowLayoutPanel();
            kp.AutoSize = true;
            kp.WrapContents = false;
            kp.Margin = new Padding(0);
            keyBox = new TextBox();
            keyBox.Width = 400;
            keyBox.UseSystemPasswordChar = true;
            keyBox.Margin = new Padding(3, 5, 3, 3);
            keyBox.HandleCreated += delegate { SendMessage(keyBox.Handle, 0x1501, (IntPtr)1, "Paste your API key here"); };
            keyBox.TextChanged += delegate
            {
                if (loading) return;
                if (keyBox.Text.Trim().Length > 0) clearedKeys.Remove(CurrentProvider.Id);
                UpdateKeyStatus();
                MarkDirty();
            };
            Button removeKey = Btn("Remove key", 100);
            removeKey.Click += delegate
            {
                string id = CurrentProvider.Id;
                typedKeys.Remove(id);
                if (!clearedKeys.Contains(id)) clearedKeys.Add(id);
                bool was = loading; loading = true; keyBox.Text = ""; loading = was;
                UpdateKeyStatus();
                MarkDirty();
            };
            keyLink = new LinkLabel();
            keyLink.Text = "Get a key";
            keyLink.AutoSize = true;
            keyLink.LinkColor = Theme.Ocean;
            keyLink.Margin = new Padding(8, 9, 3, 0);
            keyLink.LinkClicked += delegate
            {
                string url = CurrentProvider.KeyUrl;
                if (url.StartsWith("https://")) { try { Process.Start(url); } catch (Exception ex) { Log.Error("Open link failed: " + ex.Message); } }
            };
            kp.Controls.AddRange(new Control[] { keyBox, removeKey, keyLink });
            keyRow = Row(t, "API key", kp, "");
            keyStatus = (Label)keyRow[2];

            FlowLayoutPanel mp = new FlowLayoutPanel();
            mp.AutoSize = true;
            mp.WrapContents = false;
            mp.Margin = new Padding(0);
            modelBox = new ComboBox();
            modelBox.DropDownStyle = ComboBoxStyle.DropDown;
            modelBox.Width = 300;
            modelBox.MaxDropDownItems = 15;
            modelBox.Margin = new Padding(3, 5, 3, 3);
            modelBox.TextChanged += delegate { MarkDirty(); };
            loadModelsBtn = Btn("Load models", 110);
            loadModelsBtn.Click += delegate { LoadModels(); };
            mp.Controls.AddRange(new Control[] { modelBox, loadModelsBtn });
            modelHint = (Label)Row(t, "Model", mp, "")[2];

            effortBox = new ComboBox();
            effortBox.DropDownStyle = ComboBoxStyle.DropDownList;
            effortBox.Items.AddRange(new object[] { "(default)", "low", "medium", "high" });
            effortBox.Width = 220;
            effortBox.SelectedIndexChanged += delegate { MarkDirty(); };
            effortRow = Row(t, "Effort", effortBox, "(default) is recommended. \"low\" can be slightly faster for short texts.");

            timeoutBox = new NumericUpDown();
            timeoutBox.Minimum = 10; timeoutBox.Maximum = 300; timeoutBox.Width = 80;
            timeoutBox.ValueChanged += delegate { MarkDirty(); };
            Row(t, "Timeout (seconds)", timeoutBox, "How long to wait for an answer. 60 is plenty; raise it for very long texts.");

            testBtn = Btn("Test connection", 150);
            testBtn.Click += delegate { RunTest(); };
            testResult = (Label)Row(t, "", testBtn, TestHint)[2];

            Label privacy = Hint(
                "Your text is sent to the AI provider you choose here, under your own account and its terms. " + App.AppName + " keeps no copy and logs errors only.\n" +
                "On a work computer, follow your company's rules about which AI services may receive work text.");
            privacy.Margin = new Padding(3, 7, 3, 10);
            Row(t, "Privacy", privacy, "");

            page.Controls.Add(Scroller(t));
            return page;
        }

        const string TestHint = "Sends one short request with the settings on this tab (saved or not) and tells you whether it works.";
        const string KeyNote = "Stored encrypted for your Windows account on this computer. Never written to the log; only sent to the provider above.";

        void SetTest(string text, Color color)
        {
            testResult.ForeColor = color;
            testResult.Text = Regex.Replace(text ?? "", "(\\r?\\n)+", "\n");
        }

        // Keep what was typed for the provider being left, then show the newly selected one.
        void StashProvider()
        {
            if (shownProvider == null) return;
            modelByProvider[shownProvider] = modelBox.Text.Trim();
            string k = keyBox.Text.Trim();
            if (k.Length > 0) typedKeys[shownProvider] = k; else typedKeys.Remove(shownProvider);
        }

        void ShowProvider()
        {
            bool was = loading;
            loading = true;
            StashProvider();
            ProviderInfo p = CurrentProvider;
            shownProvider = p.Id;
            providerInfo.Text = p.Needs;
            modelBox.Items.Clear();
            modelBox.Items.AddRange(p.Models);
            string m;
            modelBox.Text = modelByProvider.TryGetValue(p.Id, out m) ? m : p.DefaultModel;
            keyBox.Text = typedKeys.ContainsKey(p.Id) ? typedKeys[p.Id] : "";
            ShowRow(effortRow, !p.IsApi);
            ShowRow(keyRow, p.IsApi);
            ShowRow(baseUrlRow, p.Id == Providers.Custom);
            loadModelsBtn.Visible = p.IsApi;
            keyLink.Visible = p.KeyUrl.Length > 0;
            modelHint.ForeColor = Theme.Muted;
            if (p.Id == Providers.Claude)
                modelHint.Text = "sonnet: recommended, best balance of quality and speed (~3-7 s).\n" +
                    "haiku: about the same speed here, but weaker with Vietnamese (it once read \"thu 3\" as \"third time\").\n" +
                    "opus: highest quality, slower, uses more of your Claude plan.";
            else if (p.Id == Providers.OpenAI)
                modelHint.Text = "Click Load models to list what your key can use, newest first. A small \"mini\" model is fast, cheap and good enough for polishing. You can also type a model name.";
            else if (p.Id == Providers.Gemini)
                modelHint.Text = "Click Load models to list what your key can use, newest first. \"flash\" models are fast and cheap; \"pro\" is higher quality and slower. You can also type a model name.";
            else
                modelHint.Text = "Type the model name exactly as the service lists it, or click Load models.";
            UpdateKeyStatus();
            SetTest(TestHint, Theme.Muted);
            loading = was;
        }

        void UpdateKeyStatus()
        {
            ProviderInfo p = CurrentProvider;
            if (!p.IsApi) return;
            string status;
            keyStatus.ForeColor = Theme.Muted;
            if (keyBox.Text.Trim().Length > 0) status = "New key typed. Click Save to store it.";
            else if (clearedKeys.Contains(p.Id)) status = "The saved key will be removed when you click Save.";
            else if (cfg.HasSavedKey(p.Id)) { status = "A key is saved (hidden). Paste a new one to replace it."; keyStatus.ForeColor = Theme.Success; }
            else if (cfg.GetKey(p.Id).Length > 0) status = "Using the key from the " + string.Join(" / ", p.KeyEnv) + " environment variable.";
            else if (p.Id == Providers.Custom) status = "No key yet. Paste the key from your service (not needed for a local model on localhost).";
            else status = "No key yet. Click \"Get a key\", create one on the " + p.Short + " site, and paste it here.";
            keyStatus.Text = status + "\n" + KeyNote;
        }

        // null when the configured AI can be used; otherwise the reason (no key, no model, no Claude CLI...).
        string ProviderProblem(Config c)
        {
            ProviderInfo p = Providers.Get(c.Provider);
            string err;
            if (p.IsApi) return Api.BuildPolish(c, "x", "x", out err) == null ? FirstLine(err) : null;
            if (string.IsNullOrEmpty(c.ClaudePath) || !File.Exists(c.ClaudePath))
                return "Claude Code was not found on this computer. Install it and run Setup again, or choose another AI here (OpenAI, Gemini...).";
            return null;
        }

        static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int i = s.IndexOfAny(new char[] { '\r', '\n' });
            return i > 0 ? s.Substring(0, i) : s;
        }

        void LoadModels()
        {
            Config c = BuildConfigFromUi();
            string err;
            HttpSpec spec = Api.BuildListModels(c, out err);
            if (spec == null) { modelHint.ForeColor = Theme.Error; modelHint.Text = FirstLine(err); return; }
            string pid = shownProvider;
            loadModelsBtn.Enabled = false;
            modelHint.ForeColor = Theme.Muted;
            modelHint.Text = "Loading the model list from " + spec.Provider.Short + "...";
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<string> ids = null;
                string msg = null;
                try
                {
                    string body;
                    int status = Http.Send(spec, 20000, null, out body);
                    if (status >= 200 && status < 300) ids = Api.ParseModels(spec, body);
                    else msg = Api.ErrorMessage(spec, status, body);
                }
                catch (System.Net.WebException ex) { msg = Api.NetworkMessage(spec, ex); }
                catch (Exception ex) { msg = "Could not read the model list: " + ex.Message; }
                try
                {
                    if (!IsDisposed) BeginInvoke(new Action(delegate
                    {
                        loadModelsBtn.Enabled = true;
                        if (shownProvider != pid) return;
                        if (ids == null || ids.Count == 0)
                        {
                            modelHint.ForeColor = Theme.Error;
                            modelHint.Text = Regex.Replace(msg ?? "No chat models found for this key.", "(\\r?\\n)+", "\n");
                            return;
                        }
                        string keep = modelBox.Text.Trim();
                        modelBox.Items.Clear();
                        modelBox.Items.AddRange(ids.ToArray());
                        string picked = "";
                        if (pid != Providers.Custom && !ids.Contains(keep))
                        {
                            picked = SuggestModel(pid, ids);
                            modelBox.Text = picked;   // marks the form dirty
                        }
                        else modelBox.Text = keep;
                        modelHint.ForeColor = Theme.Success;
                        modelHint.Text = ids.Count + " models loaded" + (picked.Length > 0 ? ". Picked " + picked + "; open the list to choose another." : ". Open the list to choose.");
                    }));
                }
                catch (InvalidOperationException) { }
            });
        }

        // A sensible fast, cheap default from the provider's own list (newest first).
        static string SuggestModel(string provider, List<string> ids)
        {
            foreach (string id in ids)
            {
                if (Regex.IsMatch(id, "preview|exp|-\\d{4}-\\d{2}-\\d{2}$")) continue;
                if (provider == Providers.OpenAI && id.Contains("mini")) return id;
                if (provider == Providers.Gemini && id.Contains("flash") && !id.Contains("lite")) return id;
            }
            return ids[0];
        }

        void RunTest()
        {
            if (testJob != null && !testJob.Done)
            {
                testJob.Cancel(); testJob = null;
                testBtn.Text = "Test connection";
                SetTest("Cancelled.", Theme.Muted);
                return;
            }
            Config c = BuildConfigFromUi();
            ProviderInfo p = Providers.Get(c.Provider);
            ClaudeJob j = new ClaudeJob();
            testJob = j;
            string err;
            bool started = app.RunJob(j, "You are a connection test. Reply with the single word OK.", "ping", c, delegate(ClaudeJob done)
            {
                try
                {
                    if (!IsDisposed) BeginInvoke(new Action(delegate
                    {
                        if (done != testJob) return;
                        testBtn.Text = "Test connection";
                        if (done.Cancelled) return;
                        string fail = App.FailureMessage(done, c, done.Output.Trim());
                        if (fail != null) SetTest(fail, Theme.Error);
                        else SetTest("Working: " + p.Short + " (" + c.ActiveModel + ") answered in " + (done.ElapsedMs / 1000.0).ToString("0.0") + " s." + (dirty ? " Click Save to keep these settings." : ""), Theme.Success);
                    }));
                }
                catch (InvalidOperationException) { }
            }, out err);
            if (!started) { testJob = null; SetTest(p.IsApi ? FirstLine(err) : err, Theme.Error); return; }
            testBtn.Text = "Stop";
            SetTest("Testing " + p.Short + " (" + c.ActiveModel + ")...", Theme.Muted);
        }

        // Suggestions while typing (not a list to scroll): any other language name is accepted too.
        static readonly string[] KnownLanguages = {
            "English", "English (British)", "English (American)", "Vietnamese", "Chinese (Simplified)", "Chinese (Traditional)", "Cantonese",
            "Japanese", "Korean", "Thai", "Indonesian", "Malay", "Filipino", "Khmer", "Lao", "Burmese", "Hindi", "Bengali", "Tamil", "Urdu",
            "Arabic", "Hebrew", "Persian", "Turkish", "Russian", "Ukrainian", "Polish", "Czech", "German", "French", "Spanish",
            "Spanish (Latin America)", "Portuguese", "Portuguese (Brazil)", "Italian", "Dutch", "Swedish", "Norwegian", "Danish",
            "Finnish", "Greek", "Hungarian", "Romanian" };

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        Control BuildLanguages()
        {
            FlowLayoutPanel row = new FlowLayoutPanel();
            row.AutoSize = true;
            row.WrapContents = false;
            row.Margin = new Padding(0);

            langList = new ListBox();
            langList.IntegralHeight = false;
            langList.Size = new Size(200, 128);
            langList.Margin = new Padding(3, 3, 8, 3);

            FlowLayoutPanel side = new FlowLayoutPanel();
            side.AutoSize = true;
            side.FlowDirection = FlowDirection.TopDown;
            side.WrapContents = false;

            FlowLayoutPanel addRow = new FlowLayoutPanel();
            addRow.AutoSize = true;
            addRow.WrapContents = false;
            addRow.Margin = new Padding(0);
            langInput = new TextBox();
            langInput.Width = 190;
            langInput.Margin = new Padding(3, 5, 3, 3);
            langInput.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            langInput.AutoCompleteSource = AutoCompleteSource.CustomSource;
            AutoCompleteStringCollection ac = new AutoCompleteStringCollection();
            ac.AddRange(KnownLanguages);
            langInput.AutoCompleteCustomSource = ac;
            langInput.HandleCreated += delegate { SendMessage(langInput.Handle, 0x1501, (IntPtr)1, "Type a language, e.g. Japanese"); }; // EM_SETCUEBANNER
            Button add = Btn("Add", 70);
            addRow.Controls.AddRange(new Control[] { langInput, add });

            FlowLayoutPanel btnRow = new FlowLayoutPanel();
            btnRow.AutoSize = true;
            btnRow.WrapContents = false;
            btnRow.Margin = new Padding(0);
            Button up = Btn("Make default", 110), moveUp = Btn("Move up", 80), remove = Btn("Remove", 80);
            btnRow.Controls.AddRange(new Control[] { up, moveUp, remove });

            langMsg = new Label();
            langMsg.AutoSize = true;
            langMsg.MaximumSize = new Size(300, 0);
            langMsg.ForeColor = Theme.Muted;
            langMsg.Margin = new Padding(3, 6, 3, 0);

            side.Controls.AddRange(new Control[] { addRow, btnRow, langMsg });
            row.Controls.AddRange(new Control[] { langList, side });

            add.Click += delegate { AddLanguage(); };
            langInput.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AddLanguage(); }
            };
            up.Click += delegate { MoveLanguage(langList.SelectedIndex, 0); };
            moveUp.Click += delegate { int i = langList.SelectedIndex; if (i > 0) MoveLanguage(i, i - 1); };
            remove.Click += delegate
            {
                int i = langList.SelectedIndex;
                if (i < 0) { langMsg.Text = "Select a language first."; return; }
                if (langs.Count <= 1) { langMsg.Text = "Keep at least one language."; return; }
                string gone = langs[i];
                langs.RemoveAt(i);
                RefreshLanguages(Math.Min(i, langs.Count - 1));
                langMsg.Text = gone + " removed." + (i == 0 ? " " + langs[0] + " is now the default." : "");
                MarkDirty();
            };
            return row;
        }

        int IndexOfLanguage(string name)
        {
            for (int i = 0; i < langs.Count; i++) if (string.Equals(langs[i], name, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        void AddLanguage()
        {
            string s = System.Text.RegularExpressions.Regex.Replace(langInput.Text.Trim(), "\\s+", " ");
            if (s.Length == 0) { langMsg.Text = "Type a language name, e.g. Japanese."; return; }
            foreach (string k in KnownLanguages) if (string.Equals(k, s, StringComparison.OrdinalIgnoreCase)) s = k;
            if (s.Length > 0 && char.IsLower(s[0])) s = char.ToUpper(s[0]) + s.Substring(1);
            if (!Config.IsValidLanguage(s)) { langMsg.Text = "Use letters, spaces and ( ) only, up to 40 characters."; return; }
            int at = IndexOfLanguage(s);
            if (at >= 0) { langList.SelectedIndex = at; langMsg.Text = s + " is already in the list."; return; }
            if (langs.Count >= Config.MaxLanguages) { langMsg.Text = "Up to " + Config.MaxLanguages + " languages. Remove one first."; return; }
            langs.Add(s);
            RefreshLanguages(langs.Count - 1);
            langInput.Text = "";
            langMsg.Text = s + " added. Click Save to use it.";
            MarkDirty();
        }

        void MoveLanguage(int from, int to)
        {
            if (from < 0) { langMsg.Text = "Select a language first."; return; }
            if (from == to) return;
            string s = langs[from];
            langs.RemoveAt(from);
            langs.Insert(to, s);
            RefreshLanguages(to);
            langMsg.Text = to == 0 ? s + " is now the default." : "";
            MarkDirty();
        }

        void RefreshLanguages(int select)
        {
            langList.Items.Clear();
            for (int i = 0; i < langs.Count; i++) langList.Items.Add((i + 1) + ". " + langs[i] + (i == 0 ? "   (default)" : ""));
            if (select >= 0 && select < langs.Count) langList.SelectedIndex = select;
            if (tryLang != null)
            {
                string keep = tryLang.SelectedItem as string;
                tryLang.Items.Clear();
                foreach (string l in langs) tryLang.Items.Add(l);
                int k = keep != null ? tryLang.Items.IndexOf(keep) : -1;
                tryLang.SelectedIndex = k >= 0 ? k : 0;
            }
        }

        const string HotkeyHintText ="Click the box, then press the keys. Must include Ctrl, Alt or Win. Recommended: Ctrl+Alt+P (Ctrl+Shift+... often clashes with browser shortcuts).";

        void OnHotkeyKey(object sender, KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
            e.Handled = true;
            Keys k = e.KeyCode;
            if (k == Keys.ControlKey || k == Keys.ShiftKey || k == Keys.Menu || k == Keys.LWin || k == Keys.RWin) return;
            if (k == Keys.Back || k == Keys.Delete) { hotkeyBox.Text = ""; MarkDirty(); return; }
            bool win = (Native.GetAsyncKeyState(0x5B) & 0x8000) != 0 || (Native.GetAsyncKeyState(0x5C) & 0x8000) != 0;
            List<string> parts = new List<string>();
            if (e.Control) parts.Add("Ctrl");
            if (e.Alt) parts.Add("Alt");
            if (e.Shift) parts.Add("Shift");
            if (win) parts.Add("Win");
            if (!e.Control && !e.Alt && !win) { hotkeyHint.Text = "Add Ctrl, Alt or Win to the combination."; return; }
            string key = (k >= Keys.D0 && k <= Keys.D9) ? ((int)(k - Keys.D0)).ToString() : k.ToString();
            string combo = string.Join("+", parts.ToArray()) + "+" + key;
            hotkeyBox.Text = combo;
            hotkeyHint.Text = app.CanRegister(combo) ? combo + " is free. Click Save to use it." : combo + " is already used by another app - pick another one.";
            MarkDirty();
        }

        void SelectDefaultStyle(string name)
        {
            for (int i = 0; i < defaultStyleBox.Items.Count; i++)
                if (string.Equals((string)defaultStyleBox.Items[i], name, StringComparison.OrdinalIgnoreCase)) { defaultStyleBox.SelectedIndex = i; return; }
            if (defaultStyleBox.Items.Count > 0) defaultStyleBox.SelectedIndex = 0;
        }

        // ------------------------------------------------------------ Styles tab
        TabPage BuildStyles()
        {
            TabPage page = new TabPage("Styles (tone + structure)");
            TableLayoutPanel outer = new TableLayoutPanel();
            outer.Dock = DockStyle.Fill;
            outer.ColumnCount = 2;
            outer.Padding = new Padding(10);
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Left: list + buttons
            TableLayoutPanel left = new TableLayoutPanel();
            left.Dock = DockStyle.Fill;
            left.RowCount = 3;
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Label intro = new Label();
            intro.Text = "Each style = one situation. The popup picks one by app, or press Ctrl+1..9 to switch.";
            intro.AutoSize = true;
            intro.MaximumSize = new Size(180, 0);
            intro.ForeColor = Theme.Muted;
            intro.Margin = new Padding(0, 0, 0, 6);
            styleList = new ListBox();
            styleList.Dock = DockStyle.Fill;
            styleList.IntegralHeight = false;
            styleList.SelectedIndexChanged += delegate { ShowStyle(); };
            FlowLayoutPanel lb = new FlowLayoutPanel();
            lb.AutoSize = true;
            lb.Dock = DockStyle.Fill;
            lb.MinimumSize = new Size(184, 0);
            lb.MaximumSize = new Size(184, 0);
            lb.Margin = new Padding(0, 6, 0, 0);
            Button bNew = Btn("New", 82), bDup = Btn("Duplicate", 82), bUp = Btn("Move up", 82), bDown = Btn("Move down", 82), bDel = Btn("Delete", 82), bRestore = Btn("Restore defaults", 170);
            lb.Controls.AddRange(new Control[] { bNew, bDup, bUp, bDown, bDel, bRestore });
            bNew.Click += delegate { AddStyle(null); };
            bDup.Click += delegate { if (current != null) AddStyle(current); };
            bUp.Click += delegate { MoveStyle(-1); };
            bDown.Click += delegate { MoveStyle(1); };
            bDel.Click += delegate { DeleteStyle(); };
            bRestore.Click += delegate { RestoreStyles(); };
            left.Controls.Add(intro, 0, 0);
            left.Controls.Add(styleList, 0, 1);
            left.Controls.Add(lb, 0, 2);

            // Right: editor
            TableLayoutPanel right = new TableLayoutPanel();
            right.Dock = DockStyle.Fill;
            right.ColumnCount = 2;
            right.Padding = new Padding(10, 0, 0, 0);
            right.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            nameBox = new TextBox(); nameBox.Width = 220;
            descBox = new TextBox(); descBox.Dock = DockStyle.Fill;
            appsBox = new TextBox(); appsBox.Dock = DockStyle.Fill;
            toneBox = Multi(); structBox = Multi();
            nameBox.TextChanged += delegate { if (current != null && !loading) { current.Name = nameBox.Text.Trim(); RefreshListText(); MarkDirty(); } };
            descBox.TextChanged += delegate { if (current != null && !loading) { current.Description = descBox.Text.Trim(); MarkDirty(); } };
            appsBox.TextChanged += delegate { if (current != null && !loading) { current.Apps = Profile.SplitApps(appsBox.Text); MarkDirty(); } };
            toneBox.TextChanged += delegate { if (current != null && !loading) { current.Tone = toneBox.Text; MarkDirty(); } };
            structBox.TextChanged += delegate { if (current != null && !loading) { current.Structure = structBox.Text; MarkDirty(); } };

            int r = 0;
            right.RowCount = 9;
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // name
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // desc
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // apps
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // apps hint
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // tone caption
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 32)); // tone
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // structure caption
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 32)); // structure
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 36)); // try it
            right.Controls.Add(Caption("Name"), 0, r); right.Controls.Add(nameBox, 1, r++);
            right.Controls.Add(Caption("Description"), 0, r); right.Controls.Add(descBox, 1, r++);
            right.Controls.Add(Caption("Apps"), 0, r); right.Controls.Add(appsBox, 1, r++);
            right.Controls.Add(Hint("Process names, comma-separated (outlook, olk, seatalk, ms-teams, winword), or title:Text to match the window title (title:Gmail). Leave empty to use this style only when you pick it."), 1, r++);
            Label tc = Caption("Tone - how it should sound (formal / friendly, British or American English, words to use or avoid...)");
            right.Controls.Add(tc, 0, r); right.SetColumnSpan(tc, 2); r++;
            right.Controls.Add(toneBox, 0, r); right.SetColumnSpan(toneBox, 2); r++;
            Label sc = Caption("Output structure - how it should be laid out (paragraphs or numbered list, greeting / sign-off, max length...)");
            right.Controls.Add(sc, 0, r); right.SetColumnSpan(sc, 2); r++;
            right.Controls.Add(structBox, 0, r); right.SetColumnSpan(structBox, 2); r++;
            Control tryPanel = BuildTryPanel();
            right.Controls.Add(tryPanel, 0, r); right.SetColumnSpan(tryPanel, 2);

            outer.Controls.Add(left, 0, 0);
            outer.Controls.Add(right, 1, 0);
            page.Controls.Add(outer);
            return page;
        }

        static TextBox Multi()
        {
            TextBox t = new TextBox();
            t.Multiline = true;
            t.ScrollBars = ScrollBars.Vertical;
            t.AcceptsReturn = true;
            t.AcceptsTab = false;
            t.WordWrap = true;
            t.Dock = DockStyle.Fill;
            t.Font = new Font("Consolas", 9.5f);
            return t;
        }

        Control BuildTryPanel()
        {
            GroupBox g = new GroupBox();
            g.Text = "Try this style (uses the unsaved text above and the AI on the AI provider tab)";
            g.Dock = DockStyle.Fill;
            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.ColumnCount = 3;
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            t.RowCount = 2;
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tryIn = Multi(); tryIn.Font = Font; tryIn.Text = SampleText;
            tryOut = Multi(); tryOut.Font = Font; tryOut.ReadOnly = true; tryOut.BackColor = SystemColors.Window;
            tryBtn = Btn("Try  >", 80);
            tryBtn.Anchor = AnchorStyles.None;
            tryBtn.Click += delegate { RunTry(); };
            tryInfo = new Label();
            tryInfo.AutoSize = true;
            tryInfo.ForeColor = Theme.Muted;
            FlowLayoutPanel lp = new FlowLayoutPanel();
            lp.AutoSize = true;
            lp.WrapContents = false;
            lp.Margin = new Padding(0);
            Label ll = new Label();
            ll.Text = "Language:";
            ll.AutoSize = true;
            ll.Margin = new Padding(3, 6, 3, 0);
            tryLang = new ComboBox();
            tryLang.DropDownStyle = ComboBoxStyle.DropDownList;
            tryLang.Width = 150;
            tryLang.MaxDropDownItems = Config.MaxLanguages;
            lp.Controls.AddRange(new Control[] { ll, tryLang });
            t.Controls.Add(lp, 0, 1);
            t.Controls.Add(tryIn, 0, 0);
            t.Controls.Add(tryBtn, 1, 0);
            t.Controls.Add(tryOut, 2, 0);
            t.Controls.Add(tryInfo, 2, 1);
            g.Controls.Add(t);
            return g;
        }

        void ShowStyle()
        {
            int i = styleList.SelectedIndex;
            current = i >= 0 && i < profiles.Count ? profiles[i] : null;
            bool was = loading;
            loading = true;
            nameBox.Text = current != null ? current.Name : "";
            descBox.Text = current != null ? current.Description : "";
            appsBox.Text = current != null ? string.Join(", ", current.Apps.ToArray()) : "";
            toneBox.Text = current != null ? current.Tone.Replace("\r\n", "\n").Replace("\n", "\r\n") : "";
            structBox.Text = current != null ? current.Structure.Replace("\r\n", "\n").Replace("\n", "\r\n") : "";
            loading = was;
            nameBox.Enabled = descBox.Enabled = appsBox.Enabled = toneBox.Enabled = structBox.Enabled = current != null;
        }

        void RefreshStyleLists(string select)
        {
            bool was = loading;
            loading = true;
            styleList.Items.Clear();
            for (int i = 0; i < profiles.Count; i++) styleList.Items.Add(ListText(i));
            string def = defaultStyleBox.SelectedItem as string ?? cfg.DefaultProfile;
            defaultStyleBox.Items.Clear();
            foreach (Profile p in profiles) defaultStyleBox.Items.Add(p.Name);
            SelectDefaultStyle(def);
            loading = was;
            int idx = 0;
            for (int i = 0; i < profiles.Count; i++) if (profiles[i].Name == select) idx = i;
            if (profiles.Count > 0) styleList.SelectedIndex = idx; else ShowStyle();
        }

        string ListText(int i)
        {
            return (i < 9 ? (i + 1) + ". " : "    ") + profiles[i].Name + (profiles[i].Apps.Count > 0 ? "  (auto)" : "");
        }

        void RefreshListText()
        {
            int i = styleList.SelectedIndex;
            if (i < 0) return;
            bool was = loading;
            loading = true;
            styleList.Items[i] = ListText(i);
            loading = was;
        }

        void AddStyle(Profile from)
        {
            Profile p;
            if (from != null) { p = from.Clone(); p.Apps = new List<string>(); p.Name = UniqueName(from.Name + " copy"); }
            else
            {
                p = new Profile();
                p.Name = UniqueName("My style");
                p.Description = "";
                p.Tone = "<!-- Describe the voice. Examples: formal / friendly; British or American English; words to prefer or avoid. -->\r\n- ";
                p.Structure = "<!-- Describe the layout. Examples: paragraphs or numbered list; keep or add greeting/sign-off; max length. -->\r\n- ";
            }
            p.Notes = "";
            int at = styleList.SelectedIndex >= 0 ? styleList.SelectedIndex + 1 : profiles.Count;
            profiles.Insert(at, p);
            MarkDirty();
            RefreshStyleLists(p.Name);
            nameBox.Focus();
            nameBox.SelectAll();
        }

        string UniqueName(string baseName)
        {
            string n = baseName;
            int k = 2;
            while (ProfileStore.Find(profiles, n) != null) n = baseName + " " + (k++);
            return n;
        }

        void MoveStyle(int delta)
        {
            int i = styleList.SelectedIndex, j = i + delta;
            if (i < 0 || j < 0 || j >= profiles.Count) return;
            Profile p = profiles[i];
            profiles.RemoveAt(i);
            profiles.Insert(j, p);
            MarkDirty();
            RefreshStyleLists(p.Name);
        }

        void DeleteStyle()
        {
            if (current == null) return;
            if (profiles.Count <= 1) { MessageBox.Show(this, "Keep at least one style.", App.AppName); return; }
            if (MessageBox.Show(this, "Delete the style \"" + current.Name + "\"?", App.AppName, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            int i = styleList.SelectedIndex;
            profiles.RemoveAt(i);
            MarkDirty();
            RefreshStyleLists(profiles[Math.Min(i, profiles.Count - 1)].Name);
        }

        void RestoreStyles()
        {
            if (MessageBox.Show(this,
                "Restore the 8 built-in styles (Auto, Chat, Email, Report, Leadership, Document, External, Light touch) to their original text?\n\n" +
                "Your own styles are kept. Changed built-in files are backed up as .bak. Unsaved edits on this tab are discarded.",
                App.AppName, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            try
            {
                int n = app.RestoreDefaultStyles();
                string sel = current != null ? current.Name : "Auto";
                LoadProfiles();
                RefreshStyleLists(sel);
                saveStatus.Text = n + " built-in styles restored.";
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, App.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        void RunTry()
        {
            if (current == null) return;
            if (tryJob != null && !tryJob.Done) { tryJob.Cancel(); tryJob = null; tryBtn.Text = "Try  >"; tryInfo.Text = "Cancelled."; return; }
            string text = tryIn.Text.Trim();
            if (text.Length == 0) { tryInfo.Text = "Type or paste some text on the left first."; return; }
            Config c = BuildConfigFromUi();
            string err;
            string lang = tryLang.SelectedItem as string ?? c.DefaultLanguage;
            string prompt = app.ComposePrompt(current, lang, out err);
            if (prompt == null) { tryOut.Text = err; return; }
            string ai = Providers.Get(c.Provider).Short + " " + c.ActiveModel;
            ClaudeJob j = new ClaudeJob();
            tryJob = j;
            bool started = app.RunJob(j, prompt, text, c, delegate(ClaudeJob done)
            {
                try
                {
                    if (!IsDisposed) BeginInvoke(new Action(delegate
                    {
                        if (done != tryJob) return;
                        tryBtn.Text = "Try  >";
                        if (done.Cancelled) return;
                        string output = App.CleanOutput(done.Output, text);
                        string fail = App.FailureMessage(done, c, output);
                        if (fail != null) { tryOut.Text = fail; tryInfo.Text = ""; return; }
                        tryOut.Text = output;
                        tryInfo.Text = "Done in " + (done.ElapsedMs / 1000.0).ToString("0.0") + " s with " + ai + ", " + lang + ".";
                    }));
                }
                catch (InvalidOperationException) { }
            }, out err);
            if (!started) { tryJob = null; tryOut.Text = err; return; }
            tryOut.Text = "";
            tryInfo.Text = "Polishing with \"" + current.Name + "\" in " + lang + " (" + ai + ")...";
            tryBtn.Text = "Stop";
        }

        // ------------------------------------------------------------ Advanced tab
        TabPage BuildAdvanced()
        {
            TabPage page = new TabPage("Advanced");
            TableLayoutPanel t = Grid();

            FlowLayoutPanel cl = new FlowLayoutPanel();
            cl.AutoSize = true;
            cl.WrapContents = false;
            claudeBox = new TextBox();
            claudeBox.Width = 430;
            claudeBox.TextChanged += delegate { MarkDirty(); };
            Button browse = Btn("Browse...", 80);
            Button check = Btn("Check", 70);
            Button login = Btn("Sign in...", 80);
            cl.Controls.AddRange(new Control[] { claudeBox, browse, check, login });
            browse.Click += delegate
            {
                using (OpenFileDialog d = new OpenFileDialog())
                {
                    d.Filter = "claude.exe|claude.exe|All files|*.*";
                    if (d.ShowDialog(this) == DialogResult.OK) claudeBox.Text = d.FileName;
                }
            };
            check.Click += delegate { CheckClaude(); };
            login.Click += delegate
            {
                string exe = claudeBox.Text.Trim();
                if (!File.Exists(exe)) { claudeStatus.Text = "claude.exe not found at that path."; return; }
                Process.Start("powershell.exe", "-NoProfile -NoExit -Command \"& '" + exe.Replace("'", "''") + "' auth login\"");
                claudeStatus.Text = "Finish the sign-in in the browser / console window, then click Check.";
            };
            Row(t, "Claude Code CLI", cl, "Only used when the AI provider is Claude. Leave as found by Setup. If you update or reinstall Claude, " + App.AppName + " finds claude.exe again automatically.");
            claudeStatus = Hint("");
            claudeStatus.ForeColor = Theme.Navy;
            t.RowCount++;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Controls.Add(claudeStatus, 1, t.RowCount - 1);

            copyWaitBox = new NumericUpDown(); copyWaitBox.Minimum = 200; copyWaitBox.Maximum = 5000; copyWaitBox.Increment = 100; copyWaitBox.Width = 90;
            copyWaitBox.ValueChanged += delegate { MarkDirty(); };
            Row(t, "Copy wait (ms)", copyWaitBox, "How long to wait for the app to copy your selection. Raise it (e.g. 2000) if you often see \"Nothing selected\" in a slow app.");

            pasteDelayBox = new NumericUpDown(); pasteDelayBox.Minimum = 200; pasteDelayBox.Maximum = 5000; pasteDelayBox.Increment = 100; pasteDelayBox.Width = 90;
            pasteDelayBox.ValueChanged += delegate { MarkDirty(); };
            Row(t, "Replace delay (ms)", pasteDelayBox, "Time before your old clipboard is put back after Replace. Raise it if Replace sometimes pastes old text.");

            fontBox = new NumericUpDown(); fontBox.Minimum = 8; fontBox.Maximum = 20; fontBox.Width = 90;
            fontBox.ValueChanged += delegate { MarkDirty(); };
            Row(t, "Popup font size", fontBox, "Text size in the result popup.");

            FlowLayoutPanel rules = new FlowLayoutPanel();
            rules.AutoSize = true;
            Button editRules = Btn("Edit core rules", 140), restoreRules = Btn("Restore default core rules", 200);
            rules.Controls.AddRange(new Control[] { editRules, restoreRules });
            editRules.Click += delegate { App.OpenFile(app.RulesFile); };
            restoreRules.Click += delegate
            {
                if (MessageBox.Show(this, "Replace prompt\\rules.md with the default? Your current file is kept as rules.md.bak.", App.AppName, MessageBoxButtons.OKCancel) != DialogResult.OK) return;
                try { app.RestoreDefaultRules(); saveStatus.Text = "Default core rules restored."; }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, App.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            Row(t, "Core rules", rules, "Rules every style follows: return only the polished text, never answer the text, keep meaning, never invent facts, avoid AI-sounding phrases. Rarely needs changes.");

            FlowLayoutPanel folders = new FlowLayoutPanel();
            folders.AutoSize = true;
            Button openSettings = Btn("Open settings folder", 160), openLog = Btn("Open error log folder", 160);
            folders.Controls.AddRange(new Control[] { openSettings, openLog });
            openSettings.Click += delegate { App.OpenFile(app.appDir); };
            openLog.Click += delegate { App.OpenFile(Log.Dir); };
            Row(t, "Folders", folders, "Styles are plain .md files in the \"profiles\" folder: copy them to share a style with colleagues. The log contains errors only, never your text.");

            Row(t, "Version", Caption(App.AppName + " " + App.Version), "");

            page.Controls.Add(Scroller(t));
            return page;
        }

        void CheckClaude()
        {
            string exe = claudeBox.Text.Trim();
            if (!File.Exists(exe)) { claudeStatus.Text = "Not found: " + exe; return; }
            claudeStatus.Text = "Checking...";
            ThreadPool.QueueUserWorkItem(delegate
            {
                string result;
                try
                {
                    ProcessStartInfo psi = exe.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
                        ? new ProcessStartInfo("node.exe", App.QuoteArg(exe) + " auth status")
                        : new ProcessStartInfo(exe, "auth status");
                    psi.UseShellExecute = false; psi.CreateNoWindow = true;
                    psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
                    psi.StandardOutputEncoding = new UTF8Encoding(false);
                    List<string> drop = new List<string>();
                    foreach (string k in psi.EnvironmentVariables.Keys) if (k.ToUpperInvariant().StartsWith("CLAUDE") && k.ToUpperInvariant() != "CLAUDE_CODE_OAUTH_TOKEN") drop.Add(k);
                    foreach (string k in drop) psi.EnvironmentVariables.Remove(k);
                    Process p = Process.Start(psi);
                    string o = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(15000);
                    if (Regex.IsMatch(o, "\"loggedIn\"\\s*:\\s*true"))
                    {
                        Match plan = Regex.Match(o, "\"subscriptionType\"\\s*:\\s*\"([^\"]*)\"");
                        result = "OK - signed in" + (plan.Success ? " (" + plan.Groups[1].Value + " plan)" : "") + ".";
                    }
                    else result = "Not signed in. Click \"Sign in...\".";
                }
                catch (Exception ex) { result = "Could not run claude: " + ex.Message; }
                try { BeginInvoke(new Action(delegate { claudeStatus.Text = result; })); } catch { }
            });
        }

        // ------------------------------------------------------------ load / save
        void LoadProfiles()
        {
            profiles = ProfileStore.Load(Path.Combine(app.appDir, "profiles"));
            originalNames.Clear();
            foreach (Profile p in profiles) originalNames.Add(p.Name);
        }

        void LoadAll()
        {
            loading = true;
            hotkeyBox.Text = cfg.Hotkey;
            modelByProvider.Clear(); typedKeys.Clear(); clearedKeys.Clear();
            foreach (ProviderInfo pi in Providers.All) modelByProvider[pi.Id] = cfg.GetModel(pi.Id);
            baseUrlBox.Text = cfg.CustomBaseUrl;
            shownProvider = null;
            providerBox.SelectedIndex = Array.IndexOf(Providers.All, Providers.Get(cfg.Provider));
            ShowProvider();
            effortBox.SelectedIndex = Math.Max(0, effortBox.Items.IndexOf(string.IsNullOrEmpty(cfg.Effort) ? "(default)" : cfg.Effort));
            timeoutBox.Value = Math.Max(timeoutBox.Minimum, Math.Min(timeoutBox.Maximum, cfg.TimeoutSeconds));
            byAppBox.Checked = cfg.AutoProfileByApp;
            startupBox.Checked = app.StartWithWindows;
            langs = new List<string>(cfg.Languages);
            RefreshLanguages(0);
            claudeBox.Text = cfg.ClaudePath;
            copyWaitBox.Value = Math.Max(copyWaitBox.Minimum, Math.Min(copyWaitBox.Maximum, cfg.CopyWaitMs));
            pasteDelayBox.Value = Math.Max(pasteDelayBox.Minimum, Math.Min(pasteDelayBox.Maximum, cfg.PasteRestoreDelayMs));
            fontBox.Value = Math.Max(fontBox.Minimum, Math.Min(fontBox.Maximum, (decimal)cfg.FontSize));
            LoadProfiles();
            defaultStyleBox.Items.Clear();
            foreach (Profile p in profiles) defaultStyleBox.Items.Add(p.Name);
            SelectDefaultStyle(cfg.DefaultProfile);
            RefreshStyleLists(profiles.Count > 0 ? profiles[0].Name : "");
            loading = false;
            dirty = false;
        }

        Config BuildConfigFromUi()
        {
            Config c = cfg.Clone();
            c.Hotkey = hotkeyBox.Text.Trim();
            StashProvider();
            c.Provider = CurrentProvider.Id;
            foreach (ProviderInfo pi in Providers.All)
            {
                string m;
                if (!modelByProvider.TryGetValue(pi.Id, out m)) continue;
                c.SetModel(pi.Id, m.Length > 0 || pi.Id == Providers.Custom ? m : pi.DefaultModel);
            }
            c.CustomBaseUrl = baseUrlBox.Text.Trim();
            foreach (string id in clearedKeys) c.SetKey(id, "");
            foreach (KeyValuePair<string, string> k in typedKeys) c.SetKey(k.Key, k.Value);
            c.Effort = effortBox.SelectedIndex <= 0 ? "" : (string)effortBox.SelectedItem;
            c.TimeoutSeconds = (int)timeoutBox.Value;
            c.DefaultProfile = defaultStyleBox.SelectedItem as string ?? "Auto";
            c.AutoProfileByApp = byAppBox.Checked;
            c.Languages = Config.CleanLanguages(langs);
            c.ClaudePath = claudeBox.Text.Trim();
            c.CopyWaitMs = (int)copyWaitBox.Value;
            c.PasteRestoreDelayMs = (int)pasteDelayBox.Value;
            c.FontSize = (float)fontBox.Value;
            return c;
        }

        bool Save()
        {
            Config c = BuildConfigFromUi();
            uint mods, vk;
            if (!App.ParseHotkey(c.Hotkey, out mods, out vk)) { Fail("Set a hotkey on the General tab."); return false; }
            if (!app.CanRegister(c.Hotkey)) { Fail(c.Hotkey + " is used by another app. Pick another hotkey."); return false; }
            if (profiles.Count == 0) { Fail("Keep at least one style."); return false; }
            if (Providers.Get(c.Provider).IsApi)
            {
                string problem = ProviderProblem(c);
                if (problem != null) { tabs.SelectedTab = providerPage; Fail(problem.Replace(" Open " + App.AppName + " Settings > AI provider.", "")); return false; }
            }
            List<string> seen = new List<string>();
            foreach (Profile p in profiles)
            {
                if (!ProfileStore.IsValidName(p.Name)) { Fail("Style name \"" + p.Name + "\" is not valid (no \\ / : * ? \" < > |, max 40 characters)."); SelectStyle(p); return false; }
                if (seen.Contains(p.Name.ToLowerInvariant())) { Fail("Two styles are named \"" + p.Name + "\"."); SelectStyle(p); return false; }
                seen.Add(p.Name.ToLowerInvariant());
            }
            string dir = Path.Combine(app.appDir, "profiles");
            try
            {
                for (int i = 0; i < profiles.Count; i++)
                {
                    profiles[i].Order = i + 1;
                    if (Unchanged(dir, profiles[i])) continue;   // keep untouched files byte-identical (upgrades refresh them)
                    ProfileStore.Save(dir, profiles[i]);
                }
                foreach (string old in originalNames)
                    if (!seen.Contains(old.ToLowerInvariant())) ProfileStore.Delete(dir, old);
                originalNames.Clear();
                foreach (Profile p in profiles) originalNames.Add(p.Name);

                if (ProfileStore.Find(profiles, c.DefaultProfile) == null) c.DefaultProfile = profiles[0].Name;
                if (startupBox.Checked != app.StartWithWindows) app.SetStartWithWindows(startupBox.Checked);
                app.ApplySettings(c);
                // Keys are saved (encrypted) now: forget the typed text and show "A key is saved".
                cfg.ApiKeys = new Dictionary<string, string>(c.ApiKeys);
                typedKeys.Clear(); clearedKeys.Clear();
                bool was = loading; loading = true; keyBox.Text = ""; loading = was;
                UpdateKeyStatus();
            }
            catch (Exception ex)
            {
                Log.Error("Save settings failed: " + ex.Message);
                Fail("Could not save: " + ex.Message);
                return false;
            }
            dirty = false;
            saveStatus.ForeColor = Theme.Success;
            saveStatus.Text = "Saved. Active now.";
            return true;
        }

        static bool Unchanged(string dir, Profile p)
        {
            string file = Path.Combine(dir, p.Name + ".md");
            if (!File.Exists(file)) return false;
            try
            {
                Profile disk = Profile.Parse(p.Name, File.ReadAllText(file, Encoding.UTF8));
                return Norm(disk.Serialize()) == Norm(p.Serialize());
            }
            catch { return false; }
        }

        static string Norm(string s) { return Regex.Replace(s.Replace("\r\n", "\n"), "[ \t]+\n", "\n").Trim(); }

        void SelectStyle(Profile p)
        {
            int i = profiles.IndexOf(p);
            if (i >= 0) styleList.SelectedIndex = i;
        }

        void Fail(string msg)
        {
            saveStatus.ForeColor = Theme.Error;
            saveStatus.Text = msg;
        }

        void OnClosingAsk(object sender, FormClosingEventArgs e)
        {
            if (tryJob != null && !tryJob.Done) tryJob.Cancel();
            if (testJob != null && !testJob.Done) testJob.Cancel();
            app.ResumeHotkey();
            if (!dirty) return;
            DialogResult r = MessageBox.Show(this, "Save your changes?", App.AppName + " Settings", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (r == DialogResult.Cancel) { e.Cancel = true; return; }
            if (r == DialogResult.Yes && !Save()) e.Cancel = true;
        }
    }
}
