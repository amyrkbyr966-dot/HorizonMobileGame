#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Import settings for the converted Project PLATEAU Tokyo map
/// (Assets/Resources/Models/Map/Plateau/**).
///
/// The converter (~/workspace/model-pipeline/plateau_convert.py) already
/// re-centred the geometry on Shibuya Crossing, merged meshes per
/// material and downsized LOD2 facade textures to 512 px. These import
/// settings finish the mobile job inside Unity:
///
/// Models: the FBX files are authored in metres (Blender export, unit
/// scale 1.0), so the importer must NOT apply the 0.01 cm default —
/// useFileUnits + globalScale 1 keep 1 unit = 1 metre. Medium mesh
/// compression + vertex/polygon optimisation shrink the build; materials
/// are imported (facade photos are embedded in the FBX).
///
/// Textures (embedded facade photos, and any extracted alongside):
/// ASTC 6x6 on Android, max 1024 px, mipmaps on. Together with the
/// converter's 512 px downsize this keeps the whole map inside the
/// &lt;2 GB full / ~800 MB target game size budget (SIZE-BUDGET.md).
///
/// Only paths under Models/Map/Plateau are touched; every other model
/// and texture in the project keeps its own settings.</summary>
public class PlateauImportSettings : AssetPostprocessor
{
    const string PlateauPath = "Models/Map/Plateau";

    void OnPreprocessModel()
    {
        if (!assetPath.Contains(PlateauPath)) return;
        var importer = (ModelImporter)assetImporter;
        // Metres, not centimetres: the converter exports unit scale 1.0.
        importer.useFileUnits = true;
        importer.globalScale = 1f;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.External;
        importer.meshCompression = ModelImporterMeshCompression.Medium;
        importer.optimizeMeshPolygons = true;
        importer.optimizeMeshVertices = true;
        importer.isReadable = false;
        importer.importAnimation = false;
        importer.importBlendShapes = false;
        importer.importCameras = false;
        importer.importLights = false;
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.Contains(PlateauPath)) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
        {
            name = "Android",
            overridden = true,
            maxTextureSize = 1024,
            format = TextureImporterFormat.ASTC_6x6,
            textureCompression = TextureImporterCompression.Compressed,
            allowsAlphaSplitting = false,
        });
        importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
        {
            name = "Standalone",
            overridden = true,
            maxTextureSize = 1024,
            format = TextureImporterFormat.DXT5,
            textureCompression = TextureImporterCompression.Compressed,
        });
    }
}
#endif
