using UnityEngine;
using YG;

public static class GameTexts
{
	public static bool Ru => YG2.saves.langRu;

	public static string LegendNick => Ru ? "Легенда" : "Legend";
	public static string SpeedBoost => Ru ? "Буст Скорости" : "Speed Boost";
	public static string Settings => Ru ? "Настройки" : "Settings";
	public static string Language => Ru ? "Язык" : "Language";
	public static string Sounds => Ru ? "Звуки" : "Sounds";
	public static string Music => Ru ? "Музыка" : "Music";
	public static string EndTheGame => Ru ? "Завершить игру" : "End the game";
	public static string Experience => Ru ? "Опыт:" : "Experience:";
	public static string Result => Ru ? "Итог" : "Result";
	public static string Coins => Ru ? "Монеты:" : "Coins:";
	public static string Diamonds => Ru ? "Алмазы:" : "Diamonds:";
	public static string Continue => Ru ? "Продолжить" : "Continue";
	public static string X3Coins => Ru ? "x3 Монеты\n(короткая реклама)" : "x3 Coins\n(short ad)";
	public static string Title => Ru ? "ВОДОВОРОТ Дыра.ио" : "WHIRLPOOL Hole.io";
	public static string EnterNick => Ru ? "Введите ваш ник" : "Enter your nickname";
	public static string Level => Ru ? "Уровень" : "Level";
	public static string Victory => Ru ? "Победа" : "Victory";
	public static string Defeat => Ru ? "Поражение" : "Defeat";
	public static string Draw => Ru ? "Ничья" : "Draw";
	public static string Equipped => Ru ? "Надето" : "equipped";
	public static string Equip => Ru ? "Одеть" : "equip";

	public static string RedBasinFeature => Ru
		? "Простой красный тазик — старт без бонусов. Надёжная классика стока!"
		: "A plain red basin — no stat bonuses. Reliable drain classic!";

	public static string ReasonBossAbsorbed => Ru
		? "Босс поглощён"
		: "The boss was absorbed";
	public static string ReasonBiggerWin => Ru
		? "К концу матча вы были крупнее"
		: "You were bigger when time ran out";
	public static string ReasonBiggerLoss => Ru
		? "К концу матча босс был крупнее"
		: "The boss was bigger when time ran out";
	public static string ReasonBossDraw => Ru
		? "К концу матча размеры равны"
		: "Same size when time ran out";
	public static string ReasonHuntingJackpot => Ru
		? "Все враги поглощены"
		: "All enemies absorbed";
	public static string ReasonHuntingKills(int killed, int total)
	{
		return Ru
			? $"Поглощено врагов: {killed} из {total}"
			: $"Enemies absorbed: {killed} of {total}";
	}
	public static string ReasonCleaningDone => Ru
		? "Карта зачищена"
		: "The map is cleared";
	public static string ReasonCleaningTimeout(int percent)
	{
		return Ru
			? $"Время вышло: {percent}%"
			: $"Time is up: {percent}%";
	}
	public static string ReasonTeamWin => Ru
		? "Синие набрали больше очков"
		: "Blue scored more points";
	public static string ReasonTeamLose => Ru
		? "Красные набрали больше очков"
		: "Red scored more points";
	public static string ReasonTeamDraw => Ru
		? "Очки команд равны"
		: "Both teams scored the same";
	public static string ReasonEaten => Ru
		? "Вас поглотили"
		: "You were absorbed";
	public static string ReasonEnemiesCleared => Ru
		? "Вражеская команда уничтожена"
		: "The enemy team is gone";
}
