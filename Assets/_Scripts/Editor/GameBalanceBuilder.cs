#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 数値の表 `Resources/Balance/GameBalance.asset` を作るツール。
/// 既にある場合は作り直さず、新しく足した項目を書き出して開くだけ（調整した値を壊さない）。
///
/// メニュー：Roomies/Balance/Rebuild Game Balance
/// </summary>
internal static class GameBalanceBuilder
{
    private const string AssetPath = "Assets/Resources/Balance/GameBalance.asset";

    [MenuItem("Roomies/Balance/Rebuild Game Balance")]
    private static void Rebuild()
    {
        System.IO.Directory.CreateDirectory("Assets/Resources/Balance");

        var balance = AssetDatabase.LoadAssetAtPath<GameBalance>(AssetPath);
        if (balance != null)
        {
            // 新しく足した項目（既定値）もファイルに書き出す。調整済みの値はそのまま
            EditorUtility.SetDirty(balance);
            AssetDatabase.SaveAssetIfDirty(balance);
            Selection.activeObject = balance;
            Debug.Log($"[Balance] 既にあります（値はそのまま・新しい項目を書き出した） → {AssetPath}");
            return;
        }

        balance = ScriptableObject.CreateInstance<GameBalance>();
        AssetDatabase.CreateAsset(balance, AssetPath);
        AssetDatabase.SaveAssets();

        Selection.activeObject = balance;
        Debug.Log($"[Balance] 数値の表を作りました → {AssetPath}");
    }
}
#endif
