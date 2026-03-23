using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using YG;

public class AudioManager : MonoBehaviour
{
	public static AudioManager Instance;

	public AudioSource musicSource;
	public AudioMixer mixer;

	public float dbValSound;
	public float dbValMusic;

[Header("Mobile Objs")]
	public GameObject krest1;
	public GameObject krest2;
	public Slider SoundSlider;
	public Slider MusicSlider;
[Header("Desktop Objs")]
	public GameObject Dkrest1;
	public GameObject Dkrest2;
	public Slider DSoundSlider;
	public Slider DMusicSlider;
	void Awake()
	{
		Instance = this;
	}

	void Start()
	{
		SetMusicVolume(YG2.saves.musicValue);
		SetSFXVolume(YG2.saves.soundValue);
		SoundSlider.onValueChanged.AddListener(SetSFXVolume);
		MusicSlider.onValueChanged.AddListener(SetMusicVolume);
		DSoundSlider.onValueChanged.AddListener(SetSFXVolume);
		DMusicSlider.onValueChanged.AddListener(SetMusicVolume);
	}

	private void SetSFXVolume(float val)
	{
		SoundSlider.value = val;
		DSoundSlider.value = val;
		YG2.saves.soundValue = val;
		dbValSound = Mathf.Log10(Mathf.Clamp(val, 0.001f, 1f)) * 20;
		mixer.SetFloat("SFXVol", dbValSound);
		if (dbValSound <= -60)
		{
			krest1.SetActive(true);
			Dkrest1.SetActive(true);
		}
		else
		{
			krest1.SetActive(false);
			Dkrest1.SetActive(false);
		}
		YG2.SaveProgress();
	}

	private void SetMusicVolume(float val) // Значение от 0 до 1
	{
		MusicSlider.value = val;
		DMusicSlider.value = val;
		YG2.saves.musicValue = val;
		dbValMusic = Mathf.Log10(Mathf.Clamp(val, 0.001f, 1f)) * 20;
		mixer.SetFloat("MusicVol", dbValMusic);
		if (dbValMusic <= -60)
		{
			krest2.SetActive(true);
			Dkrest2.SetActive(true);
		}
		else
		{
			krest2.SetActive(false);
			Dkrest2.SetActive(false);
		}
		YG2.SaveProgress();
	}
}
