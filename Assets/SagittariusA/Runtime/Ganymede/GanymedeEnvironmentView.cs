using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SagittariusA.Gameplay.Ganymede
{
    // One shared terrain instance. Construction groups are independent objects;
    // activating a station never reloads the scene or moves the player.
    public sealed class GanymedeEnvironmentView : MonoBehaviour
    {
        [Serializable] private sealed class MaterialList { public MaterialSpec[] materials=Array.Empty<MaterialSpec>(); }
        [Serializable] private sealed class MaterialSpec { public string name="";public float[] color=Array.Empty<float>();public float metallic=0,roughness=.7f; }
        private GameObject model, radiation, ice, relay;
        private readonly List<Material> ownedMaterials=new List<Material>();
        private readonly List<Collider> supports=new List<Collider>();
        public Vector3 Spawn {get;private set;}
        public Vector3 SiteA {get;private set;}
        public Vector3 SiteB {get;private set;}
        public Vector3 Overlook {get;private set;}
        public Vector3 Lander {get;private set;}
        public bool Loaded=>model!=null;
        public int ActiveStationCount=>(radiation!=null&&radiation.activeSelf?1:0)+(ice!=null&&ice.activeSelf?1:0)+(relay!=null&&relay.activeSelf?1:0);

        public bool Initialize()
        {
            var prefab=Resources.Load<GameObject>("Ganymede/GanymedeEnvironment");if(prefab==null)return false;
            model=Instantiate(prefab,transform);model.name="Ganymede shared environment";
            var terrain=FindMesh("Ganymede | continuous terrain");
            if(terrain==null)throw new InvalidOperationException("Ganymede terrain mesh missing from export.");
            // Normalize against the known 180 m terrain width, independent of FBX unit settings.
            model.transform.localScale*=180f/terrain.bounds.size.x;
            var specs=Resources.Load<TextAsset>("Ganymede/GanymedeMaterials");
            var definitions=specs==null?Array.Empty<MaterialSpec>():JsonUtility.FromJson<MaterialList>(specs.text).materials;
            var converted=new Dictionary<string,Material>();
            foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                var list=renderer.sharedMaterials;
                for(int i=0;i<list.Length;i++)
                {
                    var source=list[i];if(source==null)continue;
                    if(!converted.TryGetValue(source.name,out var mat))
                    {
                        var spec=definitions.FirstOrDefault(s=>s.name==source.name);
                        bool planet=source.name.StartsWith("GanymedeJupiter",StringComparison.Ordinal);
                        var shader=Shader.Find(planet?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit")??Shader.Find(planet?"Unlit/Texture":"Standard");
                        mat=new Material(shader){name=source.name+" Unity"};
                        var texture=source.name.StartsWith("Ganymede",StringComparison.Ordinal)?Resources.Load<Texture2D>("Ganymede/"+source.name):null;
                        mat.color=texture!=null?Color.white:spec?.color?.Length>=3?new Color(spec.color[0],spec.color[1],spec.color[2],1):source.color;
                        if(texture!=null){mat.mainTexture=texture;if(mat.HasProperty("_BaseMap"))mat.SetTexture("_BaseMap",texture);}
                        if(mat.HasProperty("_Metallic"))mat.SetFloat("_Metallic",spec?.metallic??0);
                        if(mat.HasProperty("_Smoothness"))mat.SetFloat("_Smoothness",1-(spec?.roughness??.7f));
                        converted[source.name]=mat;ownedMaterials.Add(mat);
                    }
                    list[i]=mat;
                }
                renderer.sharedMaterials=list;
                if(renderer.name.StartsWith("Jupiter")||renderer.name.Contains("horizon"))continue;
                var mesh=renderer.GetComponent<MeshFilter>();if(mesh==null||mesh.sharedMesh==null)continue;
                var collider=renderer.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=mesh.sharedMesh;
                if(renderer==terrain||renderer.name.StartsWith("Landing pad")||renderer.name.StartsWith("Survey plate"))supports.Add(collider);
            }
            radiation=FindGroup("Built_Radiation");ice=FindGroup("Built_IceSurvey");relay=FindGroup("Built_Uplink");
            if(radiation==null||ice==null||relay==null)throw new InvalidOperationException("Ganymede construction groups missing.");
            Physics.SyncTransforms();SiteA=Anchor("crater-plain");SiteB=Anchor("grooved-rise");Overlook=Anchor("photo-overlook");Lander=Anchor("Lander_Return");
            // Step away from the authored equipment case before creating the capsule.
            Spawn=Anchor("PlayerSpawn")+new Vector3(-1.6f,0,-1.6f);Spawn=new Vector3(Spawn.x,GroundAt(Spawn)+.14f,Spawn.z);
            SetBuilt(false,false,false);return true;
        }
        public void SetBuilt(bool a,bool b,bool c){radiation?.SetActive(a);ice?.SetActive(b);relay?.SetActive(c);}
        public MeshRenderer FindMesh(string prefix)=>model==null?null:model.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r=>r.name.StartsWith(prefix,StringComparison.Ordinal));
        private GameObject FindGroup(string name)=>model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name)?.gameObject;
        private Vector3 Anchor(string name)
        {var at=FindGroup("Anchor_"+name);if(at==null)throw new InvalidOperationException("Missing Ganymede anchor: "+name);return at.transform.position;}
        public float GroundAt(Vector3 position)
        {float height=float.NegativeInfinity;foreach(var col in supports)if(col.Raycast(new Ray(position+Vector3.up*40,Vector3.down),out var hit,100))height=Mathf.Max(height,hit.point.y);return float.IsNegativeInfinity(height)?position.y:height;}
        private void OnDestroy(){foreach(var mat in ownedMaterials)if(mat!=null)Destroy(mat);}
    }
}
