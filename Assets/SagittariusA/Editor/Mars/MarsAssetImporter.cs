using UnityEditor;
namespace SagittariusA.EditorTools.Mars
{
    public sealed class MarsAssetImporter : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if(assetPath!="Assets/SagittariusA/Resources/Mars/MarsEnvironment.fbx")return;
            var model=(ModelImporter)assetImporter;model.isReadable=true;
            model.importCameras=false;model.importLights=false;model.importAnimation=false;
            model.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
        }
    }
}
