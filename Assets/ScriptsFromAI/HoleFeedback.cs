using UnityEngine;
using YG;

public class HoleFeedback : MonoBehaviour
{
	public static HoleFeedback ForPlayer { get; private set; }

	private static readonly Color Water = new Color(0.25f, 0.78f, 0.92f, 0.85f);
	private static readonly Color VoidShadow = new Color(0.12f, 0.14f, 0.18f, 0.85f);
	private static readonly Color Gold = new Color(1f, 0.82f, 0.22f, 0.9f);
	private static readonly Color ParticleTint = new Color(0.55f, 0.58f, 0.62f, 0.45f);

	[SerializeField] private bool debugEmitOnStart;

	private HoleParent target;
	private Transform vfxRoot;
	private ParticleSystem suction;
	private ParticleSystem gulp;
	private ParticleSystem ring;
	private int lastLevel;
	private Color borderRest = Color.white;
	private float borderFlashTimer;
	private Material billboardMat;
	private Material meshMat;
	private Mesh sphereMesh;
	private Texture2D billboardTexture;
	private bool ownsSphereMesh;
	private bool matchEnded;
	private bool ygPaused;

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
		}
		else
		{
			suction.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
			if (gulp != null)
				gulp.Clear(true);
			if (ring != null)
				ring.Clear(true);
		}
	}

	void OnDestroy()
	{
		if (ForPlayer == this)
			ForPlayer = null;
		if (billboardMat != null)
			Destroy(billboardMat);
		if (meshMat != null)
			Destroy(meshMat);
		if (billboardTexture != null)
			Destroy(billboardTexture);
		if (ownsSphereMesh && sphereMesh != null)
			Destroy(sphereMesh);
	}

	void LateUpdate()
	{
		if (target == null || matchEnded || ygPaused)
			return;

		UpdateSuction();
		UpdateBorderFlash();

		while (target.currentLevel > lastLevel)
		{
			lastLevel++;
			PlayLevelUp();
		}
	}

	public void PlayGulp()
	{
		float radius = GetEffectRadius();
		Vector3 pos = GetHoleWorldPos();
		EmitBurst(gulp, 24, pos, Water, radius * 0.15f, 1.6f);
		EmitBurst(gulp, 16, pos, VoidShadow, radius * 0.1f, 2.1f);
		HoleCameraFollow.Punch(0.28f);
		AudioManager.PlayGulp();
	}

	public void PlayLevelUp()
	{
		float radius = GetEffectRadius();
		EmitRing(radius, Gold, 40);
		FlashBorder();
		HoleCameraFollow.Punch(0.7f);
		AudioManager.PlayLevelUp();
	}

	public void PlayAbsorb(Color burstColor)
	{
		float radius = GetEffectRadius();
		EmitBurst(gulp, 40, GetHoleWorldPos(), burstColor, radius * 0.2f, 2.4f);
		EmitRing(radius * 1.15f, burstColor, 48);
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
		suction.transform.position = GetHoleWorldPos() + Vector3.up * 0.05f;
		var shape = suction.shape;
		shape.radius = radius;

		var main = suction.main;
		main.startSize = radius * 0.1f;

		int falling = target.nearbyFallingObjects != null ? target.nearbyFallingObjects.Count : 0;
		var emission = suction.emission;
		emission.rateOverTime = falling > 0 ? 56f : 20f;
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
			billboardMat = CreateBillboardMaterial(billboardTexture);
		if (meshMat == null)
			meshMat = CreateMeshMaterial();
		if (sphereMesh == null)
			sphereMesh = CreateSphereMesh(out ownsSphereMesh);
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
		{
			suction = CreateSystem("SuctionDust", true, 96, false);
			var main = suction.main;
			main.startLifetime = 0.55f;
			main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
			main.startSize = 0.12f;
			main.startColor = Water;
			main.gravityModifier = 0.15f;
			var shape = suction.shape;
			shape.shapeType = ParticleSystemShapeType.Circle;
			shape.radius = 0.4f;
			var vel = suction.velocityOverLifetime;
			vel.enabled = true;
			vel.radial = new ParticleSystem.MinMaxCurve(-1.8f);
			ConfigureBillboardRenderer(suction.GetComponent<ParticleSystemRenderer>());
			suction.Play();
		}

		if (gulp == null)
		{
			gulp = CreateSystem("GulpBurst", false, 80, true);
			var main = gulp.main;
			main.startLifetime = 0.45f;
			main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
			main.startSize = 0.15f;
			main.startColor = VoidShadow;
			main.gravityModifier = 0.8f;
			var emission = gulp.emission;
			emission.rateOverTime = 0f;
			var shape = gulp.shape;
			shape.shapeType = ParticleSystemShapeType.Hemisphere;
			shape.radius = 0.15f;
			ConfigureMeshRenderer(gulp.GetComponent<ParticleSystemRenderer>());
		}

		if (ring == null)
		{
			ring = CreateSystem("LevelRing", false, 96, true);
			var main = ring.main;
			main.startLifetime = 0.55f;
			main.startSpeed = 1.8f;
			main.startSize = 0.18f;
			main.startColor = VoidShadow;
			main.gravityModifier = 0f;
			var emission = ring.emission;
			emission.rateOverTime = 0f;
			var shape = ring.shape;
			shape.shapeType = ParticleSystemShapeType.Circle;
			shape.radius = 0.5f;
			var vel = ring.velocityOverLifetime;
			vel.enabled = true;
			vel.radial = new ParticleSystem.MinMaxCurve(1.4f);
			ConfigureMeshRenderer(ring.GetComponent<ParticleSystemRenderer>());
		}
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

	private void ConfigureMeshRenderer(ParticleSystemRenderer rend)
	{
		if (rend == null)
			return;

		rend.renderMode = ParticleSystemRenderMode.Mesh;
		if (sphereMesh != null)
			rend.mesh = sphereMesh;
		if (meshMat != null)
			rend.material = meshMat;
		rend.maxParticleSize = 4f;
		rend.sortingFudge = -1f;
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

	private static Material CreateBillboardMaterial(Texture2D texture)
	{
		Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
		if (shader == null)
			shader = Shader.Find("Mobile/Particles/Additive");
		if (shader == null)
			shader = Shader.Find("Particles/Standard Unlit");
		if (shader == null)
			shader = Shader.Find("Sprites/Default");
		if (shader == null)
			return null;

		Material mat = new Material(shader);
		if (texture != null)
			mat.mainTexture = texture;
		if (mat.HasProperty("_TintColor"))
			mat.SetColor("_TintColor", ParticleTint);
		return mat;
	}

	private static Material CreateMeshMaterial()
	{
		Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
		if (shader == null)
			shader = Shader.Find("Mobile/Particles/Additive");
		if (shader == null)
			shader = Shader.Find("Particles/Standard Unlit");
		if (shader == null)
			shader = Shader.Find("Unlit/Color");
		if (shader == null)
			return null;

		Material mat = new Material(shader);
		if (mat.HasProperty("_TintColor"))
			mat.SetColor("_TintColor", ParticleTint);
		return mat;
	}

	private static Mesh CreateSphereMesh(out bool ownsMesh)
	{
		Mesh builtin = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
		if (builtin != null)
		{
			ownsMesh = false;
			return builtin;
		}

		GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		Mesh source = temp.GetComponent<MeshFilter>().sharedMesh;
		Mesh copy = new Mesh();
		copy.vertices = source.vertices;
		copy.triangles = source.triangles;
		copy.normals = source.normals;
		copy.uv = source.uv;
		copy.RecalculateBounds();
		Destroy(temp);
		ownsMesh = true;
		return copy;
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
				tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
			}
		}
		tex.Apply(false, false);
		return tex;
	}
}
