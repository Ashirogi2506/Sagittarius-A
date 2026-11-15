using System;
using System.Linq;
using SagittariusA.Gameplay.Data;
using UnityEngine;
using UnityEngine.UI;
using MissionSimulation.FarSignal;

namespace SagittariusA.Gameplay.Flow
{
    // Uses the simulation teammate's power rules; results commit through the existing run/revision gate.
    public sealed class EarthFlightMinigames:MonoBehaviour,IEarthMinigameRunner
    {
        private GameObject ui;private EarthMinigameContext context;private Action<EarthMinigameResult> complete;private EarthPowerTest power;private string pad;private float minute=2;private bool laserAttempted;
        public bool Supports(EarthActivity a)=>a==EarthActivity.Power||a==EarthActivity.Landing;
        public void Begin(EarthMinigameContext c,Action<EarthMinigameResult> finished){context=c;complete=finished;pad=c.checks.padId;minute=2;laserAttempted=false;power=new EarthPowerTest(c.build.parts[5],c.supplyKw,c.build.cargo.Contains("laser"));Draw();}
        private void Draw()
        {
            if(ui!=null){ui.SetActive(false);Destroy(ui);}ui=ReleaseServices.Canvas("Flight systems",500);var p=ReleaseServices.Box(ui.transform,"Flight panel",.15f,.12f,.7f,.76f,new Color(.025f,.04f,.065f,.99f));
            ReleaseServices.Text(p,context.activity==EarthActivity.Power?"IN FLIGHT / POWER CHECK":"LUNAR APPROACH / LANDING PLAN",.05f,.85f,.9f,.1f,34);
            if(context.activity==EarthActivity.Power)
            {
                var state=power.Snapshot();ReleaseServices.Text(p,$"Supply {context.supplyKw} kW · Cabin 2 kW · Radio 1 kW · Laser 3 kW\nSwitch on the essentials. Test the optional laser if carried.",.05f,.65f,.9f,.16f,24);
                ReleaseServices.Button(p,"CABIN  "+(state.cabinOn?"ON":"OFF"),.05f,.48f,.4f,.1f,()=>{power.SetCabin(!state.cabinOn);Draw();});
                ReleaseServices.Button(p,"RADIO  "+(state.radioOn?"ON":"OFF"),.53f,.48f,.4f,.1f,()=>{power.SetRadio(!state.radioOn);Draw();});
                if(context.build.cargo.Contains("laser"))ReleaseServices.Button(p,"TEST LASER",.05f,.32f,.4f,.1f,()=>{power.TryLaser();laserAttempted=true;Draw();});
                ReleaseServices.Text(p,state.laserTripped?"Laser overload detected. Essentials protected; use hand tools on the Moon.":state.laserOn?"Laser ready. All checks passed.":power.EssentialsPassed?"Cabin and radio ready.":"Enable cabin and radio.",.05f,.18f,.9f,.12f,23);
                ReleaseServices.Button(p,"SAVE POWER CHECK",.52f,.05f,.4f,.09f,()=>{var s=power.Snapshot();context.checks.cabinOn=s.cabinOn;context.checks.radioOn=s.radioOn;context.checks.powerTestCompleted=true;context.checks.laserTested=laserAttempted;Finish(true);}).interactable=power.EssentialsPassed&&(!context.build.cargo.Contains("laser")||laserAttempted);
            }
            else
            {
                var catalog=GetComponent<EarthGameplaySession>().Catalog;
                ReleaseServices.Text(p,$"Fuel available: {context.fuelUnits} units. Choose a sunlit pad and arrival time.",.05f,.73f,.9f,.09f,23);
                for(int i=0;i<catalog.landingMap.Length;i++){var option=catalog.landingMap[i];ReleaseServices.Button(p,(pad==option.id?"✓ ":"")+option.name+" · "+option.fuelUnits+" fuel · daylight "+option.openMinute+"–"+option.closeMinute+" min",.05f,.61f-i*.11f,.9f,.09f,()=>{pad=option.id;Draw();});}
                ReleaseServices.Text(p,"ARRIVAL MINUTE: "+minute.ToString("0"),.05f,.19f,.5f,.09f,26);ReleaseServices.Button(p,"−",.60f,.19f,.13f,.09f,()=>{minute=Mathf.Max(0,minute-1);Draw();});ReleaseServices.Button(p,"+",.8f,.19f,.13f,.09f,()=>{minute=Mathf.Min(10,minute+1);Draw();});
                var selected=catalog.landingMap.FirstOrDefault(x=>x.id==pad);bool valid=selected!=null&&context.fuelUnits>=selected.fuelUnits&&minute>=selected.openMinute&&minute<selected.closeMinute;
                ReleaseServices.Button(p,valid?"CONFIRM APPROACH":"SELECT A FUEL-SAFE, SUNLIT APPROACH",.38f,.05f,.57f,.09f,()=>{context.checks.padId=pad;context.checks.arrivalMinute=minute;Finish(true);}).interactable=valid;
            }
            ReleaseServices.Button(p,"CANCEL",.05f,.05f,.25f,.09f,()=>Finish(false));
        }
        private void Finish(bool ok){var cb=complete;complete=null;if(ui!=null){ui.SetActive(false);Destroy(ui);}cb?.Invoke(new EarthMinigameResult{runId=context.runId,buildRevision=context.buildRevision,activity=context.activity,completed=ok,checks=context.checks});}
        public void Pause(){if(ui!=null)ui.SetActive(false);}public void Resume(){if(ui!=null)ui.SetActive(true);}public void Cancel(){if(complete!=null)Finish(false);}
    }
}
