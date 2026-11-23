using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Networking;
using ZunuConsole.Core;

namespace ZunuConsole.Cloud
{
    // Cloud stores ONLY optional appearance preferences and favorite command names.
    // All Neon/PG credentials stay on the server. Never commit or print access tokens.
    [Serializable]
    public sealed class CloudSetting
    {
        public string key;
        public string value;
    }

    [Serializable]
    public sealed class CloudProfile
    {
        public CloudSetting[] settings = new CloudSetting[0];
        public string[] favorites = new string[0];
    }

    internal sealed class ZunuCloudClient
    {
        private static readonly HashSet<string> SyncedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ui.theme", "ui.accent", "ui.opacity", "ui.rounding", "ui.fontSize",
            "ui.logFontSize", "ui.density", "ui.accentR", "ui.accentG", "ui.accentB"
        };

        private readonly ZunuPlugin plugin;
        private readonly ConfigEntry<bool> enabled;
        private readonly ConfigEntry<string> endpoint;
        private readonly ConfigEntry<string> accessToken;
        private readonly ConfigEntry<int> interval;
        private bool busy;
        private bool initialPullCompleted;
        private float nextSync;
        public string Status { get; private set; }
        public string LastSync { get; private set; }
        public bool Busy { get { return busy; } }
        public bool Enabled { get { return enabled.Value; } }
        public bool Configured { get { return enabled.Value && ValidEndpoint(endpoint.Value) && !string.IsNullOrWhiteSpace(accessToken.Value); } }
        public string Endpoint { get { return ValidEndpoint(endpoint.Value) ? endpoint.Value : "Not configured"; } }

        public ZunuCloudClient(ZunuPlugin owner)
        {
            plugin = owner;
            enabled = owner.Config.Bind("Neon Cloud", "Enabled", false, "Opt-in. Sync appearance and favorite command names to a Neon-backed HTTPS API.");
            endpoint = owner.Config.Bind("Neon Cloud", "ApiBaseUrl", "", "HTTPS URL for YOUR deployed ZunuConsole API. Never enter a postgres connection string.");
            accessToken = owner.Config.Bind("Neon Cloud", "AccessToken", "", "Private per-user access token. Keep the .cfg file private; never share or commit it.");
            interval = owner.Config.Bind("Neon Cloud", "IntervalSeconds", 120, "Automatic push frequency, minimum 60 seconds.");
            Status = "LOCAL ONLY";
            LastSync = "Never";
        }

        // URL/token entered in the in-game Cloud panel. Only the per-user token is saved
        // to the local BepInEx config; the server never sends or stores the raw token.
        public bool Configure(string apiUrl, string token, out string reason)
        {
            reason = null;
            apiUrl = (apiUrl ?? "").Trim().TrimEnd('/');
            token = (token ?? "").Trim();
            if (!ValidEndpoint(apiUrl) || apiUrl.EndsWith("/api/state", StringComparison.OrdinalIgnoreCase))
            {
                reason = "Enter the HTTPS base URL of the deployed ZunuConsole API.";
                return false;
            }
            if (token.Length < 32 || token.Length > 256 || !token.All(ch =>
                (ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z') ||
                (ch >= '0' && ch <= '9') || ch == '-' || ch == '_'))
            {
                reason = "Enter the 32-256 character private access token.";
                return false;
            }
            endpoint.Value = apiUrl;
            accessToken.Value = token;
            enabled.Value = true;
            initialPullCompleted = false;
            nextSync = 0f;
            Status = "READY TO CONNECT";
            return true;
        }

        public void Disconnect()
        {
            enabled.Value = false;
            accessToken.Value = "";
            initialPullCompleted = false;
            nextSync = 0f;
            Status = "LOCAL ONLY";
            LastSync = "Never";
        }

        private static bool ValidEndpoint(string candidate)
        {
            Uri uri;
            return !string.IsNullOrWhiteSpace(candidate)
                && Uri.TryCreate(candidate, UriKind.Absolute, out uri)
                && uri.Scheme == Uri.UriSchemeHttps
                && !string.IsNullOrWhiteSpace(uri.Host)
                && string.IsNullOrWhiteSpace(uri.UserInfo)
                && string.IsNullOrEmpty(uri.Query)
                && string.IsNullOrEmpty(uri.Fragment);
        }

        public void Tick()
        {
            if (!Configured)
            {
                if (!busy) Status = enabled.Value ? "SETUP REQUIRED" : "LOCAL ONLY";
                return;
            }
            if (busy || Time.realtimeSinceStartup < nextSync) return;
            if (!initialPullCompleted) Pull();
            else Push();
        }

        public void Pull()
        {
            if (!Configured || busy) { if (!Configured) Status = "SETUP REQUIRED"; return; }
            plugin.StartCoroutine(Request(false));
        }

        public void Push()
        {
            if (!Configured || busy) { if (!Configured) Status = "SETUP REQUIRED"; return; }
            plugin.StartCoroutine(Request(true));
        }

        private CloudProfile Snapshot()
        {
            var settings = plugin.Settings.All.Where(x => SyncedKeys.Contains(x.Key))
                .Select(x => new CloudSetting { key = x.Key, value = plugin.Settings.Get(x.Key) }).ToArray();
            var favorites = plugin.Tools.Favorites.OrderBy(x => x).Take(250).ToArray();
            return new CloudProfile { settings = settings, favorites = favorites };
        }

        private void Apply(CloudProfile profile)
        {
            if (profile == null) return;
            if (profile.settings != null)
                foreach (var item in profile.settings.Take(50))
                {
                    if (item == null || !SyncedKeys.Contains(item.key)) continue;
                    string ignored;
                    plugin.Settings.Set(item.key, item.value, out ignored);
                }
            if (profile.favorites != null)
            {
                plugin.Tools.Favorites.Clear();
                foreach (var favorite in profile.favorites.Take(250))
                    if (!string.IsNullOrWhiteSpace(favorite) && favorite.Length <= 80 && plugin.Engine.Find(favorite) != null)
                        plugin.Tools.Favorites.Add(favorite);
                plugin.Tools.Save();
            }
        }

        private IEnumerator Request(bool push)
        {
            busy = true;
            Status = push ? "SAVING" : "LOADING";
            nextSync = Time.realtimeSinceStartup + Mathf.Max(60, interval.Value);
            string url = endpoint.Value.TrimEnd('/') + "/api/state";
            UnityWebRequest request = null;
            try
            {
                if (push)
                {
                    byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(Snapshot()));
                    request = new UnityWebRequest(url, "PUT");
                    request.uploadHandler = new UploadHandlerRaw(body);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                else request = UnityWebRequest.Get(url);
                request.SetRequestHeader("Authorization", "Bearer " + accessToken.Value);
                request.timeout = 12;
            }
            catch (Exception ex)
            {
                // Never log authorization headers, full URL queries, or tokens.
                Status = "CONFIGURATION ERROR";
                plugin.Log.Write("Cloud setup failed: " + ex.GetType().Name, LogSeverity.Warning);
                if (request != null) request.Dispose();
                busy = false;
                yield break;
            }

            yield return request.SendWebRequest();
            long code = request.responseCode;
            bool ok = request.result == UnityWebRequest.Result.Success;
            if (!push && code == 404)
            {
                initialPullCompleted = true;
                Status = "NEW PROFILE";
                busy = false;
                request.Dispose();
                Push();
                yield break;
            }
            if (!ok)
            {
                Status = code == 401 || code == 403 ? "UNAUTHORIZED" : "SYNC ERROR " + code;
                // Avoid logging server response bodies, which might contain sensitive data.
                plugin.Log.Write("Cloud request failed (HTTP " + code + ")", LogSeverity.Warning);
                request.Dispose();
                busy = false;
                yield break;
            }
            if (!push)
            {
                try
                {
                    CloudProfile profile = JsonUtility.FromJson<CloudProfile>(request.downloadHandler.text);
                    Apply(profile);
                    initialPullCompleted = true;
                }
                catch (Exception) { Status = "INVALID PROFILE"; request.Dispose(); busy = false; yield break; }
            }
            else initialPullCompleted = true;
            Status = "SYNCED";
            LastSync = DateTime.Now.ToString("HH:mm:ss");
            request.Dispose();
            busy = false;
        }
    }
}
