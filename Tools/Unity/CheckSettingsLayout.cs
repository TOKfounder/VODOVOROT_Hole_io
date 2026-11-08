using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Vodovorot.Stage3;

public static class CheckSettingsLayout
{
    private static readonly List<object> checks = new List<object>();
    private static readonly List<object> traces = new List<object>();

    public static async Task<string> Run()
    {
        checks.Clear();
        traces.Clear();
        string error = null;
        byte[] save = File.ReadAllBytes("Assets/PluginYourGames/Editor/SavesEditorYG2.json");
        try
        {
            Assert(Application.isPlaying && RankSimulationContext.Active && YG.YG2.saves.musicValue == 0 && YG.YG2.saves.soundValue == 0,
                "isolated silent profile");
            foreach (int map in new[] { 0, 1 })
            {
                YG.YG2.saves.isGaming = map != 0;
                SceneManager.LoadScene(map);
                await Task.Delay(500);
                GameController controller = UnityEngine.Object.FindAnyObjectByType<GameController>();
                AudioManager audio = UnityEngine.Object.FindAnyObjectByType<AudioManager>();
                foreach (bool mobile in new[] { false, true })
                {
                    controller.MobileSettingsPanel.SetActive(false);
                    controller.DesktopSettingsPanel.SetActive(false);
                    GameObject panel = mobile ? controller.MobileSettingsPanel : controller.DesktopSettingsPanel;
                    panel.SetActive(true);
                    await Task.Delay(100);
                    Canvas.ForceUpdateCanvases();
                    Assert(panel.GetComponentInParent<Canvas>().rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay,
                        "settings use the authored viewport Canvas: scene" + map + " mobile=" + mobile);
                    foreach (Text label in panel.GetComponentsInChildren<Text>(true))
                    {
                        Vector3[] corners = new Vector3[4];
                        label.rectTransform.GetWorldCorners(corners);
                        Assert(corners.All(x => x.x >= 0 && x.x <= Screen.width && x.y >= 0 && x.y <= Screen.height),
                            "label inside viewport: " + map + "/" + mobile + "/" + label.name);
                    }
                    Slider sound = mobile ? audio.SoundSlider : audio.DSoundSlider;
                    Slider music = mobile ? audio.MusicSlider : audio.DMusicSlider;
                    DragTo(sound, 0f, map, mobile);
                    Assert(YG.YG2.saves.soundValue <= .01f && audio.SoundSlider.value <= .01f && audio.DSoundSlider.value <= .01f,
                        "SFX pointer changes both controls and real profile: " + map + "/" + mobile);
                    DragTo(music, 0f, map, mobile);
                    Assert(YG.YG2.saves.musicValue <= .01f, "music pointer changes real profile: " + map + "/" + mobile);
                    // Keep this entire fixture quiet. Actual midpoint restoration
                    // is checked through the browser settings at handoff.
                    sound.value = 0;
                    music.value = 0;
                    Button close = panel.transform.Find("Return").GetComponent<Button>();
                    close.onClick.Invoke();
                    Assert(!panel.activeSelf, "existing close action still closes panel: " + map + "/" + mobile);
                }
            }
            Assert(save.SequenceEqual(File.ReadAllBytes("Assets/PluginYourGames/Editor/SavesEditorYG2.json")), "personal save bytes unchanged");
        }
        catch (Exception failure)
        {
            error = failure.ToString();
            throw;
        }
        finally
        {
            File.WriteAllText("Docs/MapPolish-20261108/Checks/settings-input.json", Newtonsoft.Json.JsonConvert.SerializeObject(
                new
                {
                    environment = "Editor pointer-event/component fixture, not native or browser input",
                    width = Screen.width,
                    height = Screen.height,
                    checks,
                    traces,
                    error
                }, Newtonsoft.Json.Formatting.Indented));
            RankSimulationContext.Stop();
        }
        return "PASS: " + checks.Count + " settings bounds, input, sync and close assertions.";
    }

    private static void DragTo(Slider slider, float amount, int map, bool mobile)
    {
        RectTransform area = slider.handleRect.parent as RectTransform;
        Vector3 point = area.TransformPoint(new Vector3(Mathf.Lerp(area.rect.xMin, area.rect.xMax, amount), area.rect.center.y, 0));
        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, point), button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        traces.Add(new
        {
            map,
            mobile,
            slider = slider.name,
            amount,
            position = pointer.position.ToString(),
            raycasts = hits.Take(6).Select(x => x.gameObject.name).ToArray()
        });
        Assert(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(slider.transform), "pointer reaches slider instead of backdrop: " + slider.name);
        pointer.pointerPressRaycast = pointer.pointerCurrentRaycast = hits[0];
        slider.OnPointerDown(pointer);
        slider.OnDrag(pointer);
        slider.OnPointerUp(pointer);
    }

    private static void Assert(bool passed, string name)
    {
        checks.Add(new
        {
            name,
            passed
        });
        if (!passed)
            throw new Exception(name);
    }
}
