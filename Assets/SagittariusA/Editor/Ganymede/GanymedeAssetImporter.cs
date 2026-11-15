using UnityEditor;

namespace SagittariusA.EditorTools.Ganymede
{
    public sealed class GanymedeAssetImporter : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if(assetPath!="Assets/SagittariusA/Resources/Ganymede/GanymedeEnvironment.fbx")return;
            var model=(ModelImporter)assetImporter;
            model.isReadable=true; // Runtime terrain and equipment collision cooking.
            model.importCameras=false;model.importLights=false;model.importAnimation=false;
            model.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
        }
    }
}
