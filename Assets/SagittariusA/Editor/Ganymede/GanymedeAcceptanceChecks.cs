using System;
using System.Collections.Generic;
using System.Linq;
using SagittariusA.Gameplay.Data;
using SagittariusA.Gameplay.Flow;
using SagittariusA.Gameplay.Mars;
using SagittariusA.Gameplay.Ganymede;
using UnityEditor;
using UnityEngine;

namespace SagittariusA.EditorTools.Ganymede
{
    public static class GanymedeAcceptanceChecks
    {
        [MenuItem("Far Signal/Ganymede/Run Campaign Acceptance Checks")]
        public static void Run() { int count = RunAll(); Debug.Log("GANYMEDE ACCEPTANCE PASSED: " + count + " groups."); }
        public static int RunAll()
        {
            int passed = 0;
            var basic = New(); Check(!basic.BeginCheckout() && basic.PowerLoad == 5,"camera overload rejected");
            Check(basic.SetCamera(false) && basic.BeginCheckout(),"checkout start"); Check(!basic.SetCamera(true) && basic.PowerLoad == 5,"last switch rejected");
            Check(!basic.Land(),"early landing"); Tick(basic,3.1f); Check(basic.State.checkoutComplete && basic.PowerLoad==3,"checkout preserves essential power"); passed++;

            var s = Surface(); Check(!s.Ascend(),"early ascent"); Check(s.Collect(),"collect");
            Check(!s.Place(GanymedeMission.SiteA,false),"invalid placement"); Sample(s,GanymedeMission.SiteB); Check(!s.Ascend(),"one-site ascent");
            Sample(s,GanymedeMission.SiteA); Check(s.Record(GanymedeMission.SiteA).simulatedDoseRate==24 && s.Record(GanymedeMission.SiteB).simulatedDoseRate==16,"reverse-order values");
            Check(!s.Compare(GanymedeMission.SiteB,GanymedeMission.SiteA,true),"swapped labels"); Check(!s.Compare(GanymedeMission.SiteA,GanymedeMission.SiteB,false),"unsafe conclusion");
            Check(s.Compare(GanymedeMission.SiteA,GanymedeMission.SiteB,true) && !s.Ascend(),"uplink required after comparison");Build(s,GanymedeMission.Relay);Check(s.Ascend(),"all three stations permit departure");passed++;

            var store = new MemoryGanymedeStore(); var h = Handoff(); var interrupted = Surface(h,store); interrupted.Collect(); Build(interrupted,GanymedeMission.SiteA);interrupted.Place(GanymedeMission.SiteA,true);
            Level(interrupted); Check(interrupted.BeginSample(),"sample"); Tick(interrupted,1); interrupted.Tick(.1f,false);
            Check(!interrupted.Sampling && interrupted.State.instrumentLeveled && interrupted.Record(GanymedeMission.SiteA)==null,"walk-away recovery");
            interrupted.Flush(); var reload = new GanymedeMission(h,store); Check(reload.State.instrumentLeveled && reload.State.placedSite==GanymedeMission.SiteA,"placed reload");
            Check(reload.BeginSample(),"sample retry"); Tick(reload,3.1f); Check(reload.Record(GanymedeMission.SiteA)!=null,"retry saved");
            Check(!reload.BeginSample(),"no duplicate sample"); Check(reload.Pack() && reload.State.records.Count==1,"pack retains record"); passed++;

            var orbit = Orbit(); Check(!orbit.Deploy() && orbit.State.deploymentCount==0,"early deployment closes clamps");
            orbit.ConfirmCrew(); Check(!orbit.Deploy(),"must reach point"); orbit.ReachDeploymentPoint(); Check(orbit.Deploy(),"release");
            Check(!orbit.Deploy() && orbit.State.deploymentCount==1 && orbit.State.antennaLocked,"one-time light release"); passed++;

            var directionalH = Handoff("directional","frequent"); var directionalStore = new MemoryGanymedeStore(); var directional = Orbit(directionalH,directionalStore);
            directional.ConfirmCrew(); directional.ReachDeploymentPoint(); directional.Deploy(); Check(!directional.BeginContact(),"no signal before aim");
            Tick(directional,1.1f); Check(!directional.State.antennaLocked,"missed alignment"); directional.AdjustAntenna(.9f); Tick(directional,1.1f);
            Check(directional.State.antennaLocked && directional.WindowSeconds==14 && directional.PacketSeconds("full-graph")==12,"directional frequent settings");
            var deployedReload = new GanymedeMission(directionalH,directionalStore); Check(deployedReload.State.deploymentCount==1 && deployedReload.State.antennaLocked && !deployedReload.Deploy(),"deployment reload"); passed++;

            var packets = Contact(); packets.MovePacketFirst("full-graph"); packets.BeginContact(); Tick(packets,10.2f);
            Check(packets.State.received.SequenceEqual(new[]{"full-graph"}) && !packets.RequiredPackets && packets.State.queue.Count==2,"graph-first missed required packets");
            Check(packets.PrioritizeRequired() && packets.BeginContact(),"retry contact"); Tick(packets,6.2f); Check(packets.RequiredPackets,"required packets recovered"); passed++;

            var packetStore = new MemoryGanymedeStore(); var packetH = Handoff(); var partial = Contact(packetH,packetStore);
            partial.BeginContact(); Tick(partial,2.1f); Check(partial.State.received.Contains("health"),"health received");
            var partialReload = new GanymedeMission(packetH,packetStore); Check(!partialReload.ContactRunning && partialReload.State.received.Contains("health") && !partialReload.State.queue.Contains("health"),"receipt durable");
            partialReload.BeginContact(); Tick(partialReload,4.1f); Check(partialReload.RequiredPackets,"second packet after reload");
            Check(partialReload.Acknowledge() && partialReload.OpenFlightLog(),"ending"); Check(partialReload.State.handoff.playerName=="Нара" && partialReload.Notice.Contains("saved"),"unicode preserved");
            Check(!partialReload.State.photoSaved && !partialReload.State.received.Contains("full-graph"),"optionals truly skipped"); passed++;

            var photoStore = new MemoryGanymedeStore(); var photoH = Handoff(); var photo = Surface(photoH,photoStore);
            Check(!photo.SavePhoto(false,true),"bad photo labels"); Check(photo.SavePhoto(true,true),"photo saved");
            Check(new GanymedeMission(photoH,photoStore).State.photoSaved,"photo reload"); passed++;

            foreach (string antenna in new[]{"light","directional"}) foreach(string mode in new[]{"standard","frequent"})
            {
                var m = Contact(Handoff(antenna,mode)); if(!m.State.antennaLocked){m.AdjustAntenna(.9f);Tick(m,1.1f);}
                m.BeginContact();Tick(m,6.1f);Check(m.RequiredPackets,"required packets fit "+antenna+"/"+mode);
                Check(m.ParticleSamples().Length==(mode=="frequent"?12:6),"science sample count");
            }
            passed++;
            var invalid = Handoff(); invalid.leftClamp=false; Check(!GanymedeMission.ValidHandoff(invalid),"unlocked handoff rejected");
            var fresh = new GanymedeMission(Handoff(),packetStore); Check(fresh.State.phase==GanymedePhase.Transit && fresh.State.received.Count==0,"different expedition never resumes old save");
            fresh.Tick(float.NaN,true); Check(!float.IsNaN(fresh.State.missionSeconds),"invalid tick ignored"); passed++;
            var buildStore=new MemoryGanymedeStore();var buildH=Handoff();var build=Surface(buildH,buildStore);build.Collect();
            Check(!build.Place(GanymedeMission.SiteA,true),"survey requires assembly");Check(!build.CommissionStation(GanymedeMission.SiteA),"no free station");
            Check(!build.FitStationPart(GanymedeMission.SiteA,"head","head"),"base first");Check(!build.FitStationPart(GanymedeMission.SiteA,"base","power"),"wrong socket");
            Check(build.FitStationPart(GanymedeMission.SiteA,"base","base"),"fit base");
            var resumedBuild=new GanymedeMission(buildH,buildStore);Check(resumedBuild.State.installedParts.Contains(GanymedeMission.SiteA+"/base"),"partial construction survives reload");
            resumedBuild.FitStationPart(GanymedeMission.SiteA,"head","head");resumedBuild.FitStationPart(GanymedeMission.SiteA,"power","power");Check(resumedBuild.CommissionStation(GanymedeMission.SiteA),"commission");
            Check(!resumedBuild.CommissionStation(GanymedeMission.SiteA)&&new GanymedeMission(buildH,buildStore).State.radiationStationBuilt,"commission once and restore");passed++;
            return passed;
        }
        public static MarsGanymedeHandoff Handoff(string antenna="light",string mode="standard")
        {
            // Build the incoming contract with the production Mars rules too.
            var moon = new MoonMarsHandoff { sourceRunId=Guid.NewGuid().ToString("N"),missionId="pole-7",nextMissionId="mars-staging",commanderId="test",playerName="Нара",build=new EarthBuild(),remainingFuelUnits=5,batteryKwh=6,supplyKw=5,routeUploaded=true,relayRestored=true };
            var mars = new MarsMission(moon,new MemoryMarsDraftStore());
            for(int i=0;i<500 && mars.State.phase==MarsPhase.AntennaRecovery;i++)mars.TickManualAlignment(mars.State.antennaAngle < mars.State.antennaTarget-.3f ? 1 : 0,.02f);
            Act(mars,MarsActionKind.QueueArrival);Act(mars,MarsActionKind.SendNextPacket);Act(mars,MarsActionKind.Land);
            if(antenna=="directional")Act(mars,MarsActionKind.SelectDirectionalAntenna);
            if(mode=="frequent")Act(mars,MarsActionKind.SetFrequentSensor);
            foreach(var action in new[]{MarsActionKind.SelectSensorPart,MarsActionKind.PlaceSensorMount,MarsActionKind.SelectAntennaPart,MarsActionKind.PlaceAntennaMount,MarsActionKind.SelectShieldPart,MarsActionKind.PlaceShieldMount,MarsActionKind.ConnectPower,MarsActionKind.ConnectData,MarsActionKind.BenchPowerOn,MarsActionKind.BenchSample,MarsActionKind.BenchTransmit,MarsActionKind.SeatOrbiter,MarsActionKind.CloseLeftClamp,MarsActionKind.CloseRightClamp,MarsActionKind.ReceiveDeploymentPlan,MarsActionKind.CompleteMars})Act(mars,action);
            var result=mars.GetGanymedeHandoff();Check(GanymedeMission.ValidHandoff(result),"real Mars handoff accepted");return result;
        }
        private static void Act(MarsMission m,MarsActionKind kind){Check(m.Act(kind).Accepted,"Mars prerequisite "+kind+" / "+m.Notice);}
        private static GanymedeMission New()=>new GanymedeMission(Handoff(),new MemoryGanymedeStore());
        private static GanymedeMission Surface(MarsGanymedeHandoff h=null,MemoryGanymedeStore store=null)
        {var m=new GanymedeMission(h??Handoff(),store??new MemoryGanymedeStore());m.SetCamera(false);m.BeginCheckout();Tick(m,3.1f);Check(m.Land(),"land");return m;}
        private static GanymedeMission Orbit(MarsGanymedeHandoff h=null,MemoryGanymedeStore store=null)
        {var m=Surface(h,store);m.Collect();Sample(m,GanymedeMission.SiteA);Sample(m,GanymedeMission.SiteB);Build(m,GanymedeMission.Relay);m.Compare(GanymedeMission.SiteA,GanymedeMission.SiteB,true);Check(m.Ascend(),"ascend");return m;}
        private static GanymedeMission Contact(MarsGanymedeHandoff h=null,MemoryGanymedeStore store=null)
        {var m=Orbit(h,store);m.ConfirmCrew();m.ReachDeploymentPoint();Check(m.Deploy(),"deploy");return m;}
        private static void Sample(GanymedeMission m,string id){Build(m,id);Check(m.Place(id,true),"place "+id);Level(m);Check(m.BeginSample(),"sample "+id);Tick(m,3.1f);Check(m.Record(id)!=null,"record "+id);Check(m.Pack(),"pack "+id);}
        private static void Build(GanymedeMission m,string id){if(m.StationBuilt(id))return;foreach(string p in new[]{"base","head","power"})Check(m.FitStationPart(id,p,p),"fit "+p);Check(m.CommissionStation(id),"commission "+id);}
        private static void Level(GanymedeMission m){Check(!m.ConfirmLevel(),"reject tilted");m.AdjustLevel(-.5f,.4f);Check(m.ConfirmLevel(),"level");}
        private static void Tick(GanymedeMission m,float seconds){for(int i=0;i<(int)Math.Ceiling(seconds/.05f);i++)m.Tick(.05f,true);}
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("Ganymede check failed: "+message);}
    }
}
