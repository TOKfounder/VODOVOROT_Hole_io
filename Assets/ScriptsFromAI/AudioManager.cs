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

	[Header("Match SFX")]
	[SerializeField] private AudioSource sfxSource;
	[SerializeField] private AudioClip gulpClip;
	[SerializeField] private AudioClip levelUpClip;
	[SerializeField] private AudioClip absorbClip;
	[SerializeField] private float gulpCooldown = 0.1f;
	[SerializeField] private float absorbCooldown = 0.15f;

	private float nextGulpTime;
	private float nextAbsorbTime;

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
		if (sfxSource == null)
		{
			sfxSource = gameObject.AddComponent<AudioSource>();
			sfxSource.playOnAwake = false;
		}

		sfxSource.spatialBlend = 0f;
		sfxSource.dopplerLevel = 0f;
		sfxSource.spread = 0f;
		sfxSource.rolloffMode = AudioRolloffMode.Linear;

		if (mixer != null && sfxSource.outputAudioMixerGroup == null)
		{
			AudioMixerGroup[] groups = mixer.FindMatchingGroups("SFX");
			if (groups != null && groups.Length > 0)
				sfxSource.outputAudioMixerGroup = groups[0];
		}

		EnsureMatchClips();
	}

	private void EnsureMatchClips()
	{
		if (gulpClip == null)
			gulpClip = LoadMatchClip("151233__owlstorm__gulp-2");
		if (levelUpClip == null)
			levelUpClip = LoadMatchClip("320655__rhodesmas__level-up-01");
		if (absorbClip == null)
			absorbClip = LoadMatchClip("vacuum");
	}

	private static AudioClip LoadMatchClip(string fileName)
	{
#if UNITY_EDITOR
		return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/SoundsAndMelodies/{fileName}.wav");
#else
		return null;
#endif
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
		StartMusic();
	}

	public void StartMusic()
	{
		if (musicSource == null || musicSource.clip == null)
			return;
		if (!musicSource.isPlaying)
			musicSource.Play();
	}

	public void StopMusic()
	{
		if (musicSource == null)
			return;
		musicSource.Stop();
	}

	public static void PlayGulp()
	{
		if (Instance == null || Time.unscaledTime < Instance.nextGulpTime)
			return;

		Instance.nextGulpTime = Time.unscaledTime + Instance.gulpCooldown;
		Instance.PlaySfx(Instance.gulpClip, 0.12f);
	}

	public static void PlayLevelUp()
	{
		if (Instance != null)
			Instance.PlaySfx(Instance.levelUpClip, 0.05f);
	}

	public static void PlayAbsorb()
	{
		if (Instance == null || Time.unscaledTime < Instance.nextAbsorbTime)
			return;

		Instance.nextAbsorbTime = Time.unscaledTime + Instance.absorbCooldown;
		Instance.PlaySfx(Instance.absorbClip, 0.06f);
	}

	public static void PlayUiClick()
	{
		if (MainMenuController.Instance != null && MainMenuController.Instance.dzyn != null)
		{
			MainMenuController.Instance.dzyn.Play();
			return;
		}

		if (Instance != null && Instance.sfxSource != null && Instance.sfxSource.clip != null)
			Instance.sfxSource.PlayOneShot(Instance.sfxSource.clip);
	}

	public void PlaySfx(AudioClip clip, float pitchVariance = 0.08f)
	{
		if (clip == null || sfxSource == null)
			return;

		sfxSource.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
		sfxSource.PlayOneShot(clip);
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
