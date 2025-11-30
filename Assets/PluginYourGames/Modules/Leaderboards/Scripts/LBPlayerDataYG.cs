using System;
using UnityEngine;
using UnityEngine.UI;
#if TMP_YG2
using TMPro;
#endif

namespace YG
{
    public class LBPlayerDataYG : MonoBehaviour
    {
        public ImageLoadYG imageLoad;

        [Serializable]
        public struct TextLegasy
        {
            public Text rank, name, score;
        }
        public TextLegasy textLegasy;

#if TMP_YG2
        [Serializable]
        public struct TextMP
        {
            public TextMeshProUGUI rank, name, score;
        }
        public TextMP textMP;
#endif
        [Space(10)]
        public MonoBehaviour[] topPlayerActivityComponents = new MonoBehaviour[0];
        public MonoBehaviour[] currentPlayerActivityComponents = new MonoBehaviour[0];

        [Header("Custom Highlighting")]
        public Image backgroundImg;         // Фон строки для цвета (золото/серебро/бронза)
        public Image medalImg;              // Иконка медали (опционально, если добавишь спрайты)
        public LayoutElement layoutElement; // Для контроля высоты строки (пробелы/отступы)

        public class Data
        {
            public string rank;
            public string name;
            public string score;
            public string photoUrl;
            public bool inTop;
            public bool currentPlayer;
            public Sprite photoSprite;
        }

        [HideInInspector]
        public Data data = new Data();

        public void UpdateEntries()
        {
            if (textLegasy.rank && data.rank != null) textLegasy.rank.text = data.rank.ToString();
            if (textLegasy.name && data.name != null) textLegasy.name.text = data.name;
            if (textLegasy.score && data.score != null) textLegasy.score.text = data.score.ToString();

#if TMP_YG2
            if (textMP.rank && data.rank != null) textMP.rank.text = data.rank.ToString();
            if (textMP.name && data.name != null) textMP.name.text = data.name;
            if (textMP.score && data.score != null) textMP.score.text = data.score.ToString();
#endif
            if (imageLoad)
            {
                if (data.photoSprite)
                {
                    imageLoad.SetTexture(data.photoSprite.texture);
                }
                else if (data.photoUrl == null)
                {
                    imageLoad.ClearTexture();
                }
                else
                {
                    imageLoad.Load(data.photoUrl);
                }
            }

            if (topPlayerActivityComponents.Length > 0)
            {
                if (data.inTop)
                {
                    ActivityMomoObjects(topPlayerActivityComponents, true);
                }
                else
                {
                    ActivityMomoObjects(topPlayerActivityComponents, false);
                }
            }

            if (currentPlayerActivityComponents.Length > 0)
            {
                if (data.currentPlayer)
                {
                    ActivityMomoObjects(currentPlayerActivityComponents, true);
                }
                else
                {
                    ActivityMomoObjects(currentPlayerActivityComponents, false);
                }
            }

            // Новые методы для выделения топ-3
            SetHighlight();
            SetSpacing();

            void ActivityMomoObjects(MonoBehaviour[] objects, bool activity)
            {
                for (int i = 0; i < objects.Length; i++)
                {
                    objects[i].enabled = activity;
                }
            }
        }

        private void SetHighlight()
        {
            if (!int.TryParse(data.rank, out int rank)) return; // Если rank не число, пропустить

            // Цвета фона
            if (backgroundImg != null)
            {
                if (rank == 1)
                {
                    backgroundImg.color = new Color(1f, 0.84f, 0f); // Золото (#FFD700)
                }
                else if (rank == 2)
                {
                    backgroundImg.color = new Color(0.75f, 0.75f, 0.75f); // Серебро (#C0C0C0)
                }
                else if (rank == 3)
                {
                    backgroundImg.color = new Color(0.8f, 0.5f, 0.2f); // Бронза (#CD7F32)
                }
                else
                {
                    backgroundImg.color = Color.white; // Обычный фон
                }
            }

            // Контраст текста ранга (опционально, подкорректируй под свои цвета)
            Color rankColor = (rank <= 3) ? Color.black : Color.black; // Пример: чёрный для топ-3
#if TMP_YG2
            if (textMP.rank != null) textMP.rank.color = rankColor;
#endif
            if (textLegasy.rank != null) textLegasy.rank.color = rankColor;

            // Медали (если medalImg подключён и спрайты готовы в Resources)
            if (medalImg != null)
            {
                medalImg.gameObject.SetActive(rank <= 3);
                if (rank == 1) medalImg.sprite = Resources.Load<Sprite>("GoldMedal");
                else if (rank == 2) medalImg.sprite = Resources.Load<Sprite>("SilverMedal");
                else if (rank == 3) medalImg.sprite = Resources.Load<Sprite>("BronzeMedal");
            }
        }

        private void SetSpacing()
        {
            if (layoutElement == null) layoutElement = GetComponent<LayoutElement>();
            if (layoutElement == null) return; // Если нет, пропустить

            if (!int.TryParse(data.rank, out int rank)) return;

            if (rank <= 3)
            {
                layoutElement.minHeight = 100f; // Больше высота для топ-3 (визуальный пробел)
                layoutElement.preferredHeight = 100f;
            }
            else
            {
                layoutElement.minHeight = 60f; // Стандарт для остальных
                layoutElement.preferredHeight = 60f;
            }
        }
    }
}