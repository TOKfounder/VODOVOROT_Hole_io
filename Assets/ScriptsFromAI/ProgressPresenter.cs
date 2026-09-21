using YG;

public class ProgressPresenter
{
	public static ProgressPresenter Instance { get; } = new ProgressPresenter();

	public ProgressModel Model { get; private set; }

	private ProgressPresenter()
	{
		Reload();
	}

	public void Reload()
	{
		ProgressConfig config = GameBalance.Progress;
		Model = new ProgressModel(ProgressRuntime.From(config));
	}

	public void Sync()
	{
		if (Model == null)
			Reload();
		Model.GrantPendingRewards();
	}

	public static int Level
	{
		get
		{
			Instance.Sync();
			return Instance.Model.CurrentLevel;
		}
	}

	public static float LevelFill
	{
		get
		{
			Instance.Sync();
			return Instance.Model.LevelFill;
		}
	}

	public static bool IsMapUnlocked(int mapId)
	{
		Instance.Model.EnsureInitialized();
		return Instance.Model.IsMapUnlocked(mapId);
	}

	public static bool IsModeUnlocked(ModeManager.Mode mode)
	{
		Instance.Model.EnsureInitialized();
		return Instance.Model.IsModeUnlocked(mode);
	}

	public static void UnlockPaidContent()
	{
		Instance.Model.UnlockPaidContent();
		YG2.SaveProgress();
	}

	public static void UnlockMap(int mapId)
	{
		Instance.Model.UnlockMap(mapId);
		YG2.SaveProgress();
	}

	public static void UnlockMode(ModeManager.Mode mode)
	{
		Instance.Model.UnlockMode(mode);
		YG2.SaveProgress();
	}
}
