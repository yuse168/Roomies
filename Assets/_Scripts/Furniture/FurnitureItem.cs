using UnityEngine;

/// <summary>家具がもたらす効果の種類。今後ここに追加していく。</summary>
public enum FurnitureEffect
{
    None,           // 効果なし（見た目だけ）
    MoveSpeed,      // 部屋にいる間 移動速度 +value%（常時）
    PassiveIncome,  // 毎朝 共同口座に +value（パッシブ収入）
}

/// <summary>
/// カタログに並ぶ1つの家具の定義。
/// 見た目を変えたいときは <see cref="prefab"/> にPrefabをドロップする。
/// 未設定なら placeholderSize / placeholderColor の仮ブロック（Cube）が使われる。
/// </summary>
[System.Serializable]
public class FurnitureItem
{
    [Tooltip("内部識別用ID（セーブ・同期で使用）")]
    public string id = "furniture";

    [Tooltip("カタログUIに表示する名前")]
    public string displayName = "家具";

    [Tooltip("見た目のPrefab。空なら仮ブロックを生成する。ここを差し替えれば見た目が変わる。")]
    public GameObject prefab;

    [Header("仮ブロック設定（prefab未設定時のみ使用）")]
    public Vector3 placeholderSize = Vector3.one;
    public Color   placeholderColor = new Color(0.6f, 0.6f, 0.6f);

    [Header("物理（仮ブロック用。重さ・重力をテストしたい時）")]
    [Tooltip("ONにすると仮ブロックにRigidbodyを付与（重力で落ちる）。本番はプレハブ側で設定推奨。")]
    public bool  placeholderUsePhysics = false;
    [Tooltip("重さ（Rigidbody.mass）")]
    public float placeholderMass = 1f;
    [Tooltip("重力を効かせるか")]
    public bool  placeholderUseGravity = true;

    [Header("コスト")]
    public int cost = 0;

    [Header("効果（Roomタグの上に設置されている間だけ発動）")]
    public FurnitureEffect effect = FurnitureEffect.None;
    [Tooltip("MoveSpeed=速度+%, PassiveIncome=毎朝の金額")]
    public float effectValue = 0f;
}
