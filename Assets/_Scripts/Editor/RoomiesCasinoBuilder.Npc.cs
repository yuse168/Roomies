#if UNITY_EDITOR
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Art = RoomiesCasinoArt;

/// <summary>
/// NPC の吹き出しと、警察に話しかける仕組みを、プレハブに置く。
///
///  ・店の人（CasinoClerk.prefab）… Roomies/Casino/2 Build Gimmick Prefabs の中で、吹き出し `Anchor_Bubble` を置く
///  ・警察（Assets/_Prefabs/Smuggling/Police_Placeholder.prefab）… Roomies/NPC/Build Police Talk。
///    ルートに NpcSpeechBubble と PoliceTalk を足し、吹き出し `Anchor_Bubble` を置く。モデル・巡回・当たり判定には触らない。何度実行しても同じ結果
/// 吹き出しは、ふだんは隠してある。NpcSpeechBubble が出し入れし、見ている人のカメラのほうへ向ける。
/// </summary>
internal static partial class RoomiesCasinoBuilder
{
    const string PolicePrefabPath = "Assets/_Prefabs/Smuggling/Police_Placeholder.prefab";

    /// <summary>吹き出しの板（クリーム色に墨のふち、下にしっぽ）。原点は板の真ん中、文字の面が +Z。</summary>
    static Mesh SpeechBubbleMesh() => Art.Build("npc_speech_bubble", lp =>
    {
        const float w = 1.7f, h = .52f;
        lp.Push(Vector3.zero, new Vector3(90, 0, 0));          // XZ の多角形を立てる（厚みの向きが +Z）
        lp.Prism(Art.Chamfer(w + .06f, h + .06f, .19f), -.012f, 0f, "ink", null, true);
        lp.Prism(Art.Chamfer(w, h, .16f), 0f, .012f, "bubble_glow", null, false, 1f, true);
        // しっぽ（板の下。立てたあとは、多角形の +Z が下になる）
        lp.Sharp = true;
        lp.Prism(new[] { new Vector2(-.13f, h / 2 - .02f), new Vector2(.13f, h / 2 - .02f), new Vector2(0f, h / 2 + .2f) }, -.012f, 0f, "ink", null, true);
        lp.Prism(new[] { new Vector2(-.09f, h / 2 - .03f), new Vector2(.09f, h / 2 - .03f), new Vector2(0f, h / 2 + .14f) }, 0f, .012f, "bubble_glow", null, false, 1f, true);
        lp.Sharp = false;
        lp.Pop();
    });

    /// <summary>ルートの直下に吹き出し（Anchor_Bubble）を置き、NpcSpeechBubble に割り当てる。height：足元から板の真ん中までの高さ。</summary>
    static NpcSpeechBubble BuildSpeechBubble(GameObject root, float height)
    {
        Transform old = root.transform.Find("Anchor_Bubble");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        Transform anchor = Child(root.transform, "Anchor_Bubble", new Vector3(0, height, 0));
        Art.Place(Child(anchor, "Visual"), "Plate", SpeechBubbleMesh(), default, 0, false, false);
        var text = Label(anchor, "Text", "…", new Vector3(0, 0, .016f), 1.05f, new Color(.13f, .1f, .16f));
        ((RectTransform)text.transform).sizeDelta = new Vector2(1.54f, .42f);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.enableAutoSizing = true; text.fontSizeMin = .6f; text.fontSizeMax = 1.05f;
        anchor.gameObject.SetActive(false);

        var speech = root.GetComponent<NpcSpeechBubble>();
        if (speech == null) speech = root.AddComponent<NpcSpeechBubble>();
        var so = new SerializedObject(speech);
        so.FindProperty("bubble").objectReferenceValue = anchor;
        so.FindProperty("text").objectReferenceValue = text;
        so.ApplyModifiedPropertiesWithoutUndo();
        return speech;
    }

    [MenuItem("Roomies/NPC/Build Police Talk")]
    internal static void BuildPoliceTalk()
    {
        Art.Begin();
        var root = PrefabUtility.LoadPrefabContents(PolicePrefabPath);
        try
        {
            var patrol = root.GetComponent<SmugglingPolicePatrol>();
            if (patrol == null || root.GetComponent<NetworkObject>() == null)
            {
                Debug.LogError("[NPC] Police_Placeholder のルートに SmugglingPolicePatrol / NetworkObject がありません");
                return;
            }
            var speech = BuildSpeechBubble(root, 2.45f);
            var talk = root.GetComponent<PoliceTalk>();
            if (talk == null) talk = root.AddComponent<PoliceTalk>();
            var so = new SerializedObject(talk);
            so.FindProperty("patrol").objectReferenceValue = patrol;
            so.FindProperty("bubble").objectReferenceValue = speech;
            so.FindProperty("interactDistance").floatValue = 3f;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PolicePrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Art.End();
        Debug.Log("[NPC] police talk built");
    }
}
#endif
