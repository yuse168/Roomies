#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 起動ロゴ用のセリフ体（明朝相当）TMPフォントアセットを生成する。
///
/// ロゴで使う文字は "LNStudio" の8文字だけなので、
/// アトラスを事前ベイクしないダイナミックフォントアセットで足りる。
///
/// 注意：現在の元TTFはWindowsのシステムフォント（Monotype / Linotype）で、
/// ゲームへ同梱して配布する権利は無い。製品ビルド前にOFLのフォント
/// （Shippori Mincho / Playfair Display / Cormorant など）へ差し替えること。
/// 差し替えはAssets/_Fontsへ.ttfを置いてSourceFontsへ追記するだけでよい。
/// </summary>
[InitializeOnLoad]
internal static class SplashFontBuilder
{
    private const string OutputFolder = "Assets/Resources/Fonts";

    /// <summary>元TTFのパスと、生成するTMPフォントアセット名。</summary>
    private static readonly (string source, string assetName)[] SourceFonts =
    {
        ("Assets/_Fonts/TimesNewRoman.ttf",    "LogoSerif SDF"),
        ("Assets/_Fonts/PalatinoLinotype.ttf", "LogoSerifAlt SDF"),
    };

    static SplashFontBuilder()
    {
        EditorApplication.delayCall += BuildIfNeeded;
    }

    [MenuItem("Roomies/Build Splash Logo Fonts")]
    private static void BuildIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
            return;

        bool createdAny = false;

        foreach ((string source, string assetName) in SourceFonts)
        {
            string outputPath = $"{OutputFolder}/{assetName}.asset";
            if (File.Exists(outputPath)) continue;
            if (!File.Exists(source)) continue;

            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(source);
            if (sourceFont == null)
            {
                Debug.LogWarning($"[Roomies] ロゴ用フォントを読み込めません：{source}");
                continue;
            }

            EnsureFolder(OutputFolder);

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
            if (fontAsset == null)
            {
                Debug.LogError($"[Roomies] TMPフォントアセットの生成に失敗：{source}");
                continue;
            }

            fontAsset.name = assetName;
            AssetDatabase.CreateAsset(fontAsset, outputPath);

            // アトラスとマテリアルはサブアセットとして同じファイルへ収める
            if (fontAsset.atlasTextures != null)
            {
                foreach (Texture2D atlas in fontAsset.atlasTextures)
                {
                    if (atlas != null) AssetDatabase.AddObjectToAsset(atlas, fontAsset);
                }
            }
            if (fontAsset.material != null)
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

            EditorUtility.SetDirty(fontAsset);
            createdAny = true;
            Debug.Log($"[Roomies] ロゴ用フォントを生成しました：{outputPath}");
        }

        if (createdAny)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
#endif
