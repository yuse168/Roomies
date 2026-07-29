using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 友達へ直接渡すWindowsビルド用に、Steam App IDを実行ファイルの隣へ配置する。
/// Steamストアへ正式配信する際は、Depotへsteam_appid.txtを含めない設定にすること。
/// </summary>
public sealed class SteamAppIdBuildProcessor : IPostprocessBuildWithReport
{
    private const string SteamAppIdFileName = "steam_appid.txt";

    public int callbackOrder => 100;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.StandaloneWindows &&
            report.summary.platform != BuildTarget.StandaloneWindows64)
        {
            return;
        }

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string sourcePath = Path.Combine(projectRoot, SteamAppIdFileName);

        if (!File.Exists(sourcePath))
        {
            throw new BuildFailedException(
                $"Steam App IDファイルが見つかりません: {sourcePath}");
        }

        string appIdText = File.ReadAllText(sourcePath).Trim();
        if (!uint.TryParse(appIdText, out uint appId) || appId == 0)
        {
            throw new BuildFailedException(
                $"{SteamAppIdFileName}には有効な数字のApp IDを記載してください。現在値: '{appIdText}'");
        }

        string outputPath = Path.GetFullPath(report.summary.outputPath);
        string outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrEmpty(outputDirectory))
        {
            throw new BuildFailedException(
                $"ビルド出力先フォルダを取得できません: {report.summary.outputPath}");
        }

        string destinationPath = Path.Combine(outputDirectory, SteamAppIdFileName);
        if (!string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(sourcePath, destinationPath, true);
        }

        Debug.Log(
            $"[SteamBuild] App ID {appId} をビルドへ配置しました: {destinationPath}");
    }
}
