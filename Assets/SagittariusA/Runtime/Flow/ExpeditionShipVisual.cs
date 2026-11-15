using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SagittariusA.Gameplay.Flow
{
    // The same team-authored ship used by the transition films, at a common gameplay scale.
    public sealed class ExpeditionShipVisual:MonoBehaviour
    {
        private readonly List<Material> owned=new List<Material>();
        public static GameObject Create(Transform parent,Vector3 ground,float height=14f,string resource="Ships/ExpeditionShip")
        {
            var prefab=Resources.Load<GameObject>(resource);if(prefab==null)return null;
            var root=new GameObject("Expedition ship / film model");root.transform.SetParent(parent,false);root.transform.position=ground;
            var owner=root.AddComponent<ExpeditionShipVisual>();var model=Instantiate(prefab,root.transform,false);
            var renderers=model.GetComponentsInChildren<MeshRenderer>();if(renderers.Length==0){Destroy(root);return null;}
            Bounds Bounds(){var b=renderers[0].bounds;foreach(var r in renderers.Skip(1))b.Encapsulate(r.bounds);return b;}
            var bounds=Bounds();model.transform.localScale*=height/Mathf.Max(.01f,bounds.size.y);bounds=Bounds();
            model.transform.position+=new Vector3(ground.x-bounds.center.x,ground.y-bounds.min.y,ground.z-bounds.center.z);
            var palette=new Dictionary<string,Material>();
            foreach(var r in renderers)
            {
                var mats=r.sharedMaterials;
                for(int i=0;i<mats.Length;i++)
                {
                    string name=mats[i]==null?"Steel":mats[i].name;
                    if(!palette.TryGetValue(name,out var m))
                    {
                        bool dark=name.Contains("HeatShield")||name.Contains("Black")||name.Contains("Inner");
                        m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name+" flight"};
                        m.color=dark?new Color(.045f,.055f,.065f):name.Contains("Dark")?new Color(.25f,.29f,.32f):new Color(.65f,.69f,.72f);
                        m.SetFloat("_Metallic",dark?.1f:.65f);m.SetFloat("_Smoothness",dark?.18f:.42f);palette[name]=m;owner.owned.Add(m);
                    }
                    mats[i]=m;
                }
                r.sharedMaterials=mats;
            }
            var collider=root.AddComponent<CapsuleCollider>();collider.height=height;collider.radius=height*.105f;collider.center=new Vector3(0,height*.5f,0);
            return root;
        }
        private void OnDestroy(){foreach(var m in owned)if(m!=null)Destroy(m);}
    }
}
