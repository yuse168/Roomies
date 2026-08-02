using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全クライアント共通の家具カタログ（コード定義・インデックス固定）。
/// マルチ同期では index だけを送り、各クライアントがこのカタログから
/// 同じ見た目・効果を復元する。
/// </summary>
public static class FurnitureCatalog
{
    private static List<FurnitureItem> _items;

    public static List<FurnitureItem> Items
    {
        get { if (_items == null) _items = Build(); return _items; }
    }

    public static int Count => Items.Count;

    /// <summary>
    /// Scene上のFurnitureEditControllerで編集したカタログを、
    /// NetworkFurnitureやServer購入処理と共有する。
    /// indexが同期キーなので全クライアントで同じ並びを使うこと。
    /// </summary>
    public static void Configure(IReadOnlyList<FurnitureItem> items)
    {
        if (items == null || items.Count == 0)
        {
            _items = Build();
            return;
        }

        _items = new List<FurnitureItem>(items);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _items = null;
    }

    public static FurnitureItem Get(int index)
    {
        var list = Items;
        if (index < 0 || index >= list.Count) return null;
        return list[index];
    }

    private static List<FurnitureItem> Build()
    {
        return new List<FurnitureItem>
        {
            // 第1弾
            new FurnitureItem{ id="coffee_maker",        displayName="コーヒーメーカー", cost=300,  effect=FurnitureEffect.MoveSpeed,     effectValue=15, placeholderSize=new Vector3(0.4f,0.6f,0.4f),  placeholderColor=new Color(0.45f,0.28f,0.18f)},
            new FurnitureItem{ id="energy_drink_fridge", displayName="エナドリ冷蔵庫",   cost=450,  effect=FurnitureEffect.MoveSpeed,     effectValue=10, placeholderSize=new Vector3(0.7f,1.6f,0.7f),  placeholderColor=new Color(0.20f,0.65f,0.80f)},
            new FurnitureItem{ id="comfy_bed",           displayName="ふかふかベッド",   cost=600,  placeholderSize=new Vector3(1.1f,0.45f,2.0f), placeholderColor=new Color(0.82f,0.32f,0.42f)},
            new FurnitureItem{ id="welcome_mat",         displayName="げんかんマット",   cost=150,  placeholderSize=new Vector3(1.0f,0.05f,0.6f), placeholderColor=new Color(0.55f,0.45f,0.30f)},
            new FurnitureItem{ id="piggy_bank",          displayName="ちょきんばこ",     cost=200,  effect=FurnitureEffect.PassiveIncome, effectValue=30, placeholderSize=new Vector3(0.4f,0.4f,0.5f),  placeholderColor=new Color(0.95f,0.55f,0.65f)},
            new FurnitureItem{ id="wall_clock",          displayName="かべ掛け時計",     cost=250,  placeholderSize=new Vector3(0.5f,0.5f,0.1f),  placeholderColor=new Color(0.85f,0.85f,0.90f)},
            // 第2弾
            new FurnitureItem{ id="delivery_terminal",   displayName="配達端末",         cost=1200, placeholderSize=new Vector3(0.6f,1.2f,0.5f),  placeholderColor=new Color(0.30f,0.35f,0.45f)},
            new FurnitureItem{ id="lucky_cat",           displayName="まねきねこ",       cost=1000, placeholderSize=new Vector3(0.4f,0.6f,0.4f),  placeholderColor=new Color(0.95f,0.85f,0.55f)},
            new FurnitureItem{ id="vending_machine",     displayName="じはんき",         cost=1800, effect=FurnitureEffect.PassiveIncome, effectValue=80, placeholderSize=new Vector3(1.0f,2.0f,0.7f),  placeholderColor=new Color(0.85f,0.25f,0.30f)},
            // 第3弾・ネタ
            new FurnitureItem{ id="disco_ball",          displayName="ミラーボール",     cost=900,  effect=FurnitureEffect.MoveSpeed,     effectValue=5,  placeholderSize=new Vector3(0.6f,0.6f,0.6f),  placeholderColor=new Color(0.80f,0.80f,0.95f)},
            new FurnitureItem{ id="trampoline",          displayName="トランポリン",     cost=700,  placeholderSize=new Vector3(1.5f,0.3f,1.5f),  placeholderColor=new Color(0.30f,0.75f,0.55f)},
            new FurnitureItem{ id="arcade_machine",      displayName="アーケード筐体",   cost=2500, placeholderSize=new Vector3(0.8f,1.8f,0.8f),  placeholderColor=new Color(0.55f,0.30f,0.80f)},
        };
    }
}
