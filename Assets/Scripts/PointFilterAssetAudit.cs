#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class PointFilterAssetAudit : AssetPostprocessor
{
    void OnPostprocessTexture(Texture2D texture)
    {
        if (!assetImporter.importSettingsMissing)
        {
            return;
            //EditorUtility.SetDirty(asset);
        }

        TextureImporter importer = assetImporter as TextureImporter;
        importer.filterMode = FilterMode.Point;
        texture.filterMode = FilterMode.Point;
    }
}
#endif
