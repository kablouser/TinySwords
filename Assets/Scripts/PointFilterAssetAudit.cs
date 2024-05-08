#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class PointFilterAssetAudit : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetImporter.importSettingsMissing)
        {
            return;
        }
        // probably unnessary
        //Undo.RecordObject(texture, "PointFilterAssetAudit");

        TextureImporter importer = assetImporter as TextureImporter;
        importer.filterMode = FilterMode.Point;
        importer.spritePixelsPerUnit = 64;
    }
}
#endif
