using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using SagittariusA.Gameplay.Data;

var catalog = JsonSerializer.Deserialize<EarthCatalog>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "FarSignalEarth.json")), new JsonSerializerOptions { IncludeFields = true });
var simulation = new DraftEarthSimulationAdapter();
int count = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); count++; Console.WriteLine("PASS: " + name); }
EarthChecks Ready(EarthBuild build)
{
    var result = new EarthChecks { powerTestCompleted = true, cabinOn = true, radioOn = true, laserTested = build.cargo.Contains("laser"), padId = "main", arrivalMinute = 0 };
    int slot = 0;
    foreach (var id in build.cargo)
    { result.placements.Add(new CargoPlacement { itemId = id, firstSlot = slot, locked = true }); slot += catalog.cargo.First(p => p.id == id).slots; }
    return result;
}
var build = new EarthBuild();
var a = simulation.Assess(catalog, build, new EarthChecks());
Check(a.costUsd == 93000000 && a.massKg == 9570 && a.payloadKg == 10100, "starter matches latest brief");
Check(!a.CanLaunch && a.issues.Count(i => i.code.EndsWith("-check")) == 3, "unplayed minigames block launch");
a = simulation.Assess(catalog, build, Ready(build));
Check(a.CanLaunch && a.slotsUsed == 3 && a.supplyKw == 5 && a.batteryKwh == 6 && a.fuelUnits == 15, "validated starter passes");
build.parts[2] = "f2"; build.parts[4] = "c2"; build.cargo.Add("fuel");
a = simulation.Assess(catalog, build, Ready(build));
Check(a.costUsd == 99000000 && a.massKg == 10320 && a.MassMarginKg == -340 && !a.CanLaunch, "fuel build blocks by exactly 340 kg");
build.parts[1] = "n2";
a = simulation.Assess(catalog, build, Ready(build));
Check(a.costUsd == 101000000 && a.MassMarginKg == 10 && a.CanLaunch && a.issues.Any(i => i.code == "margin"), "N-2 correction passes with 10 kg warning");
build = new EarthBuild { parts = new[] { "l2", "n2", "f2", "e1", "c2", "p2" } }; build.cargo.Add("laser");
a = simulation.Assess(catalog, build, Ready(build));
Check(a.costUsd == 114000000 && a.massKg == 10270 && a.payloadKg == 11130 && a.CanLaunch, "laser build matches brief");
build.parts[5] = "p1";
a = simulation.Assess(catalog, build, Ready(build));
Check(a.CanLaunch && a.issues.Any(i => i.code == "laser" && !i.blocksLaunch), "laser overload remains recoverable with hand tools");
var checks = Ready(build); checks.laserTested = false;
Check(!simulation.Assess(catalog, build, checks).CanLaunch, "carried laser must be tested");
build = new EarthBuild(); checks = Ready(build); checks.arrivalMinute = 4;
a = simulation.Assess(catalog, build, checks);
Check(a.CanLaunch && a.issues.Any(i => i.code == "shadow"), "Beacon closing minute excluded, warning does not gate Earth launch");
checks.padId = "ridge";
Check(!simulation.Assess(catalog, build, checks).issues.Any(i => i.code == "shadow"), "Ridge remains lit at minute four");
build.parts[3] = "e3";
Check(simulation.Assess(catalog, build, checks).issues.Any(i => i.code == "pad-fuel"), "12 fuel warns for Ridge");
checks.arrivalMinute = float.NaN;
Check(!simulation.Assess(catalog, build, checks).CanLaunch, "invalid landing time blocked");
build = new EarthBuild(); checks = Ready(build); checks.placements[0].locked = false;
Check(!simulation.Assess(catalog, build, checks).CanLaunch, "loose panel blocks launch");
checks = Ready(build); checks.placements[1].firstSlot = 1;
Check(!simulation.Assess(catalog, build, checks).CanLaunch, "overlapping cargo rejected");
build.parts[4] = "c3"; build.parts[2] = "f3"; checks = Ready(build); checks.placements[0].firstSlot = 2; checks.placements[1].firstSlot = 0;
Check(simulation.Assess(catalog, build, checks).issues.Any(i => i.code == "cargo-check"), "panel cannot bridge rows in six-slot bay");
build = new EarthBuild(); build.parts[1] = "n3";
Check(simulation.Assess(catalog, build, Ready(build)).issues.Any(i => i.code == "mount"), "wrong mount rejected");
build = new EarthBuild(); build.cargo.Remove("kit");
Check(simulation.Assess(catalog, build, Ready(build)).issues.Any(i => i.code == "required-kit"), "missing required kit rejected");
build = new EarthBuild(); build.parts[4] = "c4";
Check(simulation.Assess(catalog, build, Ready(build)).issues.Any(i => i.code == "cover"), "oversize bay rejected");
build = new EarthBuild(); build.parts[0] = "missing";
Check(!simulation.Assess(catalog, build, new EarthChecks()).CanLaunch, "unknown IDs rejected safely");
build = new EarthBuild(); checks = Ready(build); checks.placements[0].firstSlot = int.MaxValue;
Check(simulation.Assess(catalog, build, checks).issues.Any(i => i.code == "cargo-check"), "out-of-range cargo cannot overflow slot validation");
Check(!simulation.Assess(catalog, build, null).CanLaunch, "missing check state rejected safely");
Console.WriteLine($"{count} checks passed.");
