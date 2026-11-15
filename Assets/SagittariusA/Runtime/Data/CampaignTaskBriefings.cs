using SagittariusA.Gameplay.Flow;

namespace SagittariusA.Gameplay.Data
{
    public sealed class CampaignTaskBriefing
    {
        public string Id, Title, Speaker, Context, Purpose, Steps, Controls;
        public CampaignTaskBriefing(string id, string title, string context, string purpose, string steps, string controls, string speaker = "SAM / FLIGHT ENGINEER")
        { Id = id; Title = title; Context = context; Purpose = purpose; Steps = steps; Controls = controls; Speaker = speaker; }
    }

    // Adapted from FarSignal.Campaign-Team-Prompt.md: Shared rules; Character
    // dialogue; Earth chapter; Moon chapter sections 1, 2 and 4. These describe
    // the game's current controls, not real engineering procedures.
    public static class CampaignTaskBriefings
    {
        // Interrupt only at the four major Moon milestones. Every smaller
        // action still has an explanation available through H.
        public static bool IsMoonMilestone(string id) =>
            id == MoonInteractionIds.ConfirmDescent || id == MoonInteractionIds.OpenWheelCover ||
            id == MoonInteractionIds.DriveBridge || id == MoonInteractionIds.IsolateCircuit;

        public static CampaignTaskBriefing Moon(string id, MoonStageContext state)
        {
            string name = string.IsNullOrWhiteSpace(state?.playerName) ? "Commander" : "Commander " + state.playerName;
            switch (id)
            {
                case MoonInteractionIds.ConfirmDescent:
                    return B(id, "ARRIVE AT POLE-7", name + ", Pole-7's main power panel has failed. The living area and backup radio still work. Khulan needs the P-4 panel and repair kit you brought from Earth.",
                        "Land using the saved approach plan, then prepare the cargo cart beside the pad.",
                        "Confirm your selected landing pad. Its landing fuel is spent once. After landing, find the cart's status console.", "BEGIN TASK confirms the saved descent. WASD move / mouse look / Space jump.", "KHULAN / POLE-7");
                case MoonInteractionIds.StartCart:
                    return B(id,"CHECK THE CARGO CART","The cart will carry the P-4 panel and insulated repair kit to the base.",
                        "Find out whether the cart is ready before loading the supplies.","Start the cart at its console and read the fault message. A jammed wheel must be repaired before loading.");
                case MoonInteractionIds.SwitchCartOff:
                    return B(id,"ISOLATE THE CART MOTOR","The cart has power, but its right wheel is jammed.",
                        "Switch off the motor before opening the wheel cover.","Use the cart's power switch. Then move to the cover on the right wheel.");
                case MoonInteractionIds.OpenWheelCover:
                    return B(id,"REPAIR THE CARGO CART","The motor is off. Dust around the right wheel is stopping the cart from moving.",
                        "Free the wheel so the cart can carry the P-4 panel and kit to Pole-7.","Open the cover, brush all three dust patches, secure the cover and test the wheel. Then load both cargo cases.","Follow each repair panel's controls. Right click picks up cargo; left click loads it. H explains the current step.");
                case MoonInteractionIds.CleanPatch1: case MoonInteractionIds.CleanPatch2: case MoonInteractionIds.CleanPatch3:
                    return B(id,"BRUSH OUT THE DUST / " + id.Substring(id.Length-1),"Think of sand caught in a toy car's wheel. The cart's brush clears the gap around the hub.",
                        "Clear all three patches to free the wheel. A patch already cleaned stays clean.","Hold the mouse button and sweep across every dust deposit in this patch. Then clean the remaining patches.","Hold left click and move across the dust. Staying on one spot will not clean the whole patch.");
                case MoonInteractionIds.CloseWheelCover:
                    return B(id,"SECURE THE WHEEL COVER","The dust has been removed. The protective cover helps keep more dust out of the wheel.",
                        "Close and secure the cover before testing the motor.","Secure all four bolts in the displayed cross order. Then return to the cart console for a test.","Click bolts in order 1 > 3 > 2 > 4.");
                case MoonInteractionIds.RetestCart:
                    return B(id,"TEST THE REPAIRED WHEEL","The hub is clean and the cover is closed. Check the repair before putting cargo on the cart.",
                        "A successful wheel test unlocks cargo loading.","Complete three diagnostic cycles in the green range. A missed attempt can be retried without losing the repair.","Click TEST while the indicator is inside the green band.");
                case MoonInteractionIds.PickUpPanel:
                    return B(id,"PICK UP THE P-4 PANEL","Pole-7 needs this replacement power panel to restore its essential line and main relay.","Carry the panel to its marked position on the cart.","Pick up the P-4 case beside the cart. You can carry one item at a time.","Right click picks up cargo. Left click places it in the available cart slot.");
                case MoonInteractionIds.PickUpKit:
                    return B(id,"PICK UP THE REPAIR KIT","The insulated kit provides the basic hand-tool route through the Pole-7 repair.","The main mission can be completed without the optional laser.","Pick up the kit beside the cart, then place it in its marked cargo slot.","Right click picks up cargo. Left click places it in the available cart slot.");
                case MoonInteractionIds.LoadPanel: case MoonInteractionIds.LoadKit:
                    return B(id,id == MoonInteractionIds.LoadPanel?"SECURE THE PANEL ON THE CART":"SECURE THE KIT ON THE CART","Loose cargo is not ready for the service road.","Both the P-4 panel and repair kit must be loaded and secured before departure.","Place the item you are carrying in its marked cart slot. This action also engages its tie-down. Then load the other required item.");
                case MoonInteractionIds.DriveBridge:
                    return B(id,"FOLLOW THE OLD SERVICE ROAD","Khulan's old map shows a service road from the pad to Pole-7. It has not been checked since the fault.","Take the loaded cart to the bridge and inspect the route before continuing.","The cart follows the service route automatically. It parks at the survey bay and lets you out beside it.","Mouse look during the ride. Inspect the bridge after the cart parks.");
                case MoonInteractionIds.InspectBridge:
                    return B(id,"INSPECT THE BROKEN BRIDGE","The old route is blocked by missing deck panels and damaged rails.","The cart needs a firm detour; it cannot carry the panel across this gap.","Inspect the closure, then reach the survey point to compare the routes. Keep the cargo on the parked cart.");
                case MoonInteractionIds.SurveyRoute:
                    return B(id,"MARK A SAFE CARGO ROUTE","Sam: Mark the wide track. It's the one we can use with the panel on the cart.","Choose the firm detour around the broken bridge for this cargo delivery.","Survey and mark the firm route. Return to the cart switch to travel to Pole-7. Inspecting the loose branch is optional.");
                case MoonInteractionIds.InspectSteep:
                    return B(id,"CHECK THE LOOSE BRANCH / OPTIONAL","This branch is steep and covered in loose dust.","A short-looking route is not necessarily suitable for a loaded cart.","Inspect the branch and read the warning. Use the wider, firm detour for the delivery.");
                case MoonInteractionIds.DriveBase:
                    return B(id,"DELIVER THE SUPPLIES","The firm cargo route has been checked. Khulan is waiting at the cargo door under the mast.","Bring both secured repair items to the base.","Ride the cart along the checked detour. When it parks beside Pole-7, use the delivery marker at the cargo door.","Mouse look during the automatic ride. Left click the delivery marker after arrival.");
                case MoonInteractionIds.DeliverCargo:
                    return B(id,"HAND OVER THE PANEL AND KIT","Khulan: You made it. Set the panel here and I'll open the cargo door.","Move the required supplies from the parked cart into Pole-7.","Confirm delivery at the cargo door. The door opens, and the damaged circuit becomes the next objective.","Left click to confirm delivery.","KHULAN / POLE-7");
                case MoonInteractionIds.IsolateCircuit:
                    return B(id,"RESTORE POLE-7","The base kept its living area on backup power while the main panel was down.","Repair the main panel and relay so Khulan can share the checked cargo route.","Isolate the circuit, repair the connections and install P-4. Restore essential power, tune the relay, then upload the route.","Start with RELAY > AUXILIARY > MAIN BUS. Later panels show their own controls. H explains the current step.");
                case MoonInteractionIds.RepairCircuit:
                    return B(id,"REPAIR WITH THE INSULATED KIT","The hand-tool path is available to every valid basic Earth loadout.","Restore the cabinet connections so the replacement panel can be installed.","Connect each cable to its matching letter and colour. A wrong socket can be retried. The laser is an optional alternative only when carried and usable.","Drag each cable to the matching labelled socket.");
                case MoonInteractionIds.RepairLaser:
                    return B(id,"USE THE POWERED LASER / ALTERNATIVE","Your laser cutter gives you another way to complete this cabinet repair. Its carried and tested power requirements have been checked.","Complete the same repair milestone as the hand-tool route.","Trace the numbered repair seam in order. Finishing either method completes this step; you do not need to do both.","Hold left click and trace points 1 through 8.");
                case MoonInteractionIds.InstallPanel:
                    return B(id,"INSTALL THE P-4 PANEL","A power panel sends electricity where it needs to go. This is the replacement you carried from Earth.","Seat and secure the new panel before restoring power.","Move the P-4 module into its matching socket, then close all four corner latches.","Drag the panel into the socket. Click each latch after it seats.");
                case MoonInteractionIds.RestoreEssential:
                    return B(id,"RESTORE POLE-7'S ESSENTIAL LINES","The new panel is fitted. Restore essential services before bringing the main relay back online.","Route the base's existing supply to essential services and local radio. Research and extra lights are optional.","Join neighbouring points from SOL or BAT to the two critical systems. Avoid blocked points and stay within the cable limit. These are base supplies, not a portable battery from your cargo.","Click adjacent points or drag to lay cable. Click a cable to remove it. Select ENERGIZE GRID when both critical systems show POWERED.");
                case MoonInteractionIds.RestoreRelay:
                    return B(id,"BRING THE MAIN RELAY ONLINE","The essential line is restored. Khulan needs the main relay to share the updated cargo route and receive Mars's call.","Tune and lock the relay before uploading the survey.","Align frequency, phase and gain with their marked targets, then lock the signal.","Drag all three sliders onto their green targets. Click LOCK SIGNAL.");
                case MoonInteractionIds.UploadRoute:
                    return B(id,"SHARE THE CHECKED ROUTE","Khulan: The main relay's back, and I can see the route you marked. I'll send it to the next crew before they land.","Save and send the checked detour, then receive the Mars assembly invitation.","Confirm the route, verify its checksum, and transmit it. Mars will invite the crew to build and test the Jupiter orbiter.","Select ROUTE > CHECKSUM > TRANSMIT.","KHULAN / POLE-7");
                case MoonInteractionIds.InspectBeacon:
                    return B(id,"MARK THE DEAD BEACON / OPTIONAL","This beacon is on a side path from the cargo route.","Record its position for the next crew. This is optional and does not block the delivery.","Inspect and mark the beacon. If you still carry a portable battery, a separate action lets you leave it here.");
                case MoonInteractionIds.LeaveBattery:
                    return B(id,"LEAVE A BATTERY / OPTIONAL","A portable battery can support this beacon while Pole-7 completes its work.","Helping here means this battery will no longer be available on Mars.","BEGIN TASK leaves the carried portable battery here and records the choice. BACK keeps the battery in your inventory.","Choose BEGIN TASK only if you want to leave the battery.");
                case MoonInteractionIds.DriveLander:
                    return B(id,"RETURN TO THE LANDING PAD","Pole-7 is online and the Mars approach file has arrived.","Return to the ship when you are ready to continue the expedition.","Ride the cart back to the active landing pad. Optional beacon work can be skipped.","Mouse look during the ride. The cart lets you out beside the lander.");
                case MoonInteractionIds.ReturnToShip:
                    return B(id,"COMPLETE THE MOON STOP",name + ", the assembly bay is ready. Come to Mars and we'll test your Jupiter orbiter together.","Record the Moon outcome and carry your remaining inventory forward.","Return to the lander to open the Moon report. The delivered panel and any battery left at the beacon do not return to your cargo.","BEGIN TASK opens the Moon results.","MARS ENGINEER / RADIO");
                default: return null;
            }
        }

        public static CampaignTaskBriefing Earth(EarthActivity activity)
        {
            switch(activity)
            {
                case EarthActivity.Cargo: return B("earth.cargo","PACK AND LOCK THE CARGO","Pole-7 needs the P-4 panel and insulated repair kit. Optional equipment can help later, but it also uses cargo space and mass.","Secure every carried item before the Earth launch.","Fit each item into the available bay slots without overlap. Engage every cargo lock and review the result.","Follow the cargo module's placement and lock controls.");
                case EarthActivity.Power: return B("earth.power","CHECK POWER DURING FLIGHT","Keep the cabin and radio on during transit. The optional laser needs its own successful power test if carried.","Confirm essential loads fit your selected supply.","Keep essential systems on, test the carried equipment, and follow any overload correction. This is an in-flight check.","Use the flight panel's switches and read the live power totals.");
                default: return B("earth.landing","PREPARE THE LUNAR APPROACH","Beacon Pad uses less landing fuel and the shorter route. Ridge Pad offers a different approach and a longer trip to Pole-7.","Choose a valid pad and arrival time before descent.","Review the stored approach map, fuel reserve and available windows. Save the plan; actual descent spends its fuel once on the Moon.","Select a pad and valid arrival time using the landing panel.");
            }
        }

        // The imported tasks outside the live Moon flow remain practice modules.
        // They do not replace the newer campaign's assembly or transmission gates.
        public static CampaignTaskBriefing SharedPractice(string id)
        {
            switch(id)
            {
                case "earth-image-calibration": return B(id,"IMAGE CALIBRATION / PRACTICE","Prepare a readable observation image by matching the requested sensor settings.","A useful measurement needs the right instrument settings and a clear record of what was sampled.","Choose the requested image band, then adjust the settings and confirm. Use the on-screen target and feedback to correct a mismatch.","Click the labelled band buttons and drag the sliders. This practice result does not complete Earth launch checks.");
                case "moon-power-routing": return B(id,"LUNAR POWER ROUTING / PRACTICE","This supplied practice layout has limited cable and two sources with limited capacity.","Keep communications, thermal control and the science instrument powered; extra lighting is optional in this practice board.","Join neighbouring points from SOL or BAT. Avoid blocked points. Remove unnecessary cables if you reach the limit, then energize the grid.","Click adjacent points or drag to lay cable. Click a cable to remove it. The Pole-7 campaign uses its own essential-line profile.");
                case "mars-power-management": return B(id,"DUST-STORM POWER / PRACTICE","In this separate training scenario, a simulated dust storm reduces the displayed solar supply.","Balance the displayed demand and battery while keeping essential systems online.","Watch supply and charge. Shed optional loads during low supply, preserve essential loads, and restore optional work when you have margin.","Use the system switches and live meters. This is optional practice, not a replacement for the Mars orbiter assembly chapter.");
                case "jupiter-shield-allocation": return B(id,"SHIELD ALLOCATION / PRACTICE","This diagram uses a fictional mass budget and risk values to compare protection choices.","Protect critical zones within the displayed budget.","Read each zone's risk and protection requirement. Allocate a suitable shield, adjust the build to fit the mass limit, and confirm.","Select a shield and place it on the diagram. This practice does not alter the orbiter chosen on Mars.");
                default:return null;
            }
        }

        private static CampaignTaskBriefing B(string id,string title,string context,string purpose,string steps,string controls="Left click to use the highlighted object. Escape / Back closes this explanation.",string speaker="SAM / FLIGHT ENGINEER")
            => new CampaignTaskBriefing(id,title,context,purpose,steps,controls,speaker);
    }
}
