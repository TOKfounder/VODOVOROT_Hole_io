using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using YG;

public class TutorialController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject tutorialCanvas;
    [SerializeField] private RectTransform tutorialPanel;
    [SerializeField] private Text textLabel;
    [SerializeField] private Button nextButton;
    [SerializeField] private Text nextButtonText;
    [SerializeField] private Button skipButton;
    [SerializeField] private Text skipButtonText;
    [SerializeField] private Image[] stepDots;
    [SerializeField] private RectTransform highlightFrame;
    [SerializeField] private RectTransform arrow;

    [Header("Settings")]
    [SerializeField] private float wordDelay = 0.12f;
    [SerializeField] private float pulseSpeed = 3f;
    [SerializeField] private float pulseMinAlpha = 0.3f;
    [SerializeField] private float pulseMaxAlpha = 1f;

    private readonly struct TutorialStep
    {
        public readonly string textRu;
        public readonly string textEn;
        public readonly string targetName;
        public readonly bool showHighlight;

        public TutorialStep(string ru, string en, string target, bool showHighlight = true)
        {
            textRu = ru;
            textEn = en;
            targetName = target;
            this.showHighlight = showHighlight;
        }
    }

    private static readonly TutorialStep[] steps =
    {
        new TutorialStep(
            "Двигай джойстиком, чтобы управлять своей дырой",
            "Move the joystick to control your hole",
            "JoystickRuler",
            showHighlight: false),          // джойстик = вся область, скрываем
        new TutorialStep(
            "Поглощай предметы — так ты растёшь в размере!",
            "Absorb objects — this is how you grow!",
            "Player_Hole"),
        new TutorialStep(
            "Следи за прогрессом — поглоти как можно больше!",
            "Watch your progress — absorb as much as you can!",
            "Indicator"),
        new TutorialStep(
            "Удерживай буст для ускорения! На ПК — клавиша Shift",
            "Hold boost for a speed rush! On PC — press Shift",
            "BoostButton"),
        new TutorialStep(
            "В настройках ты можешь регулировать звук и досрочно завершить игру",
            "In settings you can adjust sound and end the game early",
            "Settings")
    };

    private int currentStep;
    private bool isAnimating;
    private Coroutine textCoroutine;
    private Coroutine pulseCoroutine;
    private Image highlightImage;

    private void Awake()
    {
        highlightImage = highlightFrame.GetComponent<Image>();
    }

    private void Start()
    {
        if (YG2.saves.tutorialDone)
        {
            tutorialCanvas.SetActive(false);
            return;
        }

        tutorialCanvas.SetActive(false);
        StartCoroutine(BeginAfterFrame());
    }

    // Ждём один кадр — чтобы GamingManager.Start() успел поставить timeScale = 1
    private IEnumerator BeginAfterFrame()
    {
        yield return null;

        bool ru = YG2.saves.langRu;
        nextButtonText.text = ru ? "Далее" : "Next";
        skipButtonText.text = ru ? "Пропустить" : "Skip";

        nextButton.onClick.AddListener(OnNextClicked);
        skipButton.onClick.AddListener(EndTutorial);

        Time.timeScale = 0f;
        tutorialCanvas.SetActive(true);
        ShowStep(0);
    }

    private void ShowStep(int index)
    {
        currentStep = index;
        UpdateDots();

        string text = YG2.saves.langRu ? steps[index].textRu : steps[index].textEn;

        if (textCoroutine != null) StopCoroutine(textCoroutine);
        textCoroutine = StartCoroutine(AnimateText(text));

        PositionOnTarget(steps[index].targetName, steps[index].showHighlight);
    }

    private void OnNextClicked()
    {
        if (isAnimating)
        {
            StopCoroutine(textCoroutine);
            textLabel.text = YG2.saves.langRu ? steps[currentStep].textRu : steps[currentStep].textEn;
            isAnimating = false;
            return;
        }

        if (currentStep + 1 < steps.Length)
            ShowStep(currentStep + 1);
        else
            EndTutorial();
    }

    private void EndTutorial()
    {
        if (textCoroutine != null) StopCoroutine(textCoroutine);
        StopPulse();
        tutorialCanvas.SetActive(false);
        Time.timeScale = 1f;
        YG2.saves.tutorialDone = true;
        YG2.SaveProgress();
    }

    private IEnumerator AnimateText(string fullText)
    {
        isAnimating = true;
        textLabel.text = "";
        string[] words = fullText.Split(' ');
        string built = "";

        foreach (string word in words)
        {
            built += (built.Length > 0 ? " " : "") + word;
            textLabel.text = built;
            yield return new WaitForSecondsRealtime(wordDelay);
        }

        isAnimating = false;
    }

    private void UpdateDots()
    {
        for (int i = 0; i < stepDots.Length; i++)
            stepDots[i].color = i == currentStep
                ? Color.white
                : new Color(1f, 1f, 1f, 0.3f);
    }

    // Пульсация работает через unscaledTime — не зависит от timeScale = 0
    private IEnumerator PulseHighlight()
    {
        while (true)
        {
            float t = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) / 2f;
            float alpha = Mathf.Lerp(pulseMinAlpha, pulseMaxAlpha, t);
            Color c = highlightImage.color;
            highlightImage.color = new Color(c.r, c.g, c.b, alpha);
            yield return null;
        }
    }

    private void StopPulse()
    {
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }
        if (highlightImage != null)
        {
            Color c = highlightImage.color;
            highlightImage.color = new Color(c.r, c.g, c.b, 1f);
        }
    }

    private void PositionOnTarget(string targetName, bool showHighlight)
    {
        StopPulse();

        if (!showHighlight)
        {
            highlightFrame.gameObject.SetActive(false);
            arrow.gameObject.SetActive(false);
            return;
        }

        GameObject target = GameObject.Find(targetName);
        if (target == null)
        {
            highlightFrame.gameObject.SetActive(false);
            arrow.gameObject.SetActive(false);
            return;
        }

        RectTransform targetRect = target.GetComponent<RectTransform>();
        Vector2 targetCenter;

        if (targetRect != null)
        {
            Vector3[] corners = new Vector3[4];
            targetRect.GetWorldCorners(corners);
            Vector2 min = corners[0];
            Vector2 max = corners[2];
            targetCenter = (min + max) / 2f;
            Vector2 targetSize = max - min;

            const float padding = 10f;
            highlightFrame.gameObject.SetActive(true);
            highlightFrame.position = new Vector3(targetCenter.x, targetCenter.y, 0f);
            highlightFrame.sizeDelta = targetSize + Vector2.one * padding * 2f;

            pulseCoroutine = StartCoroutine(PulseHighlight());
        }
        else
        {
            // 3D-объект (Player_Hole)
            targetCenter = Camera.main.WorldToScreenPoint(target.transform.position);
            highlightFrame.gameObject.SetActive(false);
        }

        arrow.gameObject.SetActive(true);
        Vector2 panelCenter = tutorialPanel.position;
        Vector2 direction = targetCenter - panelCenter;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        arrow.position = new Vector3(
            panelCenter.x + direction.x * 0.5f,
            panelCenter.y + direction.y * 0.5f,
            0f);
        arrow.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}
