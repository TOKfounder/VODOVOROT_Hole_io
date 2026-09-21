using UnityEngine;
using YG;

public readonly struct ProgressRoadNode
{
	public readonly int level;
	public readonly string title;
	public readonly string detail;
	public readonly bool milestone;
	public readonly bool reached;
	public readonly bool current;

	public ProgressRoadNode(int level, string title, string detail, bool milestone, bool reached, bool current)
	{
		this.level = level;
		this.title = title;
		this.detail = detail;
		this.milestone = milestone;
		this.reached = reached;
		this.current = current;
	}
}

public class ProgressModel
{
	public ProgressRuntime Runtime { get; private set; }

	public ProgressModel(ProgressRuntime runtime)
	{
		Runtime = runtime ?? ProgressRuntime.From(null);
	}

	public int CurrentLevel => Runtime.GetLevel(YG2.saves.exp);
	public float LevelFill => Runtime.LevelFill(YG2.saves.exp);
	public float OverallFill => Runtime.OverallFill(YG2.saves.exp);

	public void EnsureInitialized()
	{
		if (YG2.saves.progressClaimedLevel < 1)
			YG2.saves.progressClaimedLevel = 1;

		if (YG2.saves.contentLocksVersion < ProgressConfig.ContentLocksVersion)
		{
			if (TutorialController.IsDone)
			{
				YG2.saves.unlockedMapsMask = ProgressConfig.AllContentMask;
				YG2.saves.unlockedModesMask = ProgressConfig.AllContentMask;
			}
			else
			{
				YG2.saves.unlockedMapsMask = ProgressConfig.FreeMapsMask;
				YG2.saves.unlockedModesMask = ProgressConfig.FreeModesMask;
			}
			YG2.saves.contentLocksVersion = ProgressConfig.ContentLocksVersion;
		}

		YG2.saves.levelOfProgress = CurrentLevel;
		NormalizeSelection();
	}

	private static bool HasBit(int mask, int bit)
	{
		if (bit < 0 || bit > 3)
			return false;
		return (mask & (1 << bit)) != 0;
	}

	public void GrantPendingRewards()
	{
		EnsureInitialized();
		int level = CurrentLevel;
		int claimed = Mathf.Max(1, YG2.saves.progressClaimedLevel);
		if (claimed >= level)
		{
			ConvertOverflow();
			YG2.saves.levelOfProgress = level;
			return;
		}

		while (claimed < level)
		{
			claimed++;
			YG2.saves.goldCoins += Runtime.CoinsAt(claimed);
			YG2.saves.diamonds += Runtime.DiamondsAt(claimed);
			ApplyUnlocksForLevel(claimed);
		}

		YG2.saves.progressClaimedLevel = claimed;
		YG2.saves.levelOfProgress = level;
		ConvertOverflow();
		NormalizeSelection();
	}

	public bool IsMapUnlocked(int mapId)
	{
		EnsureInitialized();
		return HasBit(YG2.saves.unlockedMapsMask, mapId);
	}

	public bool IsModeUnlocked(ModeManager.Mode mode)
	{
		EnsureInitialized();
		return HasBit(YG2.saves.unlockedModesMask, (int)mode);
	}

	public void UnlockPaidContent()
	{
		EnsureInitialized();
		YG2.saves.unlockedMapsMask |= ProgressConfig.AllContentMask;
		YG2.saves.unlockedModesMask |= ProgressConfig.AllContentMask;
	}

	public void UnlockMap(int mapId)
	{
		EnsureInitialized();
		if (mapId < 0 || mapId > 3)
			return;
		YG2.saves.unlockedMapsMask |= 1 << mapId;
	}

	public void UnlockMode(ModeManager.Mode mode)
	{
		EnsureInitialized();
		int bit = (int)mode;
		if (bit < 0 || bit > 3)
			return;
		YG2.saves.unlockedModesMask |= 1 << bit;
	}

	public int UnlockLevelForMap(int mapId)
	{
		if (mapId == ProgressConfig.CastleMapId)
			return Runtime.castleUnlockLevel;
		if (mapId == ProgressConfig.IndustrialMapId)
			return Runtime.industrialUnlockLevel;
		return 1;
	}

	public int UnlockLevelForMode(ModeManager.Mode mode)
	{
		if (mode == ModeManager.Mode.Hunting)
			return Runtime.huntingUnlockLevel;
		if (mode == ModeManager.Mode.TeamMode)
			return Runtime.teamUnlockLevel;
		return 1;
	}

	public ProgressRoadNode[] BuildRoad()
	{
		int level = CurrentLevel;
		int max = Runtime.maxLevel;
		ProgressRoadNode[] nodes = new ProgressRoadNode[max];
		for (int i = 0; i < max; i++)
		{
			int nodeLevel = i + 1;
			string title;
			string detail;
			bool milestone = GameTexts.TryProgressMilestone(nodeLevel, this, out title, out detail);
			nodes[i] = new ProgressRoadNode(
				nodeLevel,
				title,
				detail,
				milestone,
				nodeLevel <= level,
				nodeLevel == level);
		}
		return nodes;
	}

	public void NormalizeSelection()
	{
		if (!HasBit(YG2.saves.unlockedMapsMask, YG2.saves.selectedMapID))
			YG2.saves.selectedMapID = ProgressConfig.GardenMapId;
		if (!HasBit(YG2.saves.unlockedModesMask, YG2.saves.chosenMode))
		{
			YG2.saves.chosenMode = (int)ModeManager.Mode.Boss;
			ModeManager.currentMode = ModeManager.Mode.Boss;
		}
	}

	private void ApplyUnlocksForLevel(int level)
	{
		if (level >= Runtime.huntingUnlockLevel)
			UnlockMode(ModeManager.Mode.Hunting);
		if (level >= Runtime.castleUnlockLevel)
			UnlockMap(ProgressConfig.CastleMapId);
		if (level >= Runtime.teamUnlockLevel)
			UnlockMode(ModeManager.Mode.TeamMode);
		if (level >= Runtime.industrialUnlockLevel)
			UnlockMap(ProgressConfig.IndustrialMapId);
	}

	private void ConvertOverflow()
	{
		int capExp = Runtime.ExpToReach(Runtime.maxLevel);
		int extra = YG2.saves.exp - capExp;
		if (extra <= 0)
			return;

		int already = Mathf.Max(0, YG2.saves.overflowExpConverted);
		int fresh = extra - already;
		if (fresh < Runtime.overflowExpPerCoin)
			return;

		int coins = fresh / Runtime.overflowExpPerCoin;
		YG2.saves.goldCoins += coins;
		YG2.saves.overflowExpConverted = already + coins * Runtime.overflowExpPerCoin;
	}
}
