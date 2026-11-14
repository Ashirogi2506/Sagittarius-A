# Gameplay UI architecture

## Screen map

`Main Menu -> Mission Selection -> Briefing -> Configuration -> Review -> Simulation -> Results`

- A successful result can unlock the next mission.
- Retry returns to Configuration without requiring the player to repeat the briefing.
- The same screens must load different mission data for Earth, Moon, Mars, and Jupiter.

## Responsibility boundaries

- UI reads mission content and displays resource, validation, simulation, and result objects.
- UI sends `MissionConfiguration` to `IMissionSimulationService`.
- The simulation system owns formulas, compatibility rules, scoring, and outcome decisions.
- The 3D system receives visualization commands after selections are validated.
- Narrative text belongs in data assets, not large hard-coded C# strings.

The placeholder scene intentionally contains no simulation formulas. This prevents the displayed values from drifting away from the simulation programmer's calculations.

## Initial contracts

- `MissionDefinition`: objective, briefing, limits, available components, orbits, and launchers.
- `MissionConfiguration`: the player's selected component IDs, orbit ID, and launcher ID.
- `MissionResourceTotals`: calculated resource quantities with explicit units.
- `MissionValidationResult`: whether launch is allowed, calculated totals, and human-readable messages.
- `MissionResult`: outcome, score breakdown, explanations, and progression unlock flag.
- `IMissionSimulationService`: the integration boundary the simulation programmer implements.
- `GlossaryDatabase`: reusable player-facing definitions for technical terms.

## Resource conventions

- Cost: game credits. The science and balancing team decides how credits relate to real currency.
- Mass: kilograms (kg).
- Power supply and demand: watts (W). Keep them separate.
- Data generation and communications capacity: megabits per second (Mbps).
- Data storage: gigabits (Gb).
- Mission duration: days.
- Scientific value: gameplay points, not a physical unit.
- Risk: percentage from 0 to 100.

Do not silently convert units inside a UI view. Unit conversion belongs in a shared formatting helper or in the simulation layer.
