using System;
using System.Linq;
using SagittariusA.Gameplay.Data;
using UnityEditor;
using UnityEngine;

namespace SagittariusA.EditorTools.Ganymede
{
    public static class EarthCargoChecks
    {
        [MenuItem("Far Signal/Earth/Run Cargo Packing Checks")]
        public static void Run()
        {
            var catalog=JsonUtility.FromJson<EarthCatalog>(Resources.Load<TextAsset>("FarSignalEarth").text);
            Debug.Log("EARTH CARGO PASSED: "+RunAll(catalog)+" groups.");
        }
        public static int RunAll(EarthCatalog catalog)
        {
            int passed=0;var build=new EarthBuild();var initial=new EarthChecks();var packing=new EarthCargoPacking(catalog,build,initial);
            Check(!packing.Complete,"unpacked");Check(!packing.Lock("panel"),"unplaced lock");Check(!packing.Place("panel",packing.Capacity-1),"panel edge");
            Check(packing.Place("panel",0),"panel fit");Check(!packing.Place("kit",1),"overlap");Check(packing.Place("kit",2),"kit fit");
            Check(!packing.Complete,"loose hold");packing.Lock("panel");Check(!packing.Complete,"one lock hold");packing.Lock("kit");Check(packing.Complete,"secured");
            Check(!new DraftEarthSimulationAdapter().Assess(catalog,build,packing.Result()).issues.Any(x=>x.code=="cargo-check"),"session simulation accepts");passed++;
            Check(initial.placements.Count==0,"cancel leaves original");var restored=new EarthCargoPacking(catalog,build,packing.Result());Check(restored.Complete,"saved locks restore");
            Check(restored.Remove("kit")&&!restored.Complete,"remove invalidates");Check(!restored.Place("missing",0),"unknown cargo");passed++;
            var six=build.Copy();six.parts[4]="c3";var wide=new EarthCargoPacking(catalog,six,new EarthChecks());Check(wide.Capacity==6,"six bay");
            Check(!wide.Place("panel",2),"no row wrapping");Check(wide.Place("panel",1),"same row");wide.Lock("panel");Check(wide.Place("panel",0)&&!wide.Placement("panel").locked,"move unlocks");
            Check(!wide.Place("kit",-1),"negative slot");Check(wide.Place("kit",5),"last slot");passed++;
            foreach(var preset in EarthReadyBuild.All)
            {
                var b=preset.Create();var m=new EarthCargoPacking(catalog,b,new EarthChecks());int slot=0;
                foreach(var item in m.Items.OrderByDescending(x=>x.slots)){Check(m.Place(item.id,slot),"preset "+preset.Id);m.Lock(item.id);slot+=item.slots;}
                Check(m.Complete,"preset complete "+preset.Id);
            }
            passed++;return passed;
        }
        private static void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("Cargo check failed: "+text);}
    }
}
