# Roadmap

## Next validation milestone

### Test on the original real-world enclosure
The next important test is the enclosure that motivated the project.

Questions to answer:
- Does top-rim selection remain reliable?
- Does the outer profile trace correctly?
- Does the locating plug fit the actual opening?
- Does the four-corner boss logic survive real enclosure geometry?
- Are screw locations usable without manual cleanup?
- Do normal downstream Part Studio features regenerate cleanly?

## Likely next improvements

### Rounded / segmented inside corners
Current boss logic expects exactly four sharp inner vertices.

A more general implementation should:
- identify logical corners in rounded-rectangle openings
- determine adjacent wall directions from tangents/straight segments
- place bosses consistently inside those corners
- retain the current simple behavior for sharp rectangles

### Optional outside overhang / flange
Allow the lid perimeter to extend beyond the enclosure outside wall by a configurable amount.

### Locating-plug lead-in
Optional chamfer on the lower outside edge of the locating plug to make assembly easier on printed parts.

### Optional fastener system
Allow users to create:
- lid only
- lid + locating plug
- lid + plug + bosses
- full lid + boss + screw system

Only add this if it remains simple enough to preserve the one-button workflow.

### Expanded screw presets
Possible additions:
- more metric sizes
- user-defined screw dimensions
- alternate head geometries
- custom countersink angle

### Alternative boss/fastener styles
Possible future modes:
- heat-set insert bosses
- tapped-hole bosses
- free-standing cylindrical bosses
- snap-fit / clip systems

These are intentionally lower priority than making the basic printed-plastic screw-boss workflow robust.

## Things intentionally out of scope

Unless there is a compelling reason, this project should not become:
- a full enclosure generator
- an electronics layout system
- a generic fastening framework
- a giant UI with dozens of configuration options

The feature exists to eliminate repetitive lid work.
