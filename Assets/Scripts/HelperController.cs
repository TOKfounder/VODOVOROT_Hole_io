using UnityEngine;

public class HelperController : HoleParent
{
    public BlackHoleController Owner { get; private set; }
    private Renderer[] visualRenderers;

    protected override void Awake()
    {
        base.Awake();
        visualRenderers = GetComponentsInChildren<Renderer>(true);
    }

    public override void Start()
    {
        base.Start();
        holeType = TypeOfHole.playerHelper;
    }

    public void Initialize(BlackHoleController owner, int startingScore)
    {
        Owner = owner;
        holeType = TypeOfHole.playerHelper;
        if (border == null)
            border = GetComponentInChildren<UnityEngine.UI.Image>(true);
        if (border != null)
            border.gameObject.SetActive(true);
        int spawnScore = startingScore > 0 ? startingScore : score;
        InitializeScore(spawnScore);
        transform.localScale = targetScale;

        if (nickname != null)
        {
            nickname.gameObject.SetActive(true);
            if (Owner != null && Owner.nickname != null)
                nickname.text = Owner.nickname.text;
        }

        SetVisualsEnabled(true);
    }

    public override void AddScore(int amount)
    {
        if (amount <= 0)
            return;

        ApplyScoreChange(amount, true, ShouldShowPointEffect(), false);
        Owner?.ReceiveHelperScore(amount, false);

        if (VodovorotGameManager.Instance != null)
            VodovorotGameManager.Instance.SaveProgress();
    }

    private void SetVisualsEnabled(bool enabled)
    {
        if (visualRenderers == null || visualRenderers.Length == 0)
            visualRenderers = GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < visualRenderers.Length; i++)
        {
            Renderer renderer = visualRenderers[i];
            if (renderer == null)
                continue;

            if (border != null && renderer.gameObject == border.gameObject)
                continue;

            renderer.enabled = enabled;
        }
    }
}
