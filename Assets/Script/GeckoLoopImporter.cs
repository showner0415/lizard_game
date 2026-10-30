using UnityEditor;

/// <summary>
/// Gecko_Walk.fbx를 가져올 때 Walk/Idle 클립은 반복(Loop Time) 켜고, Roll은 한 번만 재생되게 끈다.
/// 반드시 Assets 안의 "Editor" 폴더에 넣을 것 (예: Assets/Editor/GeckoLoopImporter.cs).
/// </summary>
public class GeckoLoopImporter : AssetPostprocessor
{
    private void OnPreprocessAnimation()
    {
        if (!assetPath.Contains("Gecko_Walk"))
            return;

        var importer = (ModelImporter)assetImporter;
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;

        for (int i = 0; i < clips.Length; i++)
        {
            bool loop = !clips[i].name.Contains("Roll");
            clips[i].loopTime = loop;
            clips[i].loopPose = loop;
        }

        importer.clipAnimations = clips;
    }
}
