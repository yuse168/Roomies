#if UNITY_EDITOR
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Art = RoomiesCasinoArt;

/// <summary>
/// 前からあるスロット（Assets/_Prefabs/Slot.prefab）とブラックジャック台（BlackjackTable.prefab）を、
/// AGENTS.md のプレハブの決まりの形に組み直し、見た目を 1 つのメッシュに替える（2026/11/23）。何度実行しても同じ結果。
///
/// スロット（組み直しの前は、仕組みの部品が見た目から約 4.9km 離れた所にあり、音もそこから鳴っていた）：
///   ルート：NetworkObject・SlotMachine・台を選ぶ当たり判定（箱）。原点は足元、画面の向きが +Z
///   Visual：筐体（1 つのメッシュ）／Anchor_Screen：画面（Canvas）
/// ブラックジャック台：
///   ルート：NetworkObject・BlackjackTable・台の当たり判定。原点は足元、遊ぶ人の側が +Z
///   Visual：台（1 つのメッシュ）
/// </summary>
internal static partial class RoomiesCasinoBuilder
{
    static void RebuildSlotPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(SlotBasePath);
        try
        {
            var machine = root.GetComponentInChildren<SlotMachine>(true);
            var canvas = machine.GetComponentInChildren<Canvas>(true).transform;

            if (machine.gameObject != root)
            {
                // 古い形：仕組みは子（SlotMachine (2)）にある。ルートへ移す（文字などへの参照はそのまま写る）
                GameObject old = machine.gameObject;
                foreach (var type in new[] { typeof(NetworkObject), typeof(SlotMachine) })
                {
                    if (root.GetComponent(type) != null) continue;
                    UnityEditorInternal.ComponentUtility.CopyComponent(old.GetComponent(type));
                    UnityEditorInternal.ComponentUtility.PasteComponentAsNew(root);
                }
                canvas.SetParent(root.transform, false);
                Object.DestroyImmediate(old);
                machine = root.GetComponent<SlotMachine>();
            }

            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            // 画面：筐体の前の面のすぐ手前。Canvas は自分の -Z の側から読めるので、アンカーを半回転しておく
            Transform anchor = root.transform.Find("Anchor_Screen");
            if (anchor == null) anchor = Child(root.transform, "Anchor_Screen");
            anchor.localPosition = new Vector3(0, Art.SlotScreenY, Art.SlotFrontZ + .012f);
            anchor.localRotation = Quaternion.Euler(0, 180, 0);
            anchor.localScale = Vector3.one;
            canvas.SetParent(anchor, false);
            var rect = (RectTransform)canvas;
            canvas.localPosition = Vector3.zero;
            canvas.localRotation = Quaternion.identity;
            canvas.localScale = Vector3.one * (Art.SlotScreenW / Mathf.Max(1f, rect.sizeDelta.x));

            // 見た目：Visual の中身を入れ替える
            Transform oldVisual = root.transform.Find("Visual");
            if (oldVisual != null) Object.DestroyImmediate(oldVisual.gameObject);
            Art.Place(Child(root.transform, "Visual"), "Cabinet", Art.SlotCabinet(0));

            // 台を選ぶ当たり判定（ぶつかる箱も兼ねる）
            var box = root.GetComponent<BoxCollider>();
            if (box == null) box = root.AddComponent<BoxCollider>();
            box.isTrigger = false;
            box.center = new Vector3(0, Art.SlotHeight / 2f, .03f);
            box.size = new Vector3(Art.SlotWidth, Art.SlotHeight, Art.SlotDepth + .14f);

            var so = new SerializedObject(machine);
            so.FindProperty("interactionCollider").objectReferenceValue = box;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 画面の中身（リール・演出の部品）と筐体のランプ
            BuildSlotScreen(root, machine, canvas);

            PrefabUtility.SaveAsPrefabAsset(root, SlotBasePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void RebuildBlackjackPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(BlackjackBasePath);
        try
        {
            Transform visual = root.transform.Find("Visual");
            if (visual != null) Object.DestroyImmediate(visual.gameObject);
            Art.Place(Child(root.transform, "Visual"), "Table", Art.BlackjackTable());

            var box = root.GetComponent<BoxCollider>();
            if (box == null) box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0, .48f, .05f);
            box.size = new Vector3(Art.BjWidth + .1f, .96f, Art.BjFrontZ - Art.BjBackZ + .05f);

            // カードと結果の画面（Anchor_Display）。前は実行時に作っていた物を、プレハブに実体として置く
            // 置く所：親（奥）の側の上。遊ぶ人と台の間に置くと、台のフェルトとチップが隠れる
            var table = root.GetComponent<BlackjackTable>();
            var so = new SerializedObject(table);
            so.FindProperty("displayLocalPosition").vector3Value = new Vector3(0, 1.72f, Art.BjBackZ - .06f);
            so.FindProperty("displayScale").floatValue = .0018f;
            so.FindProperty("displaySize").vector2Value = new Vector2(700, 300);   // 補助の表示（賭け金と結果）だけなので低くする
            so.ApplyModifiedPropertiesWithoutUndo();
            table.EditorBuildUI();

            // 台の上の実物：カード・チップ・点数の札
            BuildBlackjackCards(root, table);

            PrefabUtility.SaveAsPrefabAsset(root, BlackjackBasePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
#endif
