using UnityEditor;

// Le animazioni custom del simp (Assets/Art/Simps/Test_animations) vengono
// riesportate spesso da Blender, e ogni volta Unity resetta le impostazioni
// per-clip (loop, bake della root). Questo le riapplica automaticamente a ogni
// reimport, cosi' non serve rifarlo a mano.
public class SimpAnimationPostprocessor : AssetPostprocessor
{
    private const string TargetFolder = "Assets/Art/Simps/Test_animations/";
    private const bool Enabled = false;

    private void OnPreprocessModel()
    {
        if (!Enabled) return;
        if (!assetPath.StartsWith(TargetFolder)) return;

        var importer = (ModelImporter)assetImporter;
        var clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;

        bool shouldLoop = assetPath.ToLower().Contains("idle");

        for (int i = 0; i < clips.Length; i++)
        {
            clips[i].loopTime = shouldLoop;
            clips[i].lockRootRotation = true;
            clips[i].lockRootHeightY = true;
            clips[i].lockRootPositionXZ = true;
        }

        importer.clipAnimations = clips;
    }
}
