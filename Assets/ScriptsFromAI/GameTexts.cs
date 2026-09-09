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

	public static string Tutorial => Ru ? "Обучение" : "Tutorial";
	public static string TutorialNext => Ru ? "Далее" : "Next";
	public static string TutorialSkip => Ru ? "Скип" : "Skip";
	public static string TutorialOk => Ru ? "Ок!" : "OK!";
	public static string TutorialFinger => "👉";

	public static string TutorialNickTitle => Ru ? "Как тебя звать?" : "What's your name?";
	public static string TutorialNickBody => Ru
		? "Напиши ник. Пустым в водоворот не пускаем — даже унитаз знает, кого глотать."
		: "Type a nickname. Empty names don't go down the drain.";
	public static string TutorialNickNeed => Ru ? "Сначала введи ник" : "Enter a nickname first";

	public static string TutorialMoveTitle => Ru ? "Куда катим" : "How to move";
	public static string TutorialMoveBody => Ru
		? "ПК: WASD или стрелки. Джойстик на экране — если трогаешь его, клавиатура молчит, и наоборот."
		: "PC: WASD or arrows. On-screen stick and keyboard never mix: one at a time.";

	public static string TutorialBoostTitle => Ru ? "Буст" : "Boost";
	public static string TutorialBoostBody => Ru
		? "Зажми кнопку буста. На ПК ещё Shift или Пробел. Отпустил — снова спокойный смыв."
		: "Hold the boost button. On PC, Shift or Space also work. Release to cruise.";

	public static string TutorialGrowTitle => Ru ? "Расти и сияй" : "Grow and glow";
	public static string TutorialGrowBody => Ru
		? "Ешь то, что меньше дыры — уровень растёт. Не-красный скин чуть быстрее и поярче. Красный тазик — честный сток без бонусов."
		: "Eat what is smaller than you to level up. Non-red skins are a bit faster and flashier. The red basin is the honest no-bonus start.";

	public static string TutorialEatTitle => Ru ? "Пора перекусить" : "Time to snack";
	public static string TutorialEatBody => Ru
		? "Заезжай на объект меньше дыры. Если не влез — он вернётся на место, не обижайся."
		: "Drive over something smaller than the hole. Miss it, and it pops back. No hard feelings.";

	public static string TutorialArrowsTitle => Ru ? "Куда смотреть" : "Where to look";
	public static string TutorialArrowsBody => Ru
		? "Стрелки и точки на карте — живые цели. Жёлтое было вчера: враги красные, босс чёрный, как пробка в трубе."
		: "Arrows and map dots mark live targets. Enemies are red. The boss is black, like a clog in the pipe.";

	public static string TutorialEatHint => Ru ? "Съешь что-нибудь поменьше 👉" : "Eat something smaller 👉";
	public static string TutorialEatDoneTitle => Ru ? "Глоток засчитан" : "That's a gulp";
	public static string TutorialEatDoneBody => Ru
		? "Так и живём: меньше — в дыру, больше — объезжай. Дальше сам, легенда."
		: "That's the loop: smaller goes in, bigger you skip. You're on your own now.";
}
