using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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
		SetSFXVolume(YG2.saves.soundValue);
		SetMusicVolume(YG2.saves.musicValue);

		if (SoundSlider != null) SoundSlider.onValueChanged.AddListener(SetSFXVolume);
		if (MusicSlider != null) MusicSlider.onValueChanged.AddListener(SetMusicVolume);
		if (DSoundSlider != null) DSoundSlider.onValueChanged.AddListener(SetSFXVolume);
		if (DMusicSlider != null) DMusicSlider.onValueChanged.AddListener(SetMusicVolume);

		BindSaveOnPointerUp(SoundSlider);
		BindSaveOnPointerUp(MusicSlider);
		BindSaveOnPointerUp(DSoundSlider);
		BindSaveOnPointerUp(DMusicSlider);
	}

	private void BindSaveOnPointerUp(Slider slider)
	{
		if (slider == null)
			return;

		EventTrigger trigger = slider.GetComponent<EventTrigger>();
		if (trigger == null)
			trigger = slider.gameObject.AddComponent<EventTrigger>();

		EventTrigger.Entry entry = new EventTrigger.Entry
		{
			eventID = EventTriggerType.PointerUp
		};
		entry.callback.AddListener(_ => YG2.SaveProgress());
		trigger.triggers.Add(entry);
	}

	private void SetSFXVolume(float val)
	{
		if (SoundSlider != null) SoundSlider.value = val;
		if (DSoundSlider != null) DSoundSlider.value = val;
		YG2.saves.soundValue = val;
		dbValSound = Mathf.Log10(Mathf.Clamp(val, 0.001f, 1f)) * 20;
		if (mixer != null)
			mixer.SetFloat("SFXVol", dbValSound);

		bool muted = dbValSound <= -60;
		if (krest1 != null) krest1.SetActive(muted);
		if (Dkrest1 != null) Dkrest1.SetActive(muted);
	}

	private void SetMusicVolume(float val)
	{
		if (MusicSlider != null) MusicSlider.value = val;
		if (DMusicSlider != null) DMusicSlider.value = val;
		YG2.saves.musicValue = val;
		dbValMusic = Mathf.Log10(Mathf.Clamp(val, 0.001f, 1f)) * 20;
		if (mixer != null)
			mixer.SetFloat("MusicVol", dbValMusic);

		bool muted = dbValMusic <= -60;
		if (krest2 != null) krest2.SetActive(muted);
		if (Dkrest2 != null) Dkrest2.SetActive(muted);
	}
}
