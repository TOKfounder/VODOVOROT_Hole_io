using UnityEngine;
using UnityEngine.UI;

public class PointsScript : MonoBehaviour
{
    [Header("Настройки анимации")]
    public float moveSpeed = 50f;
    public float duration = 1f;

    private Text txt;
    private Color startColor;
    private float time;

    private void Awake()
    {
        txt = GetComponent<Text>();
        if (txt != null)
            startColor = txt.color;
    }

    /// <summary>
    /// Вызывается из HoleParent при получении объекта из пула
    /// </summary>
    public void OnSpawn(int amount)
    {
        if (txt == null)
            txt = GetComponent<Text>();

        time = 0f;
        gameObject.SetActive(true);

        if (txt != null)
        {
            txt.text = $"+{amount}";
            txt.color = startColor;
        }
    }

    void Update()
    {
        if (time < duration)
        {
            transform.Translate(Vector3.up * moveSpeed * Time.deltaTime);
            time += Time.deltaTime;

            if (txt != null)
            {
                float alpha = 1f - time / duration;
                txt.color = new Color(txt.color.r, txt.color.g, txt.color.b, alpha);
            }
        }
        else
        {
            // Возвращаем в пул вместо Destroy
            if (HoleParent.Instance != null)
                HoleParent.Instance.ReturnPointsToPool(gameObject);
            else
                gameObject.SetActive(false);
        }
    }
}