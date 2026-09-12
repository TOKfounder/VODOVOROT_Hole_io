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

	private static readonly string[] SkinFlavorRu =
	{
		"Простой красный тазик — старт без бонусов. Надёжная классика стока!",
		"Этот унитаз готов поддержать тебя в любой трудной и странной ситуации!",
		"Блеск роскоши для истинных чемпионов! Стань королём туалетных побед.",
		"Сиди с комфортом и властвуй! Злые силы не пройдут через эту дыру...",
		"На этом троне даже проблемы исчезают! Почувствуй себя властелином стока."
	};

	private static readonly string[] SkinFlavorEn =
	{
		"A plain red basin — no stat bonuses. Reliable drain classic!",
		"This toilet bowl is ready to support you in any difficult and strange situation!",
		"The splendor of luxury for true champions! Become the king of toilet victories.",
		"Sit comfortably and rule! Evil forces will not pass through this hole...",
		"On this throne, even problems disappear! Feel like the lord of the drain."
	};

	public static string SkinBonusLine(int index)
	{
		int speed = SkinStats.SpeedBonusPercent(index);
		int size = SkinStats.StartBonusPercent(index);
		if (speed <= 0 && size <= 0)
			return Ru ? "Бонус: нет." : "Bonus: none.";
		return Ru
			? $"Бонус: скорость +{speed}%, размер +{size}%."
			: $"Bonus: speed +{speed}%, size +{size}%.";
	}

	public static string SkinFeature(int index)
	{
		string[] flavors = Ru ? SkinFlavorRu : SkinFlavorEn;
		if (index < 0 || index >= flavors.Length)
			index = 0;
		return flavors[index] + "\n" + SkinBonusLine(index);
	}

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
	public static string TutorialSkip => Ru ? "Пропустить" : "Skip";
	public static string TutorialOk => Ru ? "Ок" : "OK";

	public static string TutorialNickTitle => Ru ? "Имя" : "Name";
	public static string TutorialNickBody => Ru
		? "Введи ник, чтобы начать."
		: "Enter a nickname to start.";
	public static string TutorialNickNeed => Ru ? "Сначала введи ник." : "Enter a nickname first.";

	public static string TutorialLegendNickTitle => Ru ? "История легенд" : "Legend history";
	public static string TutorialLegendNickBody => Ru
		? "Под каким именем внести вас в историю легенд?"
		: "What name should we enter in the legend history?";

	public static string TutorialModesTitle => Ru ? "Режимы" : "Modes";
	public static string TutorialModesBody => Ru ? "Открой режимы игры." : "Open the game modes.";
	public static string TutorialCleaningPickTitle => Ru ? "Зачистка" : "Cleaning";
	public static string TutorialCleaningPickBody => Ru
		? "Выбери тотальную зачистку."
		: "Choose Total Cleaning.";
	public static string TutorialReturnTitle => Ru ? "Готово" : "Done";
	public static string TutorialReturnBody => Ru ? "Закрой панель режимов." : "Close the modes panel.";
	public static string TutorialMapsTitle => Ru ? "Карты" : "Maps";
	public static string TutorialMapsBody => Ru ? "Открой список карт." : "Open the maps list.";
	public static string TutorialCityTitle => Ru ? "Город" : "City";
	public static string TutorialCityBody => Ru ? "Выбери карту City." : "Choose the City map.";
	public static string TutorialPlayTitle => Ru ? "Играть" : "Play";
	public static string TutorialPlayBody => Ru ? "Нажми Играть." : "Tap Play.";
	public static string TutorialContinueTitle => Ru ? "Продолжить" : "Continue";
	public static string TutorialContinueBody => Ru ? "Нажми Продолжить." : "Tap Continue.";
	public static string TutorialCurrencyTitle => Ru ? "Валюта" : "Currency";
	public static string TutorialCurrencyBody => Ru ? "Открой магазин валюты." : "Open the currency shop.";
	public static string TutorialExchangeTitle => Ru ? "Обмен" : "Exchange";
	public static string TutorialExchangeBody => Ru ? "Обменяй алмазы на монеты." : "Exchange diamonds for coins.";
	public static string TutorialValuteReturnTitle => Ru ? "Закрой магазин" : "Close shop";
	public static string TutorialValuteReturnBody => Ru
		? "Закрой магазин валюты."
		: "Close the currency shop.";
	public static string TutorialSkinsTitle => Ru ? "Скины" : "Skins";
	public static string TutorialSkinsBody => Ru ? "Открой магазин скинов." : "Open the skin shop.";
	public static string TutorialRotateTitle => Ru ? "Белый друг" : "White Friend";
	public static string TutorialRotateBody => Ru
		? "Прокрути карусель до Белого друга."
		: "Rotate the carousel to White Friend.";
	public static string TutorialBuyTitle => Ru ? "Покупка" : "Buy";
	public static string TutorialBuyBody => Ru ? "Купи Белого друга за монеты." : "Buy White Friend with coins.";
	public static string TutorialEquipTitle => Ru ? "Экипировка" : "Equip";
	public static string TutorialEquipBody => Ru ? "Надень Белого друга." : "Equip White Friend.";
	public static string TutorialSkinsReturnTitle => Ru ? "Закрой магазин" : "Close shop";
	public static string TutorialSkinsReturnBody => Ru
		? "Закрой магазин скинов."
		: "Close the skin shop.";

	public static string TutorialCleaningRulesTitle => Ru ? "Зачистка карты" : "Map cleaning";
	public static string TutorialCleaningRulesBody => Ru
		? "Таймер 3:00. Съешь крупные объекты — процент растёт. Дойди до 100% или дождись конца времени."
		: "Timer 3:00. Eat the large objects to raise the percent. Reach 100% or wait until time runs out.";

	public static string TutorialMoveTitle => Ru ? "Как двигаемся" : "How to move";
	public static string TutorialMoveBody => Ru
		? "Управляй джойстиком. На ПК — WASD."
		: "Use the joystick. On PC, use WASD.";

	public static string TutorialBoostTitle => Ru ? "Буст" : "Boost";
	public static string TutorialBoostBody => Ru
		? "Удерживай кнопку буста. На ПК — Shift или Пробел."
		: "Hold the boost button. On PC: Shift or Space.";

	public static string TutorialEatTitle => Ru ? "Поглощение" : "Absorb";
	public static string TutorialEatBody => Ru
		? "Наезжай на объект меньше дыры. Больше дыры — не поглотится."
		: "Drive over something smaller than the hole. Larger objects stay.";

	public static string TutorialMapTitle => Ru ? "Карта" : "Map";
	public static string TutorialMapBody => Ru
		? "Точки на миникарте — ты и цели. Стрелки показывают направление."
		: "Minimap dots are you and targets. Arrows show the direction.";

	public static string TutorialArrowsTitle => Ru ? "Стрелки" : "Arrows";
	public static string TutorialArrowsBody => Ru
		? "Стрелки ведут к ближайшим объектам, которые можно поглотить."
		: "Arrows point to the nearest objects you can absorb.";

	public static string TutorialEatHint => Ru
		? "Наезжай на объект меньше дыры."
		: "Drive over something smaller than the hole.";
	public static string TutorialEatDoneTitle => Ru ? "Готово" : "Done";
	public static string TutorialEatDoneBody => Ru
		? "Объект поглощён. Так растёт дыра."
		: "Object absorbed. That is how the hole grows.";
}
