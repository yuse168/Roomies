using UnityEngine;

/// <summary>
/// 足音の鳴らし口。プレイヤーもNPCもここを通す。
///
/// ・同じ足音が重ならないよう、`GameAudio.PlayExclusive` で1本の口を使い回す
/// ・足元の材質（マテリアル名）でピッチと音量を少し変える
///   調整は `Resources/Audio/FootstepSurfaces.asset`
/// </summary>
public static class FootstepAudio
{
    private const string Key = "player/footstep";

    /// <summary>足元を調べる距離（足の位置から下へ）。</summary>
    private const float GroundProbe = 1.4f;

    private static FootstepSurfaces surfaces;
    private static bool surfacesLoaded;
    private static readonly RaycastHit[] hits = new RaycastHit[4];

    /// <summary>自分の足音（2Dで鳴らす。距離減衰なし）。</summary>
    public static void PlayLocal(Vector3 footPosition, float volume)
    {
        Resolve(footPosition, out float pitch, out float surfaceVolume);
        GameAudio.PlayExclusive(Key, volume * surfaceVolume, pitch);
    }

    /// <summary>他人・NPCの足音（位置から鳴らす）。</summary>
    public static void PlayAt(Vector3 footPosition, float volume, string channel,
                              float maxDistance = 18f)
    {
        Resolve(footPosition, out float pitch, out float surfaceVolume);
        GameAudio.PlayExclusiveAt(
            Key, channel, footPosition, volume * surfaceVolume, pitch, maxDistance);
    }

    /// <summary>足元の材質から、ピッチと音量を決める。</summary>
    private static void Resolve(Vector3 footPosition, out float pitch, out float volume)
    {
        FootstepSurfaces table = Table();
        pitch = table != null ? table.defaultPitch : 1f;
        volume = table != null ? table.defaultVolume : 1f;

        if (table == null) return;

        // 足元へ1本だけ短いレイを飛ばす（足音1回につき1回なので負荷は小さい）
        int count = Physics.RaycastNonAlloc(
            footPosition + Vector3.up * 0.3f, Vector3.down, hits,
            GroundProbe, ~0, QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        string materialName = null;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null || hit.distance >= nearest) continue;

            // 自分自身（プレイヤーのカプセル）は床ではない
            if (hit.collider.GetComponentInParent<CharacterController>() != null) continue;

            var renderer = hit.collider.GetComponent<Renderer>();
            if (renderer == null) renderer = hit.collider.GetComponentInChildren<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null) continue;

            nearest = hit.distance;
            materialName = renderer.sharedMaterial.name;
        }

        table.Resolve(materialName, out pitch, out volume);
    }

    private static FootstepSurfaces Table()
    {
        if (!surfacesLoaded)
        {
            surfaces = Resources.Load<FootstepSurfaces>("Audio/FootstepSurfaces");
            surfacesLoaded = true;
        }
        return surfaces;
    }

    /// <summary>アセットを差し替えた直後に読み直す（Editor用）。</summary>
    public static void ClearCache()
    {
        surfaces = null;
        surfacesLoaded = false;
    }
}
