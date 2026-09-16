# Development Notes

## Project goal

Automate the repetitive parts of making a practical 3D-printable enclosure lid in Onshape without turning the project into a general-purpose enclosure generator.

The intended workflow is:

1. Model an enclosure normally.
2. Select its planar top rim face.
3. Run the custom Lid feature.
4. Adjust a small number of practical parameters.
5. Continue normal Part Studio modeling on the generated solids if desired.

## Proven development path

The feature was built incrementally and each stage was tested before the next was added:

### V0.1 — Lid plate
- Selected one planar top rim face.
- Extracted all boundary edges.
- Split boundary edges into connected loops.
- Identified the longest loop as the outside perimeter.
- Filled that perimeter.
- Extruded a separate lid solid.

### V0.2 — Locating plug
- Used the non-outer boundary as the enclosure opening.
- Filled and extruded the opening downward.
- Offset only the plug side faces inward by the fit-clearance value.
- Unioned the plug with the lid.

The original reference lid used a **solid locating plug**, not a thin locating ring. That simpler design was intentionally retained.

### V0.3 — Corner bosses
- Added four bosses to the enclosure.
- Each boss begins as a cylinder centered on an opening corner.
- The cylinder is intersected with the opening volume, producing a quarter-circle boss for ordinary 90° corners.
- Bosses extend from the enclosure floor to the underside of the locating plug.
- Bosses are unioned into the enclosure body.

### V0.4 — Screw system
- Added M2 / M2.5 / M3 / M4 presets.
- Added through-clearance holes and 90° countersinks to the lid.
- Added blind pilot holes to the enclosure bosses.
- Kept pilot diameter and depth user-editable.
- Tested screw placement first at D/4 and moved it to **D/6**, which produced a better material margin at the curved boss edge.

## Current derived rules

### Outer lid profile
The longest connected rim-edge loop is treated as the outside enclosure perimeter.

### Inner opening
All selected-rim boundary edges not belonging to the outer loop are treated as the opening.

### Screw placement
For boss diameter D:

```
screw inset = D / 6
```

The screw center is moved by that inset along each of the two opening edges meeting at the corner.

### Boss height
Bosses extend:

```
from enclosure floor
to locating-depth below the rim
```

This prevents the enclosure boss from colliding with the lid's locating plug.

### Countersink depth
The current screw presets use a 90° included angle, so:

```
countersink depth = countersink radius - clearance-hole radius
```

## Coding philosophy

- Preserve working geometry code.
- Add one capability at a time.
- Prefer native solid/topology operations over rebuilding geometry with sketches.
- Do not make the user re-enter dimensions already encoded in the enclosure.
- Fail clearly on unsupported topology rather than silently making malformed geometry.
- Keep generated helper bodies temporary.
- Leave the final enclosure and lid as normal solid parts.

## Current implementation details

The feature relies on standard FeatureScript operations including:

- `evPlane`
- `qAdjacent`
- `connectedComponents`
- `evLength`
- `qSubtraction`
- `qOwnerBody`
- `evBox3d`
- `opFillSurface`
- `opExtrude`
- `qNonCapEntity`
- `opOffsetFace`
- `opBoolean`
- `fCylinder`
- `fCone`
- `opDeleteBodies`

## Why no permanent sketches?

The purpose of the feature is partly to avoid feature-tree clutter.

The script works directly with enclosure topology and temporary solids/surfaces. After regeneration, the user should see the normal enclosure and lid parts rather than a large stack of generated sketches and intermediate features.

## Important known assumption

The current corner-boss code expects exactly four inner corner vertices, with two opening edges meeting at each corner.

That is appropriate for the current tested case but is the main limitation to generalize next.
