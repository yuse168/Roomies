using UnityEngine;

/// <summary>Cosmetic only. Particles never spawn colliders, loot, or money.</summary>
public static class MiningFeedback
{
    public static void Sound(Vector3 point, AudioClip clip, float volume = .35f)
    {
        if (clip != null) AudioSource.PlayClipAtPoint(clip, point, volume);
    }
    public static void Chips(Vector3 point, int count, AudioClip clip, bool rare = false)
    {
        Sound(point, clip);
        var go = new GameObject("Mining impact"); go.transform.position = point;
        var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.loop = false; main.duration = .25f; main.startLifetime = .65f;
        main.startSpeed = rare ? 2.4f : 3; main.startSize = rare ? .09f : .12f;
        main.gravityModifier = 1; main.maxParticles = 40;
        main.startColor = rare ? new Color(1,.75f,.18f) : new Color(.74f,.65f,.5f);
        var emission = ps.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .2f;
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = Resources.Load<Material>("RoomiesArt/Materials/mining_particles");
        ps.Play(); Object.Destroy(go, 1.5f);
    }
    public static GameObject Gas(Vector3 point)
    {
        var go = new GameObject("Visible nonlethal gas"); go.transform.position = point;
        var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.startLifetime = 2; main.startSpeed = .25f; main.startSize = .35f;
        main.startColor = new Color(.7f,1,.25f,.45f); main.maxParticles = 90;
        var emission = ps.emission; emission.rateOverTime = 30;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(8,.6f,6);
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = Resources.Load<Material>("RoomiesArt/Materials/mining_particles");
        ps.Play(); return go;
    }
}
