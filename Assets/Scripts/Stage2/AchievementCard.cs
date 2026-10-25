using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using YG;

namespace Vodovorot.Stage2
{
    /// <summary>Uses actual equipped mesh/material and settled game data. Sharing never fabricates a rank.</summary>
    public sealed class AchievementCard : MonoBehaviour
    {
        [Serializable]
        public sealed class CardData
        {
            public bool ru;
            public string percent;
            public string map;
            public string mode;
            public string skin;
            public string url;
            public int rank;
            public int xp;
        }

        private static AchievementCard current;
        private CardData data;
        private Texture2D portrait;
        private RectTransform card;
        private Text status;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void VodovorotAchievementOpen(string json, string portraitBase64);
#endif

        public static CardData BuildData(GamingManager match)
        {
            bool ru = YG2.saves.langRu;
            int map = match != null ? match.Stage2Map : UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex - 1;
            string[] maps = ru ? new[] { "Город", "Сад", "Средневековье" } : new[] { "City", "Garden", "Medieval Castle" };
            string[] modes = ru ? new[] { "Тотальная зачистка", "Босс", "Охота", "Командный" }
                : new[] { "Total Cleaning", "Boss", "Hunting", "Team" };
            string[] skins = ru ? new[] { "Красный тазик", "Белый друг", "Золотой унитаз", "Трон Кощея", "Туалет Бога" }
                : new[] { "Red Basin", "White Friend", "Golden Toilet", "Scrag's Throne", "God's Toilet" };
            var confirmed = LeaderboardRoundService.Current;
            int rank = confirmed != null && match != null && confirmed.roundId == match.Stage2RoundId &&
                confirmed.status == LeaderboardRoundStatus.Confirmed ? confirmed.rank : 0;
            string app = YG2.envir.appID;
            if (string.IsNullOrEmpty(app) || !long.TryParse(app, out long id) || id <= 0) app = "473763";
            return new CardData
            {
                ru = ru,
                percent = (Mathf.Floor(Mathf.Clamp01(match != null ? match.GetCapturePercent() : 0) * 1000f) / 10f)
                    .ToString("0.0", CultureInfo.InvariantCulture) + "%",
                map = maps[Mathf.Clamp(map, 0, maps.Length - 1)],
                mode = modes[Mathf.Clamp(match != null ? match.Stage2Mode : YG2.saves.chosenMode, 0, modes.Length - 1)],
                skin = skins[Mathf.Clamp(match != null ? match.Stage2EquippedSkin : YG2.saves.equipedMaterial, 0, skins.Length - 1)],
                rank = Mathf.Max(0, rank), xp = Mathf.Max(0, YG2.saves.exp),
                url = "https://yandex.ru/games/app/" + app
            };
        }

        public static void Show(GamingManager match)
        {
            if (current != null) Destroy(current.gameObject);
            var data = BuildData(match);
            Texture2D portrait = RenderEquippedSkin(match != null ? match.Stage2EquippedSkin : -1);
#if UNITY_WEBGL && !UNITY_EDITOR
            VodovorotAchievementOpen(JsonUtility.ToJson(data), portrait != null ? Convert.ToBase64String(portrait.EncodeToPNG()) : "");
            if (portrait != null) Destroy(portrait);
#else
            var overlay = Stage2Ui.Overlay("AchievementCard", 3100);
            current = overlay.gameObject.AddComponent<AchievementCard>();
            current.data = data; current.portrait = portrait;
            current.Build(overlay);
#endif
        }

        private void Build(RectTransform root)
        {
            card = Stage2Ui.Rect(root, "Achievement", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            card.sizeDelta = new Vector2(800, 1050);
            card.gameObject.AddComponent<Image>().color = Stage2Ui.Paper;
            Stage2Ui.Text(card, data.ru ? "МОЙ ВОДОВОРОТ" : "MY VODOVOROT", 45, new Vector2(.05f, .88f), new Vector2(.95f, .97f));
            var pictureFrame = Stage2Ui.Rect(card, "PortraitFrame", new Vector2(.15f, .50f), new Vector2(.85f, .87f));
            var picture = Stage2Ui.Rect(pictureFrame, "EquippedSkin", Vector2.zero, Vector2.one).gameObject.AddComponent<RawImage>();
            picture.texture = portrait; picture.raycastTarget = false;
            picture.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            Stage2Ui.Text(card, data.skin, 29, new Vector2(.05f, .43f), new Vector2(.95f, .5f));
            Stage2Ui.Text(card, data.percent, 108, new Vector2(.04f, .27f), new Vector2(.96f, .43f), Stage2Ui.Accent);
            Stage2Ui.Text(card, data.map + " · " + data.mode, 26, new Vector2(.04f, .21f), new Vector2(.96f, .28f));
            Stage2Ui.Text(card, data.rank > 0 ? (data.ru ? "Место в общем рейтинге: " : "Global rank: ") + data.rank
                : (data.ru ? "Расту с каждой игрой" : "Growing with every game"), 31,
                new Vector2(.05f, .13f), new Vector2(.95f, .21f));
            Stage2Ui.Text(card, data.url, 23, new Vector2(.05f, .04f), new Vector2(.95f, .12f));
            Stage2Ui.Button(root, data.ru ? "Сохранить карточку" : "Save card", new Vector2(.08f, .035f), new Vector2(.48f, .095f),
                () => StartCoroutine(SaveImage()));
            Stage2Ui.Button(root, data.ru ? "Скопировать ссылку" : "Copy link", new Vector2(.52f, .035f), new Vector2(.92f, .095f),
                () => { GUIUtility.systemCopyBuffer = data.url; status.text = data.ru ? "Ссылка скопирована" : "Link copied"; });
            Stage2Ui.Button(root, data.ru ? "Закрыть" : "Close", new Vector2(.72f, .92f), new Vector2(.96f, .98f), () => Destroy(gameObject));
            status = Stage2Ui.Text(root, "", 23, new Vector2(.06f, .105f), new Vector2(.94f, .15f), Color.white);
        }

        private void LateUpdate()
        {
            if (card == null) return;
            var root = (RectTransform)transform;
            float scale = Mathf.Min(root.rect.width * .9f / 800f, root.rect.height * .7f / 1050f);
            card.localScale = Vector3.one * scale;
            if (Input.GetKeyDown(KeyCode.Escape)) Destroy(gameObject);
        }

        private IEnumerator SaveImage()
        {
            yield return new WaitForEndOfFrame();
            var corners = new Vector3[4]; card.GetWorldCorners(corners);
            int x = Mathf.Clamp(Mathf.FloorToInt(corners[0].x), 0, Screen.width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(corners[0].y), 0, Screen.height - 1);
            int width = Mathf.Clamp(Mathf.RoundToInt(corners[2].x - x), 1, Screen.width - x);
            int height = Mathf.Clamp(Mathf.RoundToInt(corners[2].y - y), 1, Screen.height - y);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(x, y, width, height), 0, 0); image.Apply();
            string path = Path.Combine(Application.persistentDataPath, "vodovorot-achievement.png");
            try
            {
                File.WriteAllBytes(path, image.EncodeToPNG());
                status.text = data.ru ? "Карточка сохранена. Ссылка доступна рядом." : "Card saved. Copy the link alongside it.";
                Debug.Log("[Vodovorot] Achievement card saved: " + path);
            }
            catch (Exception) { status.text = data.ru ? "Не удалось сохранить карточку" : "Could not save the card"; }
            Destroy(image);
        }

        public static Texture2D RenderEquippedSkin(int equippedSkin = -1)
        {
            var controller = VodovorotGameManager.Instance != null ? VodovorotGameManager.Instance.GameController : null;
            if (controller == null) return null;
            int skin = equippedSkin >= 0 ? equippedSkin : YG2.saves.equipedMaterial;
            GameObject source = skin == 0 ? controller.mainObodok : controller.mainToilet;
            if (source == null) return null;
            var root = new GameObject("AchievementPortraitStage");
            var model = new GameObject("NormalizedMeshes"); model.transform.SetParent(root.transform, false);
            var filters = source.GetComponentsInChildren<MeshFilter>(true);
            bool hasBounds = false; Bounds bounds = default;
            foreach (var filter in filters)
            {
                var original = filter.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || original == null) continue;
                var mesh = new GameObject("PortraitMesh", typeof(MeshFilter), typeof(MeshRenderer)); mesh.layer = 31;
                mesh.transform.SetParent(model.transform, false);
                mesh.transform.localPosition = source.transform.InverseTransformPoint(filter.transform.position);
                mesh.transform.localRotation = Quaternion.Inverse(source.transform.rotation) * filter.transform.rotation;
                Vector3 scale = source.transform.lossyScale, child = filter.transform.lossyScale;
                mesh.transform.localScale = new Vector3(child.x / Mathf.Max(.001f, Mathf.Abs(scale.x)),
                    child.y / Mathf.Max(.001f, Mathf.Abs(scale.y)), child.z / Mathf.Max(.001f, Mathf.Abs(scale.z)));
                mesh.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var renderer = mesh.GetComponent<MeshRenderer>(); renderer.sharedMaterials = original.sharedMaterials;
                if (original.gameObject == source && controller.materials != null && skin >= 0 && skin < controller.materials.Length)
                    renderer.sharedMaterial = controller.materials[skin];
                if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; } else bounds.Encapsulate(renderer.bounds);
            }
            if (!hasBounds) { root.SetActive(false); Destroy(root); return null; }
            // FBX import units vary (the basin is centimetre-sized). Normalize near the
            // origin before isolating the portrait from the world, avoiding float quantization.
            float dimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float normalization = 1.5f / Mathf.Max(.00001f, dimension);
            model.transform.localScale = Vector3.one * normalization;
            model.transform.localPosition = -bounds.center * normalization;
            root.transform.position = new Vector3(30000, 30000, 30000);
            hasBounds = false;
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
            {
                if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            var cameraObject = new GameObject("PortraitCamera", typeof(Camera)); cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false; camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(1, 1, 1, 0);
            camera.orthographic = true; camera.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) * .78f;
            camera.nearClipPlane = .01f; camera.farClipPlane = 1000;
            camera.transform.position = bounds.center + new Vector3(1.3f, .9f, -2f).normalized * (bounds.size.magnitude * 2 + 1);
            camera.transform.LookAt(bounds.center);
            var lightObject = new GameObject("PortraitLight", typeof(Light)); lightObject.transform.SetParent(root.transform, false);
            var light = lightObject.GetComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.1f;
            light.cullingMask = 1 << 31; light.transform.rotation = Quaternion.Euler(35, -30, 0);
            var target = RenderTexture.GetTemporary(512, 512, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                var image = new Texture2D(512, 512, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); image.Apply(); return image;
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target);
                // Destroy is deferred until the end of the frame. Disable this stage now so
                // another skin rendered in the same frame cannot see its meshes or light.
                root.SetActive(false); Destroy(root);
            }
        }

        private void OnDestroy() { if (portrait != null) Destroy(portrait); if (current == this) current = null; }
    }
}
