using System.Collections.Generic;
using UnityEngine;
using YG;

public class HoleFeedback : MonoBehaviour
{
	public static HoleFeedback ForPlayer { get; private set; }

	private static readonly Color Water = new Color(0.12f, 0.42f, 0.92f, 0.92f);
	private static readonly Color WaterDeep = new Color(0.04f, 0.18f, 0.62f, 0.86f);
	private static readonly Color WaterWhite = new Color(0.72f, 0.9f, 1f, 0.95f);

	[SerializeField] private bool debugEmitOnStart;
	[Header("Suction Ribbons")]
	[SerializeField] private int ribbonCount = 5;
	[SerializeField] private float ribbonOrbitSpeed = 0.52f;
	[SerializeField] private float ribbonInwardSpeed = 0.3f;
	[SerializeField] private float ribbonTrailTime = 0.4f;
	[SerializeField] private float ribbonWidth = 0.08f;
	[SerializeField] private float ribbonOuterMul = 1.28f;
	[SerializeField] private float ribbonRespawnRadius = 0.12f;

	private HoleParent target;
	private Transform vfxRoot;
	private ParticleSystem suction;
	private ParticleSystem gulp;
	private ParticleSystem ring;
	private readonly List<Ribbon> ribbons = new List<Ribbon>(8);
	private Material ribbonMat;
	private int lastLevel;
	private Color borderRest = Color.white;
	private float borderFlashTimer;
	private Material billboardMat;
	private Texture2D billboardTexture;
	private bool matchEnded;
	private bool ygPaused;
	private float nextGulpTime;
	[SerializeField] private float gulpBatchInterval = 0.2f;

	private struct Ribbon
	{
		public Transform transform;
		public TrailRenderer trail;
		public float angle;
		public float radiusNorm;
	}

	public static HoleFeedback Ensure(HoleParent player)
	{
		if (player == null)
			return null;

		HoleFeedback feedback = player.GetComponent<HoleFeedback>();
		if (feedback == null)
			feedback = player.gameObject.AddComponent<HoleFeedback>();
		feedback.Bind(player);
		return feedback;
	}

	public void Bind(HoleParent player)
	{
		target = player;
		ForPlayer = this;
		matchEnded = false;
		ygPaused = false;
		lastLevel = player != null ? player.currentLevel : 0;
		if (player != null && player.border != null)
			borderRest = player.border.color;
		EnsureMaterials();
		EnsureSystems();
		ApplyPlaybackState();

		if (debugEmitOnStart)
			PlayGulp();
	}

	void OnEnable()
	{
		YG2.onPauseGame += HandleYgPause;
	}

	void OnDisable()
	{
		YG2.onPauseGame -= HandleYgPause;
	}

	public void SetMatchActive(bool active)
	{
		matchEnded = !active;
		ApplyPlaybackState();
	}

	private void HandleYgPause(bool paused)
	{
		ygPaused = paused;
		ApplyPlaybackState();
	}

	private void ApplyPlaybackState()
	{
		bool play = !matchEnded && !ygPaused;
		if (suction == null)
			return;

		if (play)
		{
			if (!suction.isPlaying)
				suction.Play();
			SetRibbonsActive(true);
		}
		else
		{
			suction.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
			if (gulp != null)
				gulp.Clear(true);
			if (ring != null)
				ring.Clear(true);
			SetRibbonsActive(false);
		}
	}

	void OnDestroy()
	{
		if (ForPlayer == this)
			ForPlayer = null;
		if (billboardMat != null)
			Destroy(billboardMat);
		if (ribbonMat != null)
			Destroy(ribbonMat);
		if (billboardTexture != null)
			Destroy(billboardTexture);
	}

	void LateUpdate()
	{
		if (target == null || matchEnded || ygPaused)
			return;

		UpdateSuction();
		UpdateRibbons();
		UpdateBorderFlash();

		while (target.currentLevel > lastLevel)
		{
			lastLevel++;
			PlayLevelUp();
		}
	}

	public void PlayGulp()
	{
		if (Time.unscaledTime < nextGulpTime)
			return;
		nextGulpTime = Time.unscaledTime + gulpBatchInterval;

		float radius = GetEffectRadius();
		float vfx = SkinStats.VfxMultiplier;
		Vector3 pos = GetHoleWorldPos();
		Vector3 bottom = pos + Vector3.down * Mathf.Max(0.08f, radius * 0.18f);
		EmitBurst(gulp, Mathf.RoundToInt(14 * vfx), bottom, WaterWhite, radius * 0.12f, 0.7f);
		EmitBurst(gulp, Mathf.RoundToInt(10 * vfx), pos, Water, radius * 0.09f, 0.85f);
		HoleCameraFollow.Punch(0.28f);
		AudioManager.PlayGulp();
	}

	public void PlayLevelUp()
	{
		float radius = GetEffectRadius();
		EmitRing(radius, Water, 36);
		FlashBorder();
		HoleCameraFollow.Punch(0.7f);
		AudioManager.PlayLevelUp();
	}

	public void PlayAbsorb(Color burstColor)
	{
		Color splash = Color.Lerp(burstColor, Water, 0.55f);
		splash.a = Mathf.Clamp01(Mathf.Max(burstColor.a, 0.8f));
		float radius = GetEffectRadius();
		EmitBurst(gulp, 48, GetHoleWorldPos(), splash, radius * 0.2f, 2.4f);
		EmitRing(radius * 1.2f, Water, 52);
		HoleCameraFollow.Punch(1.15f);
		AudioManager.PlayAbsorb();
	}

	private float GetEffectRadius()
	{
		if (target == null)
			return 0.35f;
		return Mathf.Max(0.35f, target.GetHoleRadius());
	}

	private void UpdateSuction()
	{
		if (suction == null)
			return;

		float radius = GetEffectRadius();
		float attractMul = GameBalance.Current != null ? GameBalance.Current.suctionAttractRadius : 1.1f;
		suction.transform.position = GetHoleWorldPos() + Vector3.up * 0.05f;
		var shape = suction.shape;
		shape.radius = radius * attractMul;

		var main = suction.main;
		main.startSize = radius * 0.1f;
		main.startColor = Water;

		int falling = target.nearbyFallingObjects != null ? target.nearbyFallingObjects.Count : 0;
		var emission = suction.emission;
		emission.rateOverTime = falling > 0 ? 28f : 12f;
	}

	private void EnsureRibbons()
	{
		if (vfxRoot == null)
			return;

		if (ribbonMat == null)
		{
			Shader shader = Shader.Find("Sprites/Default");
			if (shader == null)
				shader = FindParticleShader();
			if (shader != null)
			{
				ribbonMat = new Material(shader);
				ribbonMat.color = Color.white;
			}
		}

		int count = Mathf.Clamp(ribbonCount, 3, 5);
		while (ribbons.Count < count)
		{
			int index = ribbons.Count;
			GameObject go = new GameObject("SuctionRibbon_" + index);
			go.transform.SetParent(vfxRoot, false);
			TrailRenderer trail = go.AddComponent<TrailRenderer>();
			trail.time = ribbonTrailTime;
			trail.minVertexDistance = 0.02f;
			trail.emitting = true;
			trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			trail.receiveShadows = false;
			trail.numCapVertices = 2;
			trail.numCornerVertices = 2;
			trail.alignment = LineAlignment.View;
			trail.textureMode = LineTextureMode.Stretch;
			if (ribbonMat != null)
				trail.material = ribbonMat;

			Color color = Color.Lerp(WaterDeep, WaterWhite, (index % 3) * 0.45f);
			Gradient gradient = new Gradient();
			gradient.SetKeys(
				new[]
				{
					new GradientColorKey(color, 0f),
					new GradientColorKey(color, 1f)
				},
				new[]
				{
					new GradientAlphaKey(0.7f, 0f),
					new GradientAlphaKey(0f, 1f)
				});
			trail.colorGradient = gradient;
			trail.widthCurve = new AnimationCurve(
				new Keyframe(0f, ribbonWidth),
				new Keyframe(1f, 0f));

			ribbons.Add(new Ribbon
			{
				transform = go.transform,
				trail = trail,
				angle = (Mathf.PI * 2f * index) / count,
				radiusNorm = 0.55f + (index % 3) * 0.15f
			});
		}

		for (int i = 0; i < ribbons.Count; i++)
		{
			Ribbon ribbon = ribbons[i];
			if (ribbon.trail == null)
				continue;
			ribbon.trail.time = ribbonTrailTime;
			ribbon.trail.widthCurve = new AnimationCurve(
				new Keyframe(0f, ribbonWidth),
				new Keyframe(1f, 0f));
			ribbons[i] = ribbon;
		}
	}

	private void UpdateRibbons()
	{
		if (ribbons.Count == 0)
			return;

		Vector3 center = GetHoleWorldPos() + Vector3.up * 0.08f;
		float outer = GetEffectRadius() * ribbonOuterMul;
		float dt = Time.deltaTime;
		int falling = target != null && target.nearbyFallingObjects != null ? target.nearbyFallingObjects.Count : 0;
		float intensity = (falling > 0 ? 1.65f : 1f) * SkinStats.VfxMultiplier;

		for (int i = 0; i < ribbons.Count; i++)
		{
			Ribbon ribbon = ribbons[i];
			if (ribbon.transform == null)
				continue;

			ribbon.angle += ribbonOrbitSpeed * intensity * dt * (i % 2 == 0 ? 1f : -1f);
			ribbon.radiusNorm -= (ribbonInwardSpeed * intensity / Mathf.Max(0.35f, outer)) * dt;
			if (ribbon.radiusNorm <= ribbonRespawnRadius)
			{
				ribbon.radiusNorm = 1f;
				if (ribbon.trail != null)
					ribbon.trail.Clear();
			}

			float r = outer * Mathf.Clamp01(ribbon.radiusNorm);
			Vector3 pos = center + new Vector3(Mathf.Cos(ribbon.angle), 0f, Mathf.Sin(ribbon.angle)) * r;
			ribbon.transform.position = pos;
			ribbons[i] = ribbon;
		}
	}

	private void SetRibbonsActive(bool active)
	{
		for (int i = 0; i < ribbons.Count; i++)
		{
			Ribbon ribbon = ribbons[i];
			if (ribbon.transform == null)
				continue;
			if (ribbon.trail != null)
			{
				ribbon.trail.emitting = active;
				if (!active)
					ribbon.trail.Clear();
			}
			ribbon.transform.gameObject.SetActive(active);
		}
	}

	private void UpdateBorderFlash()
	{
		if (target.border == null || borderFlashTimer <= 0f)
			return;

		borderFlashTimer -= Time.deltaTime;
		float t = Mathf.Clamp01(borderFlashTimer / 0.22f);
		target.border.color = Color.Lerp(borderRest, Color.white, t);
	}

	private void FlashBorder()
	{
		if (target.border != null)
			borderRest = target.border.color;
		borderFlashTimer = 0.22f;
	}

	private Vector3 GetHoleWorldPos()
	{
		if (target.hole != null)
			return target.hole.transform.position;
		return target.transform.position;
	}

	private void EnsureMaterials()
	{
		if (billboardTexture == null)
			billboardTexture = CreateSoftCircleTexture();
		if (billboardMat == null)
			billboardMat = CreateParticleMaterial(billboardTexture, "Legacy Shaders/Particles/Additive");
		else
			ApplyParticleMaterial(billboardMat, billboardTexture);
	}

	private void EnsureSystems()
	{
		if (vfxRoot == null)
		{
			Transform existing = transform.Find("HoleVfx");
			if (existing != null)
				vfxRoot = existing;
			else
			{
				GameObject rootGo = new GameObject("HoleVfx");
				vfxRoot = rootGo.transform;
				vfxRoot.SetParent(transform, false);
			}
		}

		if (suction == null)
			suction = CreateSystem("SuctionDust", true, 96, false);
		ConfigureSuction();

		if (gulp == null)
			gulp = CreateSystem("GulpBurst", false, 80, false);
		ConfigureGulp();

		if (ring == null)
			ring = CreateSystem("LevelRing", false, 96, false);
		ConfigureRing();
		EnsureRibbons();
	}

	private void ConfigureSuction()
	{
		if (suction == null)
			return;

		var main = suction.main;
		main.startLifetime = 0.85f;
		main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
		main.startSize = 0.12f;
		main.startColor = Water;
		main.gravityModifier = 0.12f;
		var shape = suction.shape;
		shape.shapeType = ParticleSystemShapeType.Circle;
		shape.radius = 0.4f;
		var vel = suction.velocityOverLifetime;
		vel.enabled = true;
		vel.radial = new ParticleSystem.MinMaxCurve(-3.4f);
		ConfigureBillboardRenderer(suction.GetComponent<ParticleSystemRenderer>());
		if (!suction.isPlaying)
			suction.Play();
	}

	private void ConfigureGulp()
	{
		if (gulp == null)
			return;

		var main = gulp.main;
		main.startLifetime = 0.38f;
		main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.85f);
		main.startSize = 0.15f;
		main.startColor = WaterWhite;
		main.gravityModifier = 0.45f;
		var emission = gulp.emission;
		emission.rateOverTime = 0f;
		var shape = gulp.shape;
		shape.shapeType = ParticleSystemShapeType.Hemisphere;
		shape.radius = 0.15f;
		ConfigureBillboardRenderer(gulp.GetComponent<ParticleSystemRenderer>());
	}

	private void ConfigureRing()
	{
		if (ring == null)
			return;

		var main = ring.main;
		main.startLifetime = 0.55f;
		main.startSpeed = 1.8f;
		main.startSize = 0.18f;
		main.startColor = Water;
		main.gravityModifier = 0f;
		var emission = ring.emission;
		emission.rateOverTime = 0f;
		var shape = ring.shape;
		shape.shapeType = ParticleSystemShapeType.Circle;
		shape.radius = 0.5f;
		var vel = ring.velocityOverLifetime;
		vel.enabled = true;
		vel.radial = new ParticleSystem.MinMaxCurve(1.4f);
		ConfigureBillboardRenderer(ring.GetComponent<ParticleSystemRenderer>());
	}

	private ParticleSystem CreateSystem(string name, bool loop, int maxParticles, bool useMesh)
	{
		Transform existing = vfxRoot.Find(name);
		if (existing != null)
			return existing.GetComponent<ParticleSystem>();

		GameObject go = new GameObject(name, typeof(ParticleSystem));
		go.transform.SetParent(vfxRoot, false);
		go.transform.localPosition = Vector3.zero;
		go.transform.localRotation = useMesh ? Quaternion.identity : Quaternion.Euler(-90f, 0f, 0f);

		ParticleSystem ps = go.GetComponent<ParticleSystem>();
		var main = ps.main;
		main.loop = loop;
		main.playOnAwake = loop;
		main.maxParticles = maxParticles;
		main.simulationSpace = ParticleSystemSimulationSpace.World;
		main.scalingMode = ParticleSystemScalingMode.Local;
		return ps;
	}

	private void ConfigureBillboardRenderer(ParticleSystemRenderer rend)
	{
		if (rend == null)
			return;

		rend.renderMode = ParticleSystemRenderMode.Billboard;
		if (billboardMat != null)
			rend.material = billboardMat;
		rend.maxParticleSize = 4f;
		rend.sortingFudge = -2f;
	}

	private void EmitBurst(ParticleSystem ps, int count, Vector3 worldPos, Color color, float size, float speed)
	{
		if (ps == null)
			return;

		ps.transform.position = worldPos;
		var main = ps.main;
		main.startColor = color;
		main.startSize = size;
		main.startSpeed = speed;
		ps.Emit(count);
	}

	private void EmitRing(float radius, Color color, int count)
	{
		if (ring == null)
			return;

		ring.transform.position = GetHoleWorldPos() + Vector3.up * 0.04f;
		var shape = ring.shape;
		shape.radius = radius;
		var main = ring.main;
		main.startColor = color;
		main.startSize = radius * 0.14f;
		ring.Emit(count);
	}

	private static Shader FindParticleShader()
	{
		Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
		if (shader == null)
			shader = Shader.Find("Mobile/Particles/Additive");
		if (shader == null)
			shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
		if (shader == null)
			shader = Shader.Find("Mobile/Particles/Alpha Blended");
		if (shader == null)
			shader = Shader.Find("Particles/Standard Unlit");
		return shader;
	}

	private static Material CreateParticleMaterial(Texture2D texture, string fallbackShader)
	{
		Shader shader = FindParticleShader();
		if (shader == null)
			shader = Shader.Find(fallbackShader);
		if (shader == null)
			return null;

		Material mat = new Material(shader);
		ApplyParticleMaterial(mat, texture);
		return mat;
	}

	private static void ApplyParticleMaterial(Material mat, Texture2D texture)
	{
		if (mat == null)
			return;

		Shader shader = FindParticleShader();
		if (shader != null)
			mat.shader = shader;
		if (texture != null)
			mat.mainTexture = texture;
		if (mat.HasProperty("_TintColor"))
			mat.SetColor("_TintColor", Water);
		if (mat.HasProperty("_Color"))
			mat.SetColor("_Color", Water);
	}

	private static Texture2D CreateSoftCircleTexture()
	{
		const int size = 64;
		Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
		Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
		float radius = size * 0.5f;
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float dist = Vector2.Distance(new Vector2(x, y), center) / radius;
				float alpha = Mathf.Clamp01(1f - dist);
				alpha *= alpha;
				float spark = Mathf.Clamp01(1f - dist * 2.4f);
				spark *= spark;
				Color pixel = Color.Lerp(WaterDeep, WaterWhite, spark);
				pixel.a = Mathf.Max(alpha, spark * 0.85f);
				tex.SetPixel(x, y, pixel);
			}
		}
		tex.Apply(false, false);
		return tex;
	}
}
