#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using YG;

namespace Vodovorot.Stage2
{
    /// <summary>Explicit Editor UI fixture. Never submits ranks or alters the service/profile.</summary>
    public static class Stage2LeaderboardViewChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] private sealed class Check { public string name; public bool passed; public string detail; }
        [Serializable] private sealed class Geometry
        {
            public float x, y, width, height;
            public Geometry(Rect rect) { x = rect.x; y = rect.y; width = rect.width; height = rect.height; }
        }
        [Serializable] private sealed class ViewReport
        {
            public string scope = "Editor-only UI animation fixture; labeled test rows, no real Yandex rank, network request or persistent reward. Current GameView geometry only; no resize claim.";
            public bool completed, passed, profileUnchanged, serviceSnapshotUnchanged, UiGeometryOwnRowMovementAndViewportFooter;
            public int screenWidth, screenHeight, activeRows;
            public string screenshot;
            public Vector2 ownStart, ownMiddle, ownEnd, ownTarget, lowerStart, lowerMiddle, lowerEnd, lowerTarget;
            public Geometry viewport, shareButton, closeButton;
            public List<Check> checks = new List<Check>();
        }

        public static IEnumerator Run(GamingManager match)
        {
            if (match == null || LeaderboardRoundService.CanUsePlatform)
                throw new InvalidOperationException("Leaderboard UI fixture requires an Editor/offline settled match.");
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Stage2QA"));
            Directory.CreateDirectory(directory);
            var report = new ViewReport { screenWidth = Screen.width, screenHeight = Screen.height,
                screenshot = Path.Combine(directory, "ranking-animation-fixture.png") };
            SavesYG profile = YG2.saves;
            string profileJson = JsonUtility.ToJson(profile);
            LeaderboardRoundSnapshot offline = LeaderboardRoundService.Current;
            string offlineSignature = Signature(offline);
            LeaderboardRoundPanel view = null;
            Text title = null;
            int titleFont = 0;
            try
            {
                if (offline == null || offline.status != LeaderboardRoundStatus.Unavailable || offline.rank != 0 || offline.improved)
                    throw new InvalidOperationException("Start from the actual offline view, without a real or simulated service confirmation.");
                LeaderboardRoundPanel.Show(match.Stage2RoundId, match.IsStage2RankRewardEligible);
                yield return null;
                Canvas.ForceUpdateCanvases();
                view = UnityEngine.Object.FindAnyObjectByType<LeaderboardRoundPanel>();
                if (view == null) throw new InvalidOperationException("Actual leaderboard panel did not open.");
                var fixture = new LeaderboardRoundSnapshot
                {
                    roundId = match.Stage2RoundId, status = LeaderboardRoundStatus.Confirmed,
                    rank = 10, previousRank = 15, score = 110, improved = true, bonusCoins = 0,
                    neighborsAvailable = true, targetRank = 9, targetXp = 11,
                    neighbors = new[]
                    {
                        new LeaderboardNeighbor { rank = 8, score = 130, name = "Тест 8" },
                        new LeaderboardNeighbor { rank = 9, score = 120, name = "Тест 9" },
                        new LeaderboardNeighbor { rank = 10, score = 110, isSelf = true },
                        new LeaderboardNeighbor { rank = 11, score = 100, name = "Тест 11" },
                        new LeaderboardNeighbor { rank = 12, score = 90, name = "Тест 12" }
                    }
                };
                // Render receives fixture data directly. The real service remains unavailable,
                // so its celebration/bonus guard cannot consume or award a confirmation.
                Invoke(view, "Render", fixture);
                title = Get<Text>(view, "title"); titleFont = title.fontSize;
                title.text = "Проверка анимации · тестовые данные"; title.fontSize = 16;
                Get<Text>(view, "statusText").text = "Тестовый набор · без записи профиля и запросов SDK";
                Invoke(view, "RenderNeighbors", fixture, true);
                Canvas.ForceUpdateCanvases();
                IList rows = Get<IList>(view, "rows");
                var active = new List<object>();
                foreach (object row in rows)
                    if (RowField<RectTransform>(row, "rect").gameObject.activeSelf) active.Add(row);
                report.activeRows = active.Count;
                object own = active.Single(row => RowField<Text>(row, "label").text.StartsWith("#10 ", StringComparison.Ordinal));
                object lower = active.Single(row => RowField<Text>(row, "label").text.StartsWith("#11 ", StringComparison.Ordinal));
                RectTransform ownRect = RowField<RectTransform>(own, "rect"), lowerRect = RowField<RectTransform>(lower, "rect");
                report.ownStart = ownRect.anchoredPosition; report.lowerStart = lowerRect.anchoredPosition;
                report.ownTarget = new Vector2(0, RowField<float>(own, "targetY"));
                report.lowerTarget = new Vector2(0, RowField<float>(lower, "targetY"));
                var animation = (IEnumerator)Invoke(view, "AnimateImprovement", fixture);
                bool sampled = false;
                while (animation.MoveNext())
                {
                    if (!sampled && ownRect.anchoredPosition.y > report.ownStart.y + .1f)
                    {
                        report.ownMiddle = ownRect.anchoredPosition;
                        report.lowerMiddle = lowerRect.anchoredPosition;
                        sampled = true;
                    }
                    yield return animation.Current;
                }
                report.ownEnd = ownRect.anchoredPosition; report.lowerEnd = lowerRect.anchoredPosition;
                Add(report, "bounded_actual_rows", active.Count >= 3 && active.Count <= 5, "Actual visible row RectTransforms=" + active.Count);
                Add(report, "own_row_moves_up", sampled && report.ownMiddle.y > report.ownStart.y && report.ownEnd.y > report.ownStart.y,
                    "Own anchored Y start/middle/end=" + report.ownStart.y + "/" + report.ownMiddle.y + "/" + report.ownEnd.y);
                Add(report, "lower_neighbor_moves_down", sampled && report.lowerMiddle.y < report.lowerStart.y && report.lowerEnd.y < report.lowerStart.y,
                    "Lower-neighbor anchored Y start/middle/end=" + report.lowerStart.y + "/" + report.lowerMiddle.y + "/" + report.lowerEnd.y);
                Add(report, "rows_arrive_at_confirmed_fixture_order", Vector2.Distance(report.ownEnd, report.ownTarget) < .1f &&
                    Vector2.Distance(report.lowerEnd, report.lowerTarget) < .1f,
                    "Own and lower-neighbor rows reached their actual target anchored positions.");
                Canvas.ForceUpdateCanvases();
                Rect viewport = Bounds(Get<RectTransform>(view, "viewport"));
                Rect share = Bounds(Get<Button>(view, "shareButton").GetComponent<RectTransform>());
                Rect close = Bounds(Get<Button>(view, "closeButton").GetComponent<RectTransform>());
                report.viewport = new Geometry(viewport); report.shareButton = new Geometry(share); report.closeButton = new Geometry(close);
                bool separate = !viewport.Overlaps(share) && !viewport.Overlaps(close) && viewport.yMin >= Mathf.Max(share.yMax, close.yMax) - .1f;
                Add(report, "viewport_footer_do_not_intersect", separate, "Measured current GameView RectTransform world bounds; scroll viewport remains above fixed action buttons.");
                Add(report, "footer_touch_targets", share.height >= 47.5f && close.height >= 47.5f,
                    "Share/back target heights=" + share.height + "/" + close.height);
                report.UiGeometryOwnRowMovementAndViewportFooter = separate && report.checks.Where(check =>
                    check.name == "own_row_moves_up" || check.name == "lower_neighbor_moves_down" || check.name == "rows_arrive_at_confirmed_fixture_order").All(check => check.passed);
                if (File.Exists(report.screenshot)) File.Delete(report.screenshot);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(report.screenshot);
                for (int frame = 0; frame < 6; frame++) yield return null;
                Add(report, "clearly_labeled_fixture_screenshot", File.Exists(report.screenshot) && new FileInfo(report.screenshot).Length > 1000,
                    "Screenshot title explicitly labels test data; actual offline SDK state is unchanged.");
                report.completed = true;
            }
            finally
            {
                if (view != null)
                {
                    if (title != null) title.fontSize = titleFont;
                    Invoke(view, "Render", offline);
                }
                LeaderboardRoundPanel.Close();
                report.profileUnchanged = ReferenceEquals(profile, YG2.saves) && JsonUtility.ToJson(YG2.saves) == profileJson;
                report.serviceSnapshotUnchanged = ReferenceEquals(offline, LeaderboardRoundService.Current) && Signature(LeaderboardRoundService.Current) == offlineSignature;
                Add(report, "profile_wallet_xp_records_untouched", report.profileUnchanged, "Full serialized profile and object identity are byte-equal to the fixture start.");
                Add(report, "service_offline_snapshot_untouched", report.serviceSnapshotUnchanged, "Real service remained unavailable; no simulated confirmed rank entered it.");
                Add(report, "offline_view_restored_and_closed", view == null || !view.gameObject.activeSelf,
                    "Restored the original offline Render state, then closed the panel.");
                report.passed = report.completed && report.checks.All(check => check.passed);
                File.WriteAllText(Path.Combine(directory, "ranking-animation-fixture.json"), JsonUtility.ToJson(report, true));
            }
            if (!report.passed) throw new InvalidOperationException("Leaderboard UI fixture failed; see Builds/Stage2QA/ranking-animation-fixture.json");
            Debug.Log("[Stage2QA] PASS ranking.ui-animation-fixture: actual own row moved up, neighbor moved down, footer separate; test data only.");
        }

        private static void Add(ViewReport report, string name, bool passed, string detail) => report.checks.Add(new Check { name = name, passed = passed, detail = detail });
        private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
        private static T RowField<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public).GetValue(target);
        private static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        private static string Signature(LeaderboardRoundSnapshot data) => data == null ? "null" : data.roundId + ":" + data.status + ":" + data.rank + ":" + data.score + ":" + data.previousRank + ":" + data.bonusCoins + ":" + data.improved;
        private static Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners.Min(point => point.x), corners.Min(point => point.y), corners.Max(point => point.x), corners.Max(point => point.y));
        }
    }
}
#endif
