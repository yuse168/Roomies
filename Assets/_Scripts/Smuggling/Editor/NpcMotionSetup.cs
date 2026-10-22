#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// NPC（警察・売人・運び屋）に、歩きのAnimatorと足音を付けるツール。
///
/// ・モデルのAnimatorへプレイヤーと同じ `Bob_animation.controller` を割り当てる
///   （未設定だとAポーズのまま滑るため）
/// ・ルートへ `NpcMotionFeedback` を付け、Animatorの参照を入れる
///
/// 規約（AGENTS.md）：実行時には何も生成しない。プレハブへ事前に付けておく。
///
/// メニュー：Roomies/Smuggling/Setup NPC Animation And Footsteps
/// </summary>
internal static class NpcMotionSetup
{
    private const string ControllerPath = "Assets/_Animation/Bob_animation.controller";

    private static readonly string[] NpcPrefabs =
    {
        "Assets/_Prefabs/Smuggling/Police_Placeholder.prefab",
        "Assets/_Prefabs/Smuggling/Dealer_Placeholder.prefab",
        "Assets/_Prefabs/Smuggling/Giver_Placeholder.prefab",
    };

    [MenuItem("Roomies/Smuggling/Setup NPC Animation And Footsteps")]
    private static void Setup()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[Smuggling] Animatorコントローラーが見つかりません: {ControllerPath}");
            return;
        }

        int done = 0;

        foreach (string path in NpcPrefabs)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                Debug.LogWarning($"[Smuggling] プレハブが開けません: {path}");
                continue;
            }

            try
            {
                Animator animator = root.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    Debug.LogWarning($"[Smuggling] Animatorが無いので飛ばします: {path}");
                    continue;
                }

                if (animator.runtimeAnimatorController == null)
                    animator.runtimeAnimatorController = controller;

                // ルートモーションで勝手に動かないようにする（移動はAIが決める）
                animator.applyRootMotion = false;

                NpcMotionFeedback feedback = root.GetComponent<NpcMotionFeedback>();
                if (feedback == null) feedback = root.AddComponent<NpcMotionFeedback>();

                var serialized = new SerializedObject(feedback);
                serialized.FindProperty("animator").objectReferenceValue = animator;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
                done++;
                Debug.Log($"[Smuggling] {System.IO.Path.GetFileNameWithoutExtension(path)} に歩きと足音を設定しました");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Smuggling] NPC {done} 体を設定しました");
    }
}
#endif
