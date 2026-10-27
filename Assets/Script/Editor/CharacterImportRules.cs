using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 캐릭터 FBX를 이름 규칙대로 자동 설정한다. (Assets 안의 "Editor" 폴더에 넣을 것)
///
///   이름.fbx          → 모델. 같은 폴더에 "이름@*.fbx"가 있으면 Generic + 아바타 생성, 애니메이션 가져오기 끔
///   이름@동작.fbx     → 애니메이션. Generic + "이름.fbx"의 아바타 복사, 머티리얼 가져오기 끔
///
/// 반복 재생(Loop Time):
///   클립 이름이 LoopNames 중 하나(Walk, Idle, Run)이거나 "_Loop"로 끝나면 켜고, 나머지(Roll, Attack 등)는 끈다.
///   Inspector에서 손으로 바꿔도 다시 가져올 때 이 규칙이 우선한다.
/// </summary>
public class CharacterImportRules : AssetPostprocessor
{
    private static readonly string[] LoopNames = { "Walk", "Idle", "Run" };
    private const string LoopSuffix = "_Loop";

    // ---------------------------------------------------------------- 모델 / 리그
    private void OnPreprocessModel()
    {
        if (!(assetImporter is ModelImporter importer))
            return;

        string file = Path.GetFileNameWithoutExtension(assetPath);
        string dir = Path.GetDirectoryName(assetPath);
        int at = file.IndexOf('@');

        if (at > 0)
        {
            // 애니메이션 파일
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = true;
            // 뼈만 있는 파일은 최상위 노드(Armature)가 하나뿐이라, 이걸 끄면 Unity가 Armature를 없애서
            // 뼈 경로가 모델(Armature/tripo::Root/...)과 달라지고 애니메이션이 안 붙는다.
            importer.preserveHierarchy = true;

            string basePath = ToAssetPath(Path.Combine(dir, file.Substring(0, at) + ".fbx"));
            Avatar avatar = FindAvatar(basePath);
            if (avatar != null)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = avatar;
            }
            else
            {
                // 기본 모델이 아직 안 들어왔으면 일단 자체 아바타로 가져오고,
                // 기본 모델이 들어오는 순간 OnPostprocessAllAssets에서 다시 가져온다.
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }
        }
        else if (AnimSiblings(dir, file).Length > 0)
        {
            // 기본 모델 파일
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.preserveHierarchy = true;
        }
    }

    // ---------------------------------------------------------------- 클립 / 반복
    private void OnPreprocessAnimation()
    {
        if (!(assetImporter is ModelImporter importer))
            return;
        if (Path.GetFileNameWithoutExtension(assetPath).IndexOf('@') <= 0)
            return;

        ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
        ModelImporterClipAnimation[] current = importer.clipAnimations;

        var clips = new List<ModelImporterClipAnimation>();
        if (current != null && current.Length > 0)
            clips.AddRange(current);   // 이미 손댄 클립 설정은 유지
        else
            clips.AddRange(defaults);

        foreach (ModelImporterClipAnimation d in defaults)
        {
            if (!clips.Exists(c => c.takeName == d.takeName))
                clips.Add(d);           // FBX에 새로 생긴 take만 추가
        }

        foreach (ModelImporterClipAnimation c in clips)
        {
            bool loop = IsLoop(c.name);
            c.loopTime = loop;
            c.loopPose = loop;
        }

        importer.clipAnimations = clips.ToArray();
    }

    // 기본 모델이 (다시) 들어오면, 아바타를 아직 못 받은 애니메이션 파일들을 다시 가져온다.
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (string path in imported)
        {
            if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                continue;
            string file = Path.GetFileNameWithoutExtension(path);
            if (file.IndexOf('@') >= 0)
                continue;

            Avatar avatar = FindAvatar(path);
            if (avatar == null)
                continue;

            foreach (string sibling in AnimSiblings(Path.GetDirectoryName(path), file))
            {
                string sp = ToAssetPath(sibling);
                if (AssetImporter.GetAtPath(sp) is ModelImporter imp && imp.sourceAvatar != avatar)
                {
                    imp.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    imp.sourceAvatar = avatar;
                    imp.SaveAndReimport();
                }
            }
        }
    }

    // ---------------------------------------------------------------- helpers
    private static bool IsLoop(string clipName)
    {
        if (clipName.EndsWith(LoopSuffix, StringComparison.OrdinalIgnoreCase))
            return true;
        foreach (string n in LoopNames)
        {
            if (string.Equals(clipName, n, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string[] AnimSiblings(string dir, string baseName)
    {
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            return Array.Empty<string>();
        return Directory.GetFiles(dir, baseName + "@*.fbx");
    }

    private static Avatar FindAvatar(string path)
    {
        foreach (UnityEngine.Object o in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (o is Avatar a)
                return a;
        }
        return null;
    }

    private static string ToAssetPath(string p) => p.Replace('\\', '/');
}
