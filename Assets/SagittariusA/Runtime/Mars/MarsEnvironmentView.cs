using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SagittariusA.Gameplay.Mars
{
    public sealed class MarsEnvironmentView : MonoBehaviour
    {
        [Serializable] private sealed class MaterialList { public MaterialSpec[] materials=Array.Empty<MaterialSpec>(); }
        [Serializable] private sealed class MaterialSpec { public string name=""; public float[] color=Array.Empty<float>(); public float metallic=0,roughness=.75f; }
        private GameObject model;
        private Vector3 origin,axisX,axisY,axisZ;
        private readonly List<Material> materials=new List<Material>();
        private readonly List<Collider> ground=new List<Collider>();
        public bool Loaded=>model!=null;
        public Vector3 Spawn {get;private set;}
        public MeshRenderer FindMesh(string prefix)=>model==null?null:model.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r=>r.name.StartsWith(prefix,StringComparison.Ordinal));
        public Vector3 Anchor(string name)=>model.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Anchor_"+name).position;
        public Vector3 Point(float x,float y,float z)
        {return origin+axisX*x+axisY*y+axisZ*z;}
        public bool Initialize()
        {
            var prefab=Resources.Load<GameObject>("Mars/MarsEnvironment");if(prefab==null)return false;
            model=Instantiate(prefab,transform);model.name="Mars authored assembly environment";
            var terrain=FindMesh("Mars_Terrain");if(terrain==null)throw new InvalidOperationException("Mars export is missing terrain.");
            model.transform.localScale*=400f/terrain.bounds.size.x;
            origin=Anchor("Origin");axisX=Anchor("AxisX")-origin;axisY=Anchor("AxisY")-origin;axisZ=Anchor("AxisZ")-origin;
            var json=Resources.Load<TextAsset>("Mars/MarsMaterials");
            var specs=json==null?Array.Empty<MaterialSpec>():JsonUtility.FromJson<MaterialList>(json.text).materials;
            var converted=new Dictionary<string,Material>();
            foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                var list=renderer.sharedMaterials;
                for(int i=0;i<list.Length;i++)
                {
                    var source=list[i];if(source==null)continue;
                    if(!converted.TryGetValue(source.name,out var mat))
                    {
                        var spec=specs.FirstOrDefault(s=>s.name==source.name);
                        mat=new Material(Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard")){name=source.name+" Unity"};
                        var texture=source.name=="MarsTerrain"?Resources.Load<Texture2D>("Mars/MarsTerrain"):null;
                        mat.color=texture!=null?Color.white:spec?.color?.Length>=3?new Color(spec.color[0],spec.color[1],spec.color[2],1):source.color;
                        if(texture!=null){mat.mainTexture=texture;if(mat.HasProperty("_BaseMap"))mat.SetTexture("_BaseMap",texture);}
                        if(mat.HasProperty("_Metallic"))mat.SetFloat("_Metallic",spec?.metallic??0);
                        if(mat.HasProperty("_Smoothness"))mat.SetFloat("_Smoothness",1-(spec?.roughness??.75f));
                        converted[source.name]=mat;materials.Add(mat);
                    }
                    list[i]=mat;
                }
                renderer.sharedMaterials=list;
                bool support=renderer==terrain||renderer.name.StartsWith("Assembly bay | Floor")||renderer.name.StartsWith("Loading lane | Ramp")||renderer.name=="Mars landing pad";
                // Distant scatter and lettering do not need physics meshes.
                if(!support&&(renderer.bounds.center-Point(2,0,0)).sqrMagnitude>75*75)continue;
                if(!support&&renderer.bounds.size.magnitude<.22f)continue;
                var mesh=renderer.GetComponent<MeshFilter>();if(mesh==null||mesh.sharedMesh==null)continue;
                var collider=renderer.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=mesh.sharedMesh;
                if(support)ground.Add(collider);
            }
            Physics.SyncTransforms();var spawn=Anchor("MarsSpawn");Spawn=new Vector3(spawn.x,GroundAt(spawn)+.12f,spawn.z);
            return true;
        }
        public float GroundAt(Vector3 at)
        {
            float height=float.NegativeInfinity;
            foreach(var collider in ground)if(collider.Raycast(new Ray(at+Vector3.up*60,Vector3.down),out var hit,150))height=Mathf.Max(height,hit.point.y);
            return float.IsNegativeInfinity(height)?at.y:height;
        }
        private void OnDestroy(){foreach(var material in materials)if(material!=null)Destroy(material);}
    }
}
