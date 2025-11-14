// using UnityEngine;
// using UnityEngine.UI;
// using YG;
// using YG.Utils.LB;

// public class CustomLeaderboard : MonoBehaviour
// {
//     public string leaderboardName = "MainLB";
    
//     // Твои UI элементы для отображения топ-3, текущего и окружения
//     public Text top1Text, top2Text, top3Text;
//     public Text currentPlayerText;
//     public Text close1Text, close2Text, close3Text;

//     void Start()
//     {
//         // Запустить обновление таблицы
//         YG2.GetLeaderboard(leaderboardName);
//         // Подписываемся на событие получения данных
//         YG2.onGetLeaderboard += OnLeaderboardReturned;
//     }

//     void OnDisable()
//     {
//         YG2.onGetLeaderboard -= OnLeaderboardReturned;
//     }

//     void OnLeaderboardReturned(LBData lbData)
//     {
//         // 1. Топ-3
//         if (lbData.players.Length > 0) top1Text.text = $"{lbData.players[0].rank}. {lbData.players[0].name}: {lbData.players[0].score}";
//         if (lbData.players.Length > 1) top2Text.text = $"{lbData.players[1].rank}. {lbData.players[1].name}: {lbData.players[1].score}";
//         if (lbData.players.Length > 2) top3Text.text = $"{lbData.players[2].rank}. {lbData.players[2].name}: {lbData.players[2].score}";

//         // 2. Текущий игрок
//         LBPlayerData current = null;
//         int playerIdx = -1;
//         for (int i = 0; i < lbData.players.Length; i++)
//         {
//             if (lbData.players[i].uniqueID == lbData.currentPlayer.uniqueID)
//             {
//                 current = lbData.players[i];
//                 playerIdx = i;
//                 break;
//             }
//         }
//         if (current != null)
//             currentPlayerText.text = $"Вы: {current.rank}. {current.name}: {current.score}";

//         // 3. Ближайшие соперники (окружающие)
//         // Покажем троих соседей (до или после), если есть
//         for (int j = -1; j <= 1; j++)
//         {
//             if (j == 0) continue; // пропускаем самого игрока
//             int idx = playerIdx + j;
//             if (idx >= 0 && idx < lbData.players.Length)
//             {
//                 string txt = $"{lbData.players[idx].rank}. {lbData.players[idx].name}: {lbData.players[idx].score}";
//                 if (j == -1) close1Text.text = txt;
//                 if (j == 1) close2Text.text = txt;
//             }
//         }
//         // Если мало игроков, пустые тексты можно очистить.
//         close3Text.text = ""; // можно не использовать, если только два "окружающих"
//     }
// }
