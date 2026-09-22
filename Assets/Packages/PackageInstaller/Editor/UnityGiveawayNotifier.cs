using System;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Raven12345
{
    /// <summary>Checks Unity's public Publisher Sale page without signing in or changing the user's account.</summary>
    [InitializeOnLoad]
    internal static class UnityGiveawayNotifier
    {
        private const string GiveawayPageUrl = "https://assetstore.unity.com/publisher-sale";
        private const string SessionCheckedKey = "Raven.PackageInstaller.UnityGiveaway.SessionChecked";
        private const string LastCheckedTicksKey = "Raven.PackageInstaller.UnityGiveaway.LastCheckedTicks";
        private const string LastGiveawayKey = "Raven.PackageInstaller.UnityGiveaway.LastGiveaway";
        private const double CacheHours = 6d;
        private const int TimeoutSeconds = 15;

        private static UnityWebRequest request;
        private static bool manualCheck;

        static UnityGiveawayNotifier()
        {
            EditorApplication.delayCall += CheckWhenEditorIsReady;
        }

        [MenuItem("Tools/Raven/Check Unity Giveaway")]
        private static void CheckFromMenu()
        {
            BeginCheck(true);
        }

        [MenuItem("Tools/Raven/Open Unity Giveaway Page")]
        private static void OpenGiveawayPage()
        {
            Application.OpenURL(GiveawayPageUrl);
        }

        private static void CheckWhenEditorIsReady()
        {
            if (SessionState.GetBool(SessionCheckedKey, false) || !IsAutomaticCheckEnabled()) return;
            SessionState.SetBool(SessionCheckedKey, true);
            BeginCheck(false);
        }

        private static bool IsAutomaticCheckEnabled()
        {
            var guids = AssetDatabase.FindAssets("t:PersonalPackageCatalog");
            if (guids.Length == 0) return true;
            var catalog = AssetDatabase.LoadAssetAtPath<PersonalPackageCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
            return catalog == null || catalog.autoCheckUnityGiveaway;
        }

        private static void BeginCheck(bool ignoreCache)
        {
            if (request != null)
            {
                if (ignoreCache) Debug.Log("[Raven Package Installer] Unity Giveaway check is already running.");
                return;
            }

            if (!ignoreCache && IsCacheFresh()) return;

            manualCheck = ignoreCache;
            request = UnityWebRequest.Get(GiveawayPageUrl);
            request.timeout = TimeoutSeconds;
            request.SetRequestHeader("Accept", "text/html,application/xhtml+xml");
            request.SendWebRequest();
            EditorApplication.update -= PollRequest;
            EditorApplication.update += PollRequest;
        }

        private static bool IsCacheFresh()
        {
            long ticks;
            return long.TryParse(EditorPrefs.GetString(LastCheckedTicksKey, "0"), out ticks) &&
                   new DateTime(ticks, DateTimeKind.Utc) > DateTime.UtcNow.AddHours(-CacheHours);
        }

        private static void PollRequest()
        {
            if (request == null || !request.isDone) return;

            var completedRequest = request;
            var wasManual = manualCheck;
            request = null;
            manualCheck = false;
            EditorApplication.update -= PollRequest;

            if (completedRequest.result != UnityWebRequest.Result.Success)
            {
                ReportProblem(wasManual, "Could not check Unity Giveaway: " + GetRequestError(completedRequest));
                completedRequest.Dispose();
                return;
            }

            var result = UnityGiveawayParser.Parse(completedRequest.downloadHandler == null ? string.Empty : completedRequest.downloadHandler.text);
            completedRequest.Dispose();
            EditorPrefs.SetString(LastCheckedTicksKey, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));

            if (!result.IsParsed)
            {
                ReportProblem(wasManual, "Could not read giveaway details. Please check the page directly: " + GiveawayPageUrl);
                return;
            }

            var isNewGiveaway = !string.Equals(EditorPrefs.GetString(LastGiveawayKey, string.Empty), result.Identity, StringComparison.Ordinal);
            EditorPrefs.SetString(LastGiveawayKey, result.Identity);
            if (wasManual || isNewGiveaway) LogGiveaway(result);
        }

        private static void ReportProblem(bool wasManual, string text)
        {
            if (wasManual) Debug.LogWarning("[Raven Package Installer] " + text);
            else Debug.LogWarning("[Raven Package Installer] " + text + " The check will retry after the cache period.");
        }

        private static void LogGiveaway(UnityGiveawayInfo giveaway)
        {
            var deadline = string.IsNullOrEmpty(giveaway.Deadline) ? "Cần kiểm tra trên trang" : giveaway.Deadline;
            var status = giveaway.HasUnambiguousFutureDeadline ? "Đang diễn ra" : "Cần kiểm tra trên trang";
            Debug.Log(
                "[Raven Package Installer] Unity Publisher Asset Giveaway\n" +
                "Asset: " + giveaway.AssetName + "\n" +
                "Coupon: " + giveaway.Coupon + "\n" +
                "Hạn nhận: " + deadline + "\n" +
                "Trạng thái: " + status + "\n" +
                "Nhận quà: <a href=\"" + giveaway.ClaimUrl + "\">Mở trang giveaway</a>");
        }

        private static string GetRequestError(UnityWebRequest completedRequest)
        {
            return string.IsNullOrEmpty(completedRequest.error) ? "Unknown network error." : completedRequest.error;
        }
    }

    internal sealed class UnityGiveawayInfo
    {
        public bool IsParsed;
        public string AssetName;
        public string Coupon;
        public string ClaimUrl;
        public string Deadline;
        public string Identity;
        public bool HasUnambiguousFutureDeadline;
    }

    /// <summary>Best-effort parser for public HTML. It deliberately returns unparsed, not "no giveaway", when page structure is unclear.</summary>
    internal static class UnityGiveawayParser
    {
        private const string Marker = "Publisher asset giveaway";

        public static UnityGiveawayInfo Parse(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return new UnityGiveawayInfo();

            var markerIndex = html.IndexOf(Marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0) return new UnityGiveawayInfo();

            var sectionHtml = html.Substring(markerIndex, Math.Min(html.Length - markerIndex, 12000));
            var sectionText = ToReadableText(sectionHtml);
            var asset = Match(sectionText, @"Publisher\s+asset\s+giveaway\s*\r?\n\s*([^\r\n]+)");
            var coupon = Match(sectionText, @"coupon\s+code\s+([A-Z0-9_-]{4,})");
            var deadline = Match(sectionText, @"(?:promotion|giveaway|sale|asset\s+promotion)[^.\r\n]*?end(?:s)?\s+([^\.\r\n]+)");
            var claimUrl = Match(sectionHtml, "<a[^>]+href\\s*=\\s*[\"']([^\"']+)[\"'][^>]*>.*?Get\\s+your\\s+free\\s+gift", RegexOptions.Singleline);

            // The asset name and coupon are the stable identifiers needed to suppress duplicate notifications.
            if (string.IsNullOrWhiteSpace(asset) || string.IsNullOrWhiteSpace(coupon)) return new UnityGiveawayInfo();

            claimUrl = NormalizeClaimUrl(claimUrl);
            var hasFutureDeadline = TryParseFutureDeadline(deadline);
            return new UnityGiveawayInfo
            {
                IsParsed = true,
                AssetName = asset.Trim(),
                Coupon = coupon.Trim(),
                ClaimUrl = claimUrl,
                Deadline = string.IsNullOrWhiteSpace(deadline) ? string.Empty : deadline.Trim(),
                Identity = asset.Trim() + "|" + claimUrl + "|" + coupon.Trim(),
                HasUnambiguousFutureDeadline = hasFutureDeadline
            };
        }

        private static string ToReadableText(string html)
        {
            var withoutNonContent = Regex.Replace(html, @"<(script|style)[^>]*>.*?</\1>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var withBreaks = Regex.Replace(withoutNonContent, @"</?(h[1-6]|p|div|li|br)[^>]*>", "\n", RegexOptions.IgnoreCase);
            var withoutTags = Regex.Replace(withBreaks, @"<[^>]+>", " ");
            return Regex.Replace(WebUtilityDecode(withoutTags), @"[ \t\f\v]+", " ");
        }

        private static string Match(string text, string pattern, RegexOptions options = RegexOptions.IgnoreCase)
        {
            var match = Regex.Match(text, pattern, options);
            return match.Success && match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : string.Empty;
        }

        private static bool TryParseFutureDeadline(string deadline)
        {
            if (string.IsNullOrWhiteSpace(deadline)) return false;
            var normalized = Regex.Replace(deadline, @"\s+(PT|PDT|PST|UTC|GMT)\b", string.Empty, RegexOptions.IgnoreCase).Trim();
            DateTime parsed;
            return DateTime.TryParse(normalized, CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.AllowWhiteSpaces, out parsed) && parsed.ToUniversalTime() > DateTime.UtcNow;
        }

        private static string WebUtilityDecode(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : System.Net.WebUtility.HtmlDecode(value);
        }

        private static string NormalizeClaimUrl(string claimUrl)
        {
            if (string.IsNullOrWhiteSpace(claimUrl)) return "https://assetstore.unity.com/publisher-sale";
            claimUrl = WebUtilityDecode(claimUrl).Trim();
            if (claimUrl.StartsWith("/", StringComparison.Ordinal)) return "https://assetstore.unity.com" + claimUrl;
            return Uri.IsWellFormedUriString(claimUrl, UriKind.Absolute) ? claimUrl : "https://assetstore.unity.com/publisher-sale";
        }
    }
}
