// PolisK+ AI providers: the Claude Code CLI (default) or an HTTP API
// (OpenAI, Google Gemini, or any service with an OpenAI-compatible endpoint).
// C# 5 / .NET Framework 4.x syntax only (compiled at runtime).
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace PolishTool
{
    internal class ProviderInfo
    {
        public string Id, Name, Short, Needs, KeyUrl = "", BaseUrl = "", DefaultModel = "";
        public bool IsApi;
        public string[] KeyEnv = new string[0];
        public string[] Models = new string[0];
    }

    internal static class Providers
    {
        public const string Claude = "claude", OpenAI = "openai", Gemini = "gemini", Custom = "custom";
        public static readonly ProviderInfo[] All = Build();

        static ProviderInfo[] Build()
        {
            ProviderInfo c = new ProviderInfo();
            c.Id = Claude; c.Short = "Claude"; c.Name = "Claude  (Claude Code CLI, your Claude plan, no API key)";
            c.Needs = "Uses the Claude Code CLI signed in with your paid Claude plan (Pro, Max, Team or Enterprise). No API key. Sign-in and the CLI path are on the Advanced tab.";
            c.DefaultModel = "sonnet"; c.Models = new string[] { "sonnet", "haiku", "opus" };

            ProviderInfo o = new ProviderInfo();
            o.Id = OpenAI; o.Short = "OpenAI"; o.Name = "OpenAI GPT  (API key)"; o.IsApi = true;
            o.Needs = "Needs an OpenAI API key with billing set up. A ChatGPT Plus subscription alone does not include API access.";
            o.KeyUrl = "https://platform.openai.com/api-keys"; o.BaseUrl = "https://api.openai.com/v1";
            o.DefaultModel = "gpt-5.4-mini"; o.Models = new string[] { "gpt-5.4-mini" };
            o.KeyEnv = new string[] { "OPENAI_API_KEY" };

            ProviderInfo g = new ProviderInfo();
            g.Id = Gemini; g.Short = "Gemini"; g.Name = "Google Gemini  (API key)"; g.IsApi = true;
            g.Needs = "Needs a Gemini API key from Google AI Studio. Read the data terms of the tier you use: free tiers may use your text to improve the service.";
            g.KeyUrl = "https://aistudio.google.com/apikey"; g.BaseUrl = "https://generativelanguage.googleapis.com/v1beta";
            g.DefaultModel = "gemini-2.5-flash"; g.Models = new string[] { "gemini-2.5-flash", "gemini-2.5-flash-lite", "gemini-2.5-pro" };
            g.KeyEnv = new string[] { "GEMINI_API_KEY", "GOOGLE_API_KEY" };

            ProviderInfo x = new ProviderInfo();
            x.Id = Custom; x.Short = "Custom API"; x.Name = "Other  (any OpenAI-compatible API)"; x.IsApi = true;
            x.Needs = "Any service with an OpenAI-compatible /chat/completions endpoint: OpenRouter, Groq, DeepSeek, Mistral, or a local Ollama / LM Studio. Enter its base URL, your key and a model name.";

            return new ProviderInfo[] { c, o, g, x };
        }

        // Unknown ids fall back to Claude, the default.
        public static ProviderInfo Get(string id)
        {
            foreach (ProviderInfo p in All) if (string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)) return p;
            return All[0];
        }
    }

    // API keys are stored encrypted with Windows DPAPI: only this Windows user on this computer can read them.
    internal static class Secrets
    {
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PolishTool.ApiKey.v1");

        public static string Protect(string plain)
        {
            return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser));
        }

        // Returns "" when the value cannot be read (copied from another computer or user, or damaged).
        public static string Unprotect(string stored)
        {
            if (string.IsNullOrEmpty(stored)) return "";
            try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(stored), Entropy, DataProtectionScope.CurrentUser)); }
            catch { return ""; }
        }

        // Keys are printable ASCII without spaces; this also keeps them safe to put in an HTTP header.
        public static bool LooksLikeKey(string s)
        {
            return s != null && Regex.IsMatch(s, "^[\\x21-\\x7E]{8,400}$");
        }
    }

    internal class HttpSpec
    {
        public string Url, Body, Kind;
        public ProviderInfo Provider;
        public string Model = "";
        public Dictionary<string, string> Headers = new Dictionary<string, string>();
    }

    internal static class Http
    {
        static bool ready;

        // Returns the HTTP status and the response body. Throws WebException when there is no HTTP response (network, timeout, abort).
        public static int Send(HttpSpec s, int timeoutMs, Action<HttpWebRequest> created, out string body)
        {
            if (!ready)
            {
                try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { } // TLS 1.2
                ServicePointManager.Expect100Continue = false;
                ready = true;
            }
            HttpWebRequest r = (HttpWebRequest)WebRequest.Create(s.Url);
            r.Method = s.Body == null ? "GET" : "POST";
            r.Timeout = timeoutMs;
            r.ReadWriteTimeout = timeoutMs;
            r.UserAgent = "PolisK+/" + App.Version;
            r.Accept = "application/json";
            r.AllowAutoRedirect = false;   // never forward the key to another host
            if (Api.IsLoopback(s.Url)) r.Proxy = null;
            else
            {
                try { IWebProxy px = WebRequest.GetSystemWebProxy(); px.Credentials = CredentialCache.DefaultCredentials; r.Proxy = px; } catch { }
            }
            foreach (KeyValuePair<string, string> h in s.Headers) r.Headers[h.Key] = h.Value;
            if (created != null) created(r);
            HttpWebResponse resp;
            try
            {
                if (s.Body != null)
                {
                    byte[] b = new UTF8Encoding(false).GetBytes(s.Body);
                    r.ContentType = "application/json";
                    r.ContentLength = b.Length;
                    using (Stream st = r.GetRequestStream()) st.Write(b, 0, b.Length);
                }
                resp = (HttpWebResponse)r.GetResponse();
            }
            catch (WebException ex)
            {
                resp = ex.Response as HttpWebResponse;
                if (resp == null) throw;
            }
            using (resp)
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                body = sr.ReadToEnd();
                return (int)resp.StatusCode;
            }
        }
    }

    internal static class Api
    {
        static JavaScriptSerializer Json()
        {
            JavaScriptSerializer js = new JavaScriptSerializer();
            js.MaxJsonLength = int.MaxValue;
            return js;
        }

        public static bool IsLoopback(string url)
        {
            Uri u;
            return Uri.TryCreate(url, UriKind.Absolute, out u) && u.IsLoopback;
        }

        // https only, except http://localhost for a model running on this computer.
        public static string CheckBaseUrl(string url)
        {
            Uri u;
            if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out u)) return "Enter the service's base URL, for example https://openrouter.ai/api/v1";
            if (u.Scheme == "https") return null;
            if (u.Scheme == "http" && u.IsLoopback) return null;
            return "The base URL must start with https:// (http:// is only allowed for localhost), so your key and text are encrypted on the way.";
        }

        static string BaseUrl(Config c, ProviderInfo p, out string err)
        {
            err = null;
            if (p.Id != Providers.Custom) return p.BaseUrl;
            string url = (c.CustomBaseUrl ?? "").Trim().TrimEnd('/');
            if (url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)) url = url.Substring(0, url.Length - "/chat/completions".Length);
            err = CheckBaseUrl(url);
            if (err != null) { err += "\r\n\r\nOpen " + App.AppName + " Settings > AI provider."; return null; }
            return url;
        }

        static bool Auth(Config c, ProviderInfo p, string baseUrl, HttpSpec s, out string err)
        {
            err = null;
            string key = c.GetKey(p.Id);
            if (key.Length == 0)
            {
                if (p.Id == Providers.Custom && IsLoopback(baseUrl)) return true;   // local models need no key
                err = "No API key saved for " + p.Short + ".\r\n\r\nOpen " + App.AppName + " Settings > AI provider, paste your key, then Save.";
                return false;
            }
            if (!Secrets.LooksLikeKey(key)) { err = "The saved " + p.Short + " API key looks wrong (spaces or line breaks in it). Paste it again in " + App.AppName + " Settings > AI provider."; return false; }
            if (p.Id == Providers.Gemini) s.Headers["x-goog-api-key"] = key;
            else s.Headers["Authorization"] = "Bearer " + key;
            return true;
        }

        public static HttpSpec BuildPolish(Config c, string system, string input, out string err)
        {
            ProviderInfo p = Providers.Get(c.Provider);
            string model = (c.GetModel(p.Id) ?? "").Trim();
            if (p.Id == Providers.Gemini && model.StartsWith("models/")) model = model.Substring(7);
            if (model.Length == 0) { err = "No model chosen for " + p.Short + ".\r\n\r\nOpen " + App.AppName + " Settings > AI provider and pick one."; return null; }
            if (!Regex.IsMatch(model, "^[\\w.\\-/:@]{1,120}$")) { err = "The model name \"" + model + "\" is not valid. Pick one in " + App.AppName + " Settings > AI provider."; return null; }
            string baseUrl = BaseUrl(c, p, out err);
            if (baseUrl == null) return null;
            HttpSpec s = new HttpSpec();
            s.Provider = p;
            s.Model = model;
            if (!Auth(c, p, baseUrl, s, out err)) return null;

            Dictionary<string, object> body = new Dictionary<string, object>();
            if (p.Id == Providers.Gemini)
            {
                s.Kind = "gemini";
                s.Url = baseUrl + "/models/" + model + ":generateContent";
                body["systemInstruction"] = Parts(system);
                Dictionary<string, object> user = Parts(input);
                user["role"] = "user";
                body["contents"] = new object[] { user };
            }
            else
            {
                s.Kind = "openai";
                s.Url = baseUrl + "/chat/completions";
                body["model"] = model;
                body["messages"] = new object[] { Message("system", system), Message("user", input) };
            }
            s.Body = Json().Serialize(body);
            return s;
        }

        static Dictionary<string, object> Parts(string text)
        {
            Dictionary<string, object> part = new Dictionary<string, object>();
            part["text"] = text;
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["parts"] = new object[] { part };
            return d;
        }

        static Dictionary<string, object> Message(string role, string content)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["role"] = role;
            d["content"] = content;
            return d;
        }

        public static HttpSpec BuildListModels(Config c, out string err)
        {
            ProviderInfo p = Providers.Get(c.Provider);
            string baseUrl = BaseUrl(c, p, out err);
            if (baseUrl == null) return null;
            HttpSpec s = new HttpSpec();
            s.Provider = p;
            if (!Auth(c, p, baseUrl, s, out err)) return null;
            s.Kind = p.Id == Providers.Gemini ? "gemini" : "openai";
            s.Url = baseUrl + (p.Id == Providers.Gemini ? "/models?pageSize=200" : "/models");
            return s;
        }

        static object Get(object o, string key)
        {
            Dictionary<string, object> d = o as Dictionary<string, object>;
            object v;
            return d != null && d.TryGetValue(key, out v) ? v : null;
        }

        static object First(object o)
        {
            IList l = o as IList;
            return l != null && l.Count > 0 ? l[0] : null;
        }

        // The polished text from a successful response. Throws InvalidOperationException with a readable reason when there is none.
        public static string ParseText(HttpSpec s, string json)
        {
            object root;
            try { root = Json().DeserializeObject(json); }
            catch { throw new InvalidOperationException(s.Provider.Short + " sent an answer " + App.AppName + " could not read."); }
            StringBuilder sb = new StringBuilder();
            if (s.Kind == "gemini")
            {
                object cand = First(Get(root, "candidates"));
                IList parts = Get(Get(cand, "content"), "parts") as IList;
                if (parts != null)
                    foreach (object part in parts)
                    {
                        object thought = Get(part, "thought");
                        if (thought is bool && (bool)thought) continue;
                        string t = Get(part, "text") as string;
                        if (t != null) sb.Append(t);
                    }
                if (sb.Length == 0)
                {
                    string why = Get(Get(root, "promptFeedback"), "blockReason") as string ?? Get(cand, "finishReason") as string;
                    throw new InvalidOperationException("Gemini returned no text" + (why != null ? " (" + why + ")." : "."));
                }
            }
            else
            {
                object msg = Get(First(Get(root, "choices")), "message");
                object content = Get(msg, "content");
                if (content is string) sb.Append((string)content);
                else if (content is IList)
                    foreach (object part in (IList)content) { string t = Get(part, "text") as string; if (t != null) sb.Append(t); }
                if (sb.Length == 0)
                {
                    string refusal = Get(msg, "refusal") as string;
                    throw new InvalidOperationException(s.Provider.Short + " returned no text" + (refusal != null ? " (it declined this text)." : "."));
                }
            }
            return sb.ToString();
        }

        // Chat-capable model ids, best guess first. Short on purpose: the box is not meant for scrolling.
        public static List<string> ParseModels(HttpSpec s, string json)
        {
            List<string> ids = new List<string>();
            object root = Json().DeserializeObject(json);
            if (s.Kind == "gemini")
            {
                IList models = Get(root, "models") as IList;
                if (models != null)
                    foreach (object m in models)
                    {
                        string name = Get(m, "name") as string;
                        IList methods = Get(m, "supportedGenerationMethods") as IList;
                        if (name == null || methods == null || !methods.Contains("generateContent")) continue;
                        if (name.StartsWith("models/")) name = name.Substring(7);
                        if (!name.StartsWith("gemini-") || Regex.IsMatch(name, "embedding|tts|image|live|audio|robotics|computer-use|transcribe|-latest$")) continue;
                        ids.Add(name);
                    }
                ids.Sort(delegate(string a, string b) { return string.CompareOrdinal(b, a); });   // newest version first
            }
            else
            {
                List<KeyValuePair<string, long>> rows = new List<KeyValuePair<string, long>>();
                IList data = Get(root, "data") as IList;
                if (data != null)
                    foreach (object m in data)
                    {
                        string id = Get(m, "id") as string;
                        if (id == null) continue;
                        if (s.Provider.Id == Providers.OpenAI &&
                            (!Regex.IsMatch(id, "^(gpt-|o\\d|chatgpt-)") || Regex.IsMatch(id, "embedding|audio|realtime|tts|transcribe|image|whisper|dall-e|moderation|search|instruct|codex")))
                            continue;
                        long created = 0;
                        object cr = Get(m, "created");
                        if (cr != null) long.TryParse(cr.ToString(), out created);
                        rows.Add(new KeyValuePair<string, long>(id, created));
                    }
                if (s.Provider.Id == Providers.OpenAI) rows.Sort(delegate(KeyValuePair<string, long> a, KeyValuePair<string, long> b) { return b.Value.CompareTo(a.Value); });
                else rows.Sort(delegate(KeyValuePair<string, long> a, KeyValuePair<string, long> b) { return string.CompareOrdinal(a.Key, b.Key); });
                foreach (KeyValuePair<string, long> r in rows) ids.Add(r.Key);
            }
            int max = s.Provider.Id == Providers.Custom ? 200 : 15;
            if (ids.Count > max) ids.RemoveRange(max, ids.Count - max);
            return ids;
        }

        // A readable message for a failed request. The provider's own error text is shown to the user but never logged.
        public static string ErrorMessage(HttpSpec s, int status, string body)
        {
            string detail = "";
            try
            {
                object err = Get(Json().DeserializeObject(body), "error");
                detail = (err as string) ?? (Get(err, "message") as string) ?? "";
            }
            catch { }
            detail = Regex.Replace(detail, "\\s+", " ").Trim();
            if (detail.Length > 300) detail = detail.Substring(0, 300) + "...";
            string name = s.Provider.Short, where = App.AppName + " Settings > AI provider";
            string msg;
            if (status == 401 || status == 403 || detail.IndexOf("API key", StringComparison.OrdinalIgnoreCase) >= 0)
                msg = name + " rejected the API key. Check or paste it again in " + where + ".";
            else if (status == 404)
                msg = "The model \"" + s.Model + "\" was not found on your " + name + " account. In " + where + ", click Load models and pick one.";
            else if (status == 429)
                msg = name + " says the rate limit or quota is reached. Wait a moment, or check billing and limits on your " + name + " account.";
            else if (status >= 300 && status < 400)
                msg = "The address redirected somewhere else (HTTP " + status + "). Check the base URL in " + where + ".";
            else if (status >= 500)
                msg = name + " has a problem on its side (HTTP " + status + "). Try again in a moment.";
            else
                msg = name + " refused the request (HTTP " + status + ").";
            return detail.Length > 0 ? msg + "\r\n\r\n" + name + " said: " + detail : msg;
        }

        public static string NetworkMessage(HttpSpec s, WebException ex)
        {
            string host = "";
            try { host = new Uri(s.Url).Host; } catch { }
            return "Could not reach " + s.Provider.Short + " (" + host + "): " + ex.Status + ".\r\n\r\nCheck your internet connection. On a company network, the firewall or proxy may block this address.";
        }
    }
}
