using UnityEngine;
using YG;

public class BlackHoleController : HoleParent
{
    [Header("Спавнер хелперов")]
    public SpawnerOfHelpers spawnerOfHelpers;

    public override void Start()
    {
        base.Start();
        holeType = TypeOfHole.player;

        if (YG2.saves.nickName != "")
            nickname.text = YG2.saves.nickName;
        else
            nickname.text = YG2.saves.langRu ? "Легенда" : "Legend";

        if (spawnerOfHelpers == null)
            spawnerOfHelpers = FindAnyObjectByType<SpawnerOfHelpers>();
    }

    // Вызывается из FallingObject, когда колón поглощён игроком
    public void OnColonAbsorbed(Transform colonTransform)
    {
        spawnerOfHelpers?.SpawnHelper(colonTransform, this, score);
    }

    public void ReceiveHelperScore(int amount, bool saveProgress = true)
    {
        ApplyScoreChange(amount, false, true, saveProgress);
    }
}
