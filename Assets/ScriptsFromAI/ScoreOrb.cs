using System.Collections;
using UnityEngine;

public class ScoreOrb : FallingObject
{
	private static readonly int ColorId = Shader.PropertyToID("_Color");

	[SerializeField] private float collectDelay = 0.9f;
	[SerializeField] private float hopDuration = 0.42f;
	[SerializeField] private float risePhaseRatio = 0.35f;

	private ScoreOrbSpawner owner;
	private float spawnTime;
	private bool armed;
	private bool settled;
	private Vector3 restPosition;
	private Collider pendingTrigger;
	private MaterialPropertyBlock colorBlock;
	private Coroutine hopRoutine;

	protected override bool AssignValueFromVolume => false;
	protected override bool CountsTowardMapTotal => false;
	protected override bool ResetsIfNotScored => false;
	protected override bool IgnoresMapOnStart => false;
	protected override int ObjectLayer => 7;

	void Update()
	{
		if (!armed || owner == null)
			return;

		if (pendingTrigger != null && !IsPendingTriggerValid())
			pendingTrigger = null;

		if (settled && pendingTrigger != null && CanCollect)
		{
			Collider trigger = pendingTrigger;
			pendingTrigger = null;
			TryBeginFall(trigger);
		}
	}

	private bool CanCollect => settled && Time.time - spawnTime >= collectDelay;

	public void Launch(ScoreOrbSpawner spawner, int orbValue, Color tint, Vector3 holeOrigin, Vector3 rest, float popHeight, float scale)
	{
		owner = spawner;
		value = Mathf.Max(1, orbValue);
		armed = true;
		settled = false;
		spawnTime = Time.time;
		isTriggered = false;
		CurrentHole = null;
		pendingTrigger = null;

		if (hopRoutine != null)
			StopCoroutine(hopRoutine);

		restPosition = rest;
		gameObject.layer = ObjectLayer;
		transform.position = new Vector3(rest.x, holeOrigin.y, rest.z);
		transform.rotation = Quaternion.identity;
		transform.localScale = Vector3.one * Mathf.Max(0.12f, scale);
		if (col != null)
			col.enabled = true;
		if (rend != null)
		{
			rend.enabled = true;
			SetOrbColor(tint);
		}

		RefreshMetrics();

		if (rb != null)
		{
			rb.velocity = Vector3.zero;
			rb.angularVelocity = Vector3.zero;
			rb.useGravity = false;
			rb.isKinematic = true;
		}

		hopRoutine = StartCoroutine(PopFromHole(holeOrigin, rest, popHeight));
	}

	private IEnumerator PopFromHole(Vector3 holeOrigin, Vector3 rest, float popHeight)
	{
		Vector3 start = new Vector3(rest.x, holeOrigin.y, rest.z);
		Vector3 apex = new Vector3(rest.x, holeOrigin.y + popHeight, rest.z);
		float totalDuration = Mathf.Max(0.2f, hopDuration);
		float riseDuration = totalDuration * risePhaseRatio;
		float fallDuration = totalDuration - riseDuration;

		float elapsed = 0f;
		while (elapsed < riseDuration)
		{
			elapsed += Time.deltaTime;
			float t = Mathf.Clamp01(elapsed / riseDuration);
			float eased = 1f - (1f - t) * (1f - t);
			MoveTo(Vector3.Lerp(start, apex, eased));
			yield return null;
		}

		elapsed = 0f;
		while (elapsed < fallDuration)
		{
			elapsed += Time.deltaTime;
			float t = Mathf.Clamp01(elapsed / fallDuration);
			float eased = t * t;
			MoveTo(Vector3.Lerp(apex, rest, eased));
			yield return null;
		}

		MoveTo(rest);
		Physics.SyncTransforms();
		settled = true;
		hopRoutine = null;
	}

	private void MoveTo(Vector3 position)
	{
		if (rb != null)
			rb.MovePosition(position);
		else
			transform.position = position;
	}

	protected override void OnSuctionBegan(HoleParent hole)
	{
		if (hopRoutine != null)
		{
			StopCoroutine(hopRoutine);
			hopRoutine = null;
		}

		settled = true;
		if (rb != null)
		{
			rb.isKinematic = false;
			rb.useGravity = true;
		}
		IgnoreMapPlatforms();
		pendingTrigger = null;
	}

	protected override void OnTriggerEnter(Collider other)
	{
		if (!armed || !settled)
			return;

		if (!CanCollect)
		{
			if (other.CompareTag("Player"))
				pendingTrigger = other;
			return;
		}

		TryBeginFall(other);
	}

	protected override void TryBeginFall(Collider other)
	{
		if (!other.CompareTag("Player"))
			return;

		HoleParent otherHole = other.GetComponentInParent<HoleParent>();
		if (otherHole == null || otherHole.holeType != HoleParent.TypeOfHole.player)
			return;

		if (isTriggered)
			return;

		CurrentHole = otherHole;
		isTriggered = true;
		if (rb != null)
		{
			rb.isKinematic = false;
			rb.useGravity = true;
		}
		if (!CurrentHole.nearbyFallingObjects.Contains(this))
			CurrentHole.nearbyFallingObjects.Add(this);
		OnSuctionBegan(CurrentHole);
	}

	void OnTriggerExit(Collider other)
	{
		if (pendingTrigger == other)
			pendingTrigger = null;
	}

	public override void ResetToStart()
	{
		if (hopRoutine != null)
		{
			StopCoroutine(hopRoutine);
			hopRoutine = null;
		}

		isTriggered = false;
		CurrentHole = null;
		pendingTrigger = null;
		settled = true;
		if (rb != null)
		{
			rb.velocity = Vector3.zero;
			rb.angularVelocity = Vector3.zero;
			rb.useGravity = false;
			rb.isKinematic = true;
			rb.MovePosition(restPosition);
		}
		else
		{
			transform.position = restPosition;
		}
		Physics.SyncTransforms();
		if (col != null)
			col.enabled = true;
		if (rend != null)
			rend.enabled = true;
	}

	public override void OnScored(HoleParent hole)
	{
		if (hole != null && value > 0)
			hole.AddScore(value);
		value = 0;
		if (owner != null)
			owner.Despawn(this);
		else
			Destroy(gameObject);
	}

	public void SleepInPool()
	{
		if (hopRoutine != null)
		{
			StopCoroutine(hopRoutine);
			hopRoutine = null;
		}
		armed = false;
		settled = false;
		isTriggered = false;
		CurrentHole = null;
		pendingTrigger = null;
		value = 0;
		if (rb != null)
		{
			rb.velocity = Vector3.zero;
			rb.angularVelocity = Vector3.zero;
			rb.isKinematic = true;
		}
		if (col != null)
			col.enabled = false;
		if (rend != null)
		{
			rend.SetPropertyBlock(null);
			rend.enabled = false;
		}
		gameObject.SetActive(false);
	}

	private void SetOrbColor(Color tint)
	{
		if (rend == null)
			return;

		if (colorBlock == null)
			colorBlock = new MaterialPropertyBlock();

		colorBlock.SetColor(ColorId, tint);
		rend.SetPropertyBlock(colorBlock);
		if (rend.material != null)
			rend.material.color = tint;
	}

	private bool IsPendingTriggerValid()
	{
		if (pendingTrigger == null || !pendingTrigger.CompareTag("Player"))
			return false;

		HoleParent hole = pendingTrigger.GetComponentInParent<HoleParent>();
		return hole != null && hole.holeType == HoleParent.TypeOfHole.player;
	}

	private void RefreshMetrics()
	{
		if (col != null)
			size = col.bounds.size;
		else if (rend != null)
			size = rend.bounds.size;
		V3 = size.x * size.y * size.z;
		if (rb != null)
			rb.mass = Mathf.Max(0.15f, V3 * 50f);
	}
}
