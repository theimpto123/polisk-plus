// Style profiles: one Markdown file per situation (profiles\<Name>.md) with a Tone and an Output structure section.
// C# 5 / .NET Framework 4.x syntax only (compiled at runtime by Add-Type).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PolishTool
{
    internal class Profile
    {
        public string Name = "";
        public string Description = "";
        public List<string> Apps = new List<string>();
        public int Order = 100;
        public string Tone = "";
        public string Structure = "";
        public string Notes = "";   // leading <!-- --> block, kept when saving

        static readonly Regex Comment = new Regex("<!--[\\s\\S]*?-->");

        public static Profile Parse(string name, string text)
        {
            Profile p = new Profile();
            p.Name = name;
            text = (text ?? "").Replace("\r\n", "\n");
            Match lead = Regex.Match(text, "^\\s*(<!--[\\s\\S]*?-->)");
            if (lead.Success) { p.Notes = lead.Groups[1].Value; text = text.Substring(lead.Length); }

            string[] lines = text.Split('\n');
            int i = 0;
            // Metadata: leading "key: value" lines (blank lines allowed); anything else starts the body.
            for (; i < lines.Length; i++)
            {
                string l = lines[i].Trim();
                if (l.Length == 0) continue;
                Match m = Regex.Match(l, "^(description|apps|order)\\s*:\\s*(.*)$", RegexOptions.IgnoreCase);
                if (!m.Success) break;
                string key = m.Groups[1].Value.ToLowerInvariant(), val = m.Groups[2].Value.Trim();
                if (key == "description") p.Description = val;
                else if (key == "order") int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out p.Order);
                else p.Apps = SplitApps(val);
            }
            // Body: "# Tone" and "# Output structure" sections. Text before any heading counts as tone.
            StringBuilder tone = new StringBuilder(), structure = new StringBuilder();
            StringBuilder cur = tone;
            for (; i < lines.Length; i++)
            {
                string t = lines[i].Trim().ToLowerInvariant();
                if (t == "# tone") { cur = tone; continue; }
                if (t == "# output structure" || t == "# structure") { cur = structure; continue; }
                cur.Append(lines[i]).Append('\n');
            }
            p.Tone = tone.ToString().Trim();
            p.Structure = structure.ToString().Trim();
            return p;
        }

        public static List<string> SplitApps(string s)
        {
            List<string> r = new List<string>();
            foreach (string a in (s ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                if (a.Trim().Length > 0) r.Add(a.Trim());
            return r;
        }

        public string Serialize()
        {
            StringBuilder sb = new StringBuilder();
            if (Notes.Length > 0) sb.Append(Notes).Append("\n");
            sb.Append("description: ").Append(Description.Replace("\n", " ")).Append("\n");
            sb.Append("apps: ").Append(string.Join(", ", Apps.ToArray())).Append("\n");
            sb.Append("order: ").Append(Order.ToString(CultureInfo.InvariantCulture)).Append("\n\n");
            sb.Append("# Tone\n\n").Append(Tone.Trim()).Append("\n\n");
            sb.Append("# Output structure\n\n").Append(Structure.Trim()).Append("\n");
            return sb.ToString().Replace("\n", "\r\n");
        }

        // The profile part of the system prompt (reader notes stripped).
        public string PromptText()
        {
            string tone = Comment.Replace(Tone, "").Trim();
            string structure = Comment.Replace(Structure, "").Trim();
            StringBuilder sb = new StringBuilder();
            if (tone.Length > 0) sb.Append("# Tone\n\n").Append(tone);
            if (structure.Length > 0) { if (sb.Length > 0) sb.Append("\n\n"); sb.Append("# Output structure\n\n").Append(structure); }
            return sb.ToString();
        }

        // "outlook" matches the process name; "title:Gmail" matches text in the window title.
        public bool MatchesApp(string process, string title)
        {
            foreach (string a in Apps)
            {
                string rule = a.Trim();
                if (rule.StartsWith("title:", StringComparison.OrdinalIgnoreCase))
                {
                    string part = rule.Substring(6).Trim();
                    if (part.Length > 0 && (title ?? "").IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }
                else
                {
                    if (rule.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) rule = rule.Substring(0, rule.Length - 4);
                    if (string.Equals(rule, process, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            return false;
        }

        public Profile Clone()
        {
            Profile p = (Profile)MemberwiseClone();
            p.Apps = new List<string>(Apps);
            return p;
        }
    }

    internal static class ProfileStore
    {
        public static List<Profile> Load(string dir)
        {
            List<Profile> list = new List<Profile>();
            if (!Directory.Exists(dir)) return list;
            foreach (string f in Directory.GetFiles(dir, "*.md"))
            {
                try { list.Add(Profile.Parse(Path.GetFileNameWithoutExtension(f), File.ReadAllText(f, Encoding.UTF8))); }
                catch (Exception ex) { Log.Error("Cannot read style " + Path.GetFileName(f) + ": " + ex.Message); }
            }
            list.Sort(delegate(Profile a, Profile b)
            {
                int c = a.Order.CompareTo(b.Order);
                return c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }

        public static Profile Find(List<Profile> list, string name)
        {
            foreach (Profile p in list) if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }

        public static void Save(string dir, Profile p)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, p.Name + ".md"), p.Serialize(), new UTF8Encoding(false));
        }

        public static void Delete(string dir, string name)
        {
            string f = Path.Combine(dir, name + ".md");
            if (File.Exists(f)) File.Delete(f);
        }

        public static bool IsValidName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Trim().Length == 0 || name.Length > 40) return false;
            return name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && name.Trim() == name;
        }

        // Which style to use for text copied from this window.
        public static Profile Pick(List<Profile> list, string defaultName, bool byApp, string process, string title, out string reason)
        {
            reason = "";
            if (byApp)
                foreach (Profile p in list)
                    if (p.MatchesApp(process, title)) { reason = "auto: " + process; return p; }
            Profile d = Find(list, defaultName);
            if (d != null) { reason = "default"; return d; }
            if (list.Count > 0) { reason = "default"; return list[0]; }
            return null;
        }
    }
}
