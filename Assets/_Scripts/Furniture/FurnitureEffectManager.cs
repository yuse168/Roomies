using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 設置済み家具の効果を集計する管理クラス。
/// 「Room」タグの上に乗っている家具だけが効果を発揮する。
///
/// ・MoveSpeed   … 部屋にある間、移動速度バフ（PlayerMovementが参照）
/// ・PassiveIncome… 毎朝、共同口座へ自動収入（サーバーのみ付与）
///
/// ※ 現状は家具がローカル管理（フェーズ2）のため、収入はサーバー(ホスト)の
///    家具のみが対象。マルチ全員ぶんの同期はフェーズ3で対応。
/// </summary>
public class FurnitureEffectManager : MonoBehaviour
{
    private static FurnitureEffectManager _instance;

    /// <summary>取得（無ければ生成）。家具登録時などに使う。</summary>
    public static FurnitureEffectManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("FurnitureEffectManager");
                _instance = go.AddComponent<FurnitureEffectManager>();
            }
            return _instance;
        }
    }

    /// <summary>生成せずに参照だけ取得（無ければnull）。</summary>
    public static FurnitureEffectManager InstanceOrNull => _instance;

    private readonly List<PlacedFurniture> furniture = new List<PlacedFurniture>();

    // Room判定の更新間隔（毎フレームは重いので間引く）
    private const float RecalcInterval = 0.5f;
    private float recalcTimer;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DayManager.OnMorningArrived += OnMorning;
    }

    private void OnDestroy()
    {
        DayManager.OnMorningArrived -= OnMorning;
        if (_instance == this) _instance = null;
    }

    /// <summary>登録中の家具一覧（ステータス表示用）。</summary>
    public IReadOnlyList<PlacedFurniture> Registered => furniture;

    /// <summary>現在 有効な PassiveIncome の毎朝合計。</summary>
    public int PassiveIncomeTotal
    {
        get
        {
            int t = 0;
            foreach (var f in furniture)
            {
                if (f == null || !f.IsEffectActive) continue;
                if (f.effect == FurnitureEffect.PassiveIncome)
                    t += Mathf.RoundToInt(f.effectValue);
            }
            return t;
        }
    }

    public void Register(PlacedFurniture f)
    {
        if (f != null && !furniture.Contains(f))
        {
            furniture.Add(f);
            f.RecalculateOnRoom();
        }
    }

    public void Unregister(PlacedFurniture f)
    {
        furniture.Remove(f);
    }

    private void Update()
    {
        recalcTimer += Time.deltaTime;
        if (recalcTimer < RecalcInterval) return;
        recalcTimer = 0f;

        for (int i = furniture.Count - 1; i >= 0; i--)
        {
            if (furniture[i] == null) { furniture.RemoveAt(i); continue; }
            furniture[i].RecalculateOnRoom();
        }
    }

    /// <summary>移動速度の倍率（効果発動中のMoveSpeed家具の合計）。1.0=等速。</summary>
    public float MoveSpeedMultiplier
    {
        get
        {
            float bonusPercent = 0f;
            foreach (var f in furniture)
            {
                if (f == null || !f.IsEffectActive) continue;
                if (f.effect == FurnitureEffect.MoveSpeed)
                    bonusPercent += f.effectValue;
            }
            return 1f + bonusPercent / 100f;
        }
    }

    /// <summary>
    /// 毎朝の処理：
    /// 1) 前日までに買った家具の効果を有効化（「翌朝から効果」）
    /// 2) 部屋にあるPassiveIncome家具ぶんの収入を共同口座へ付与（サーバーのみ）
    /// </summary>
    private void OnMorning()
    {
        // 1) 全家具の効果を有効化（購入翌朝に発動）
        foreach (var f in furniture)
        {
            if (f != null) f.SetEffectActive(true);
        }

        // 2) 収入付与は多重加算を防ぐためサーバー（ホスト）だけが行う
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        var money = SharedMoneyManager.Instance;
        if (money == null) return;

        int total = 0;
        foreach (var f in furniture)
        {
            if (f == null || !f.IsEffectActive) continue;
            if (f.effect == FurnitureEffect.PassiveIncome)
                total += Mathf.RoundToInt(f.effectValue);
        }

        if (total > 0)
        {
            money.AddSharedMoney(total);
            Debug.Log($"[Furniture] パッシブ収入 +¥{total} を共同口座へ");
        }
    }
}
