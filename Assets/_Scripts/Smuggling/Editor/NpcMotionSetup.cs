#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// NPC（警察・売人・運び屋）に、プレイヤーと同じ歩きのモデル・Animator・足音を付けるツール。
///
/// なぜモデルごと差し替えるか：
///  NPCは `Bobs.fbx`（アバターが有効にならない）を使っていたため、Animatorへコントローラーを
///  割り当ててもアニメーションが再生されず、Aポーズのまま滑っていた。
///  プレイヤーと同じ `bob_rig_mG.fbx` に差し替えると、同じクリップがそのまま動く。
///
/// 規約（AGENTS.md）：
///  ・見た目は `Visual/ModelRoot` の下だけを入れ替える。ルートのスクリプト・判定は触らない
///  ・実行時には何も生成しない
///
/// メニュー：Roomies/Smuggling/Setup NPC Animation And Footsteps
/// </summary>
internal static class NpcMotionSetup
{
    private const string ControllerPath = "Assets/_Animation/Bob_animation.controller";
    private const string ModelPath = "Assets/_Models/Bob/bob_rig_mG.fbx";

    // プレハブと、その体の色
    private static readonly (string prefab, string material)[] Npcs =
    {
        ("Assets/_Prefabs/Smuggling/Police_Placeholder.prefab", "Assets/_Prefabs/Smuggling/Smuggling_PoliceBlue.mat"),
        ("Assets/_Prefabs/Smuggling/Dealer_Placeholder.prefab", "Assets/_Prefabs/Smuggling/Smuggling_Dark.mat"),
        ("Assets/_Prefabs/Smuggling/Giver_Placeholder.prefab",  "Assets/_Prefabs/Smuggling/Smuggling_Brown.mat"),
    };

    [MenuItem("Roomies/Smuggling/Setup NPC Animation And Footsteps")]
    private static void Setup()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);

        if (controller == null || model == null)
        {
            Debug.LogError($"[Smuggling] 必要なアセットが見つかりません: {ControllerPath} / {ModelPath}");
            return;
        }

        int done = 0;

        foreach ((string prefabPath, string materialPath) in Npcs)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                Debug.LogWarning($"[Smuggling] プレハブが開けません: {prefabPath}");
                continue;
            }

            try
            {
                Transform modelRoot = FindChild(root.transform, "ModelRoot");
                if (modelRoot == null)
                {
                    Debug.LogWarning($"[Smuggling] ModelRoot が無いので飛ばします: {prefabPath}");
                    continue;
                }

                // 古いモデルを外す（ModelRootの中だけ入れ替える）
                for (int i = modelRoot.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(modelRoot.GetChild(i).gameObject);

                // プレイヤーと同じモデルを入れる。NPCのルートは足元なので原点に置く
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, modelRoot);
                PrefabUtility.UnpackPrefabInstance(
                    instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.name = "CharacterModel";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;

                Animator animator = instance.GetComponentInChildren<Animator>(true);
                if (animator == null) animator = instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // 遠くでも歩き続ける

                // 体の色を戻す（目などは触らない）
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material != null)
                {
                    foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                    {
                        if (renderer.name.ToLowerInvariant().Contains("eye")) continue;

                        var materials = new Material[renderer.sharedMaterials.Length];
                        for (int i = 0; i < materials.Length; i++) materials[i] = material;
                        renderer.sharedMaterials = materials;
                    }
                }

                NpcMotionFeedback feedback = root.GetComponent<NpcMotionFeedback>();
                if (feedback == null) feedback = root.AddComponent<NpcMotionFeedback>();

                var serialized = new SerializedObject(feedback);
                serialized.FindProperty("animator").objectReferenceValue = animator;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                done++;
                Debug.Log($"[Smuggling] {System.IO.Path.GetFileNameWithoutExtension(prefabPath)} の" +
                          $"モデルをプレイヤーと同じものに差し替え、歩きと足音を設定しました" +
                          $"（isHuman={animator.isHuman}）");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Smuggling] NPC {done} 体を設定しました");
    }

    /// <summary>名前で子孫を探す（深さは問わない）。</summary>
    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name) return child;
        return null;
    }
}
#endif
