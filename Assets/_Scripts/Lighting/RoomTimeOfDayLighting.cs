using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 部屋の明かりと画面の仕上げを、朝と夜で切り替える（見た目だけ。ゲームの進行・通信には触らない）。
/// DayManager の IsNight を読むだけ。全員の画面で同じように動く（時間帯は DayManager が同期している）。
/// 明かりの強さ・画面の仕上げの重みはインスペクターで直せる。配置ツール「Material Finish/6 Time Of Day Lighting」が置く。
/// </summary>
public class RoomTimeOfDayLighting : MonoBehaviour
{
    [System.Serializable]
    public struct LampLevel
    {
        public Light light;
        public float night;
        public float morning;
    }

    [SerializeField] private LampLevel[] lamps = new LampLevel[0];
    [SerializeField] private Volume nightVolume;
    [SerializeField] private Volume morningVolume;
    [SerializeField, Min(0f)] private float blendSeconds = 1.5f;

    private float blend = 1f;   // 1 = 夜、0 = 朝
    private bool targetNight = true;
    private bool initialized;

    private void Update()
    {
        var day = DayManager.Instance;
        bool night = day == null || day.IsNight;
        if (!initialized) { initialized = true; targetNight = night; blend = night ? 1f : 0f; Apply(blend); return; }
        if (night != targetNight) targetNight = night;
        float goal = targetNight ? 1f : 0f;
        if (Mathf.Approximately(blend, goal)) return;
        blend = blendSeconds <= 0f ? goal : Mathf.MoveTowards(blend, goal, Time.deltaTime / blendSeconds);
        Apply(blend);
    }

    /// <summary>すぐに朝か夜にする（エディタの撮影でも使う）。</summary>
    public void ApplyImmediate(bool night)
    {
        targetNight = night;
        blend = night ? 1f : 0f;
        Apply(blend);
    }

    private void Apply(float nightAmount)
    {
        foreach (var lamp in lamps)
        {
            if (lamp.light == null) continue;
            float intensity = Mathf.Lerp(lamp.morning, lamp.night, nightAmount);
            lamp.light.intensity = intensity;
            lamp.light.enabled = intensity > 0.001f;
        }
        if (nightVolume != null) nightVolume.weight = nightAmount;
        if (morningVolume != null) morningVolume.weight = 1f - nightAmount;
    }
}
