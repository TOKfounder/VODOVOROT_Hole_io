using UnityEngine;

public class ScoreOrb : FallingObject
{
	[SerializeField] private float lifetime = 12f;
	[SerializeField] private float collectDelay = 0.45f;

	private ScoreOrbSpawner owner;
	private float spawnTime;
	private bool armed;
	private Collider pendingTrigger;

	protected override bool AssignValueFromVolume => false;
	protected override bool CountsTowardMapTotal => false;
	protected override bool ResetsIfNotScored => false;
	protected override bool IgnoresMapOnStart => false;
	protected override int ObjectLayer => 0;

	void Update()
	{
		if (!armed || owner == null)
			return;

		if (Time.time - spawnTime >= lifetime)
		{
			owner.Despawn(this);
			return;
		}

		if (pendingTrigger != null && CanCollect)
		{
			Collider trigger = pendingTrigger;
			pendingTrigger = null;
			TryBeginFall(trigger);
		}
	}

	private bool CanCollect => Time.time - spawnTime >= collectDelay;

	public void Launch(ScoreOrbSpawner spawner, int orbValue, Color tint, Vector3 origin, Vector3 impulse, float scale)
	{
		owner = spawner;
		value = Mathf.Max(1, orbValue);
		armed = true;
		spawnTime = Time.time;
		isTriggered = false;
		CurrentHole = null;
		pendingTrigger = null;

		transform.position = origin;
		transform.rotation = Quaternion.identity;
		transform.localScale = Vector3.one * Mathf.Max(0.12f, scale);
		if (col != null)
			col.enabled = true;
		if (rend != null)
		{
			rend.enabled = true;
			rend.material.color = tint;
		}

		RefreshMetrics();

		if (rb != null)
		{
			rb.isKinematic = false;
			rb.useGravity = true;
			rb.velocity = Vector3.zero;
			rb.angularVelocity = Vector3.zero;
			rb.AddForce(impulse, ForceMode.VelocityChange);
		}
	}

	protected override void OnTriggerEnter(Collider other)
	{
		if (!armed)
			return;

		if (!CanCollect)
		{
			if (other.CompareTag("Player"))
				pendingTrigger = other;
			return;
		}

		TryBeginFall(other);
	}

	public override void ResetToStart()
	{
		if (owner != null)
			owner.Despawn(this);
		else
			Destroy(gameObject);
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
		armed = false;
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
			rend.enabled = false;
		gameObject.SetActive(false);
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
