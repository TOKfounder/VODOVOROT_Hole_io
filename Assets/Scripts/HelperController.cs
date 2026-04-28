using UnityEngine;

public class HelperController : HoleParent
{
    public BlackHoleController Owner { get; private set; }

    public override void Start()
    {
        base.Start();
        holeType = TypeOfHole.playerHelper;

        // Helpers не должны иметь бордер и nickname
        if (border != null) border.gameObject.SetActive(false);
        if (nickname != null) nickname.gameObject.SetActive(false);
    }

    public void Initialize(BlackHoleController owner, int startingScore)
    {
        Owner = owner;
        holeType = TypeOfHole.playerHelper;
        InitializeScore(startingScore);
    }

    public override void AddScore(int amount)
    {
        if (amount <= 0)
            return;

        ApplyScoreChange(amount, true, true, false);
        Owner?.ReceiveHelperScore(amount, false);

        if (VodovorotGameManager.Instance != null)
            VodovorotGameManager.Instance.SaveProgress();
    }
}
