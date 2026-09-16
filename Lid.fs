FeatureScript 3070;
import(path : "onshape/std/geometry.fs", version : "3070.0");

export enum LidScrewSize
{
    annotation { "Name" : "M2" }
    M2,

    annotation { "Name" : "M2.5" }
    M2_5,

    annotation { "Name" : "M3" }
    M3,

    annotation { "Name" : "M4" }
    M4
}

function screwPointFromCorner(context is Context, cornerVertex is Query, innerEdges is Query, inset)
{
    const cornerPoint = evVertexPoint(context, {
        "vertex" : cornerVertex
    });

    const touchingInnerEdges = qIntersection([
        innerEdges,
        qAdjacent(
            cornerVertex,
            AdjacencyType.VERTEX,
            EntityType.EDGE
        )
    ]);

    const edgeList = evaluateQuery(context, touchingInnerEdges);

    if (size(edgeList) != 2)
    {
        throw regenError(
            "Each corner must have exactly two opening edges."
        );
    }

    const otherVertex0 = qSubtraction(
        qAdjacent(
            edgeList[0],
            AdjacencyType.VERTEX,
            EntityType.VERTEX
        ),
        cornerVertex
    );

    const otherVertex1 = qSubtraction(
        qAdjacent(
            edgeList[1],
            AdjacencyType.VERTEX,
            EntityType.VERTEX
        ),
        cornerVertex
    );

    const otherPoint0 = evVertexPoint(context, {
        "vertex" : otherVertex0
    });

    const otherPoint1 = evVertexPoint(context, {
        "vertex" : otherVertex1
    });

    const direction0 = normalize(otherPoint0 - cornerPoint);
    const direction1 = normalize(otherPoint1 - cornerPoint);

    return cornerPoint +
        direction0 * inset +
        direction1 * inset;
}

annotation {
    "Feature Type Name" : "Lid V0.4",
    "Feature Type Description" :
        "Creates a separate solid lid with a locating plug, four corner bosses, pilot holes, clearance holes, and countersinks."
}
export const lidV01 = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation {
            "Name" : "Top rim face",
            "Filter" : EntityType.FACE && GeometryType.PLANE && BodyType.SOLID,
            "MaxNumberOfPicks" : 1
        }
        definition.rimFace is Query;

        annotation { "Name" : "Lid thickness" }
        isLength(
            definition.lidThickness,
            { (millimeter) : [0.5, 2.5, 20] } as LengthBoundSpec
        );

        annotation { "Name" : "Fit clearance" }
        isLength(
            definition.fitClearance,
            { (millimeter) : [0, 0.25, 2] } as LengthBoundSpec
        );

        annotation { "Name" : "Locating depth" }
        isLength(
            definition.locatingDepth,
            { (millimeter) : [0.5, 2, 20] } as LengthBoundSpec
        );

        annotation { "Name" : "Boss diameter" }
        isLength(
            definition.bossDiameter,
            { (millimeter) : [2, 14, 50] } as LengthBoundSpec
        );

        annotation {
            "Group Name" : "Screw system",
            "Collapsed By Default" : false
        }
        {
            annotation {
                "Name" : "Screw size",
                "Default" : LidScrewSize.M3
            }
            definition.screwSize is LidScrewSize;

            annotation { "Name" : "Pilot diameter" }
            isLength(
                definition.pilotDiameter,
                { (millimeter) : [0.5, 2.2, 10] } as LengthBoundSpec
            );

            annotation { "Name" : "Pilot depth" }
            isLength(
                definition.pilotDepth,
                { (millimeter) : [1, 10, 50] } as LengthBoundSpec
            );
        }
    }
    {
        const rimPlane = evPlane(context, {
            "face" : definition.rimFace
        });

        const rimEdges = qAdjacent(
            definition.rimFace,
            AdjacencyType.EDGE,
            EntityType.EDGE
        );

        const loops = connectedComponents(
            context,
            rimEdges,
            AdjacencyType.VERTEX
        );

        var outerEdges = qUnion(loops[0]);
        var longestPerimeter = evLength(context, {
            "entities" : outerEdges
        });

        for (var loop in loops)
        {
            const loopQuery = qUnion(loop);
            const perimeter = evLength(context, {
                "entities" : loopQuery
            });

            if (perimeter > longestPerimeter)
            {
                longestPerimeter = perimeter;
                outerEdges = loopQuery;
            }
        }

        const innerEdges = qSubtraction(
            rimEdges,
            outerEdges
        );

        const enclosureBody = qOwnerBody(definition.rimFace);

        const rimCSys = coordSystem(rimPlane);
        const enclosureBounds = evBox3d(context, {
            "topology" : enclosureBody,
            "cSys" : rimCSys,
            "tight" : true
        });
        const enclosureDepth = -enclosureBounds.minCorner[2];

        if (enclosureDepth <= definition.locatingDepth)
        {
            throw regenError(
                "Locating depth must be less than the enclosure depth."
            );
        }

        // ----- MAIN LID PLATE -----

        opFillSurface(context, id + "lidProfile", {
            "edgesG0" : outerEdges
        });

        const profileFace =
            qCreatedBy(id + "lidProfile", EntityType.FACE);

        opExtrude(context, id + "lidSolid", {
            "entities" : profileFace,
            "direction" : rimPlane.normal,
            "endBound" : BoundingType.BLIND,
            "endDepth" : definition.lidThickness
        });

        // ----- LOCATING PLUG -----

        opFillSurface(context, id + "plugProfile", {
            "edgesG0" : innerEdges
        });

        const plugProfileFace =
            qCreatedBy(id + "plugProfile", EntityType.FACE);

        opExtrude(context, id + "locatingPlug", {
            "entities" : plugProfileFace,
            "direction" : -rimPlane.normal,
            "endBound" : BoundingType.BLIND,
            "endDepth" : definition.locatingDepth
        });

        opOffsetFace(context, id + "plugClearance", {
            "moveFaces" :
                qNonCapEntity(id + "locatingPlug", EntityType.FACE),
            "offsetDistance" :
                -definition.fitClearance
        });

        opBoolean(context, id + "joinPlug", {
            "tools" : qUnion([
                qCreatedBy(id + "lidSolid", EntityType.BODY),
                qCreatedBy(id + "locatingPlug", EntityType.BODY)
            ]),
            "operationType" : BooleanOperationType.UNION
        });

        // ----- CORNERS AND SCREW LOCATIONS -----

        const innerVertices = qAdjacent(
            innerEdges,
            AdjacencyType.VERTEX,
            EntityType.VERTEX
        );
        const cornerVertices = evaluateQuery(context, innerVertices);

        if (size(cornerVertices) != 4)
        {
            throw regenError(
                "Corner bosses currently require an opening with exactly four corner vertices."
            );
        }

        const bossRadius = definition.bossDiameter / 2;

        // Screw center = D/6 from each adjacent inside wall.
        const screwInset = definition.bossDiameter / 6;

        const screwPoint0 = screwPointFromCorner(
            context,
            cornerVertices[0],
            innerEdges,
            screwInset
        );

        const screwPoint1 = screwPointFromCorner(
            context,
            cornerVertices[1],
            innerEdges,
            screwInset
        );

        const screwPoint2 = screwPointFromCorner(
            context,
            cornerVertices[2],
            innerEdges,
            screwInset
        );

        const screwPoint3 = screwPointFromCorner(
            context,
            cornerVertices[3],
            innerEdges,
            screwInset
        );

        // ----- SCREW SIZE GEOMETRY -----

        var clearanceDiameter = 3.4 * millimeter;
        var countersinkDiameter = 6.0 * millimeter;

        if (definition.screwSize == LidScrewSize.M2)
        {
            clearanceDiameter = 2.4 * millimeter;
            countersinkDiameter = 4.0 * millimeter;
        }
        else if (definition.screwSize == LidScrewSize.M2_5)
        {
            clearanceDiameter = 2.9 * millimeter;
            countersinkDiameter = 5.0 * millimeter;
        }
        else if (definition.screwSize == LidScrewSize.M4)
        {
            clearanceDiameter = 4.5 * millimeter;
            countersinkDiameter = 8.0 * millimeter;
        }

        const clearanceRadius = clearanceDiameter / 2;
        const countersinkRadius = countersinkDiameter / 2;

        const countersinkDepth =
            countersinkRadius - clearanceRadius;

        if (countersinkDepth >= definition.lidThickness)
        {
            throw regenError(
                "The selected screw countersink is deeper than the lid thickness."
            );
        }

        const cutterMargin = 0.1 * millimeter;

        // ----- LID CLEARANCE HOLES -----

        const lidTopOffset =
            definition.lidThickness + cutterMargin;

        const lidBottomOffset =
            definition.locatingDepth + cutterMargin;

        fCylinder(context, id + "clearance0", {
            "topCenter" :
                screwPoint0 + rimPlane.normal * lidTopOffset,
            "bottomCenter" :
                screwPoint0 - rimPlane.normal * lidBottomOffset,
            "radius" : clearanceRadius
        });

        fCylinder(context, id + "clearance1", {
            "topCenter" :
                screwPoint1 + rimPlane.normal * lidTopOffset,
            "bottomCenter" :
                screwPoint1 - rimPlane.normal * lidBottomOffset,
            "radius" : clearanceRadius
        });

        fCylinder(context, id + "clearance2", {
            "topCenter" :
                screwPoint2 + rimPlane.normal * lidTopOffset,
            "bottomCenter" :
                screwPoint2 - rimPlane.normal * lidBottomOffset,
            "radius" : clearanceRadius
        });

        fCylinder(context, id + "clearance3", {
            "topCenter" :
                screwPoint3 + rimPlane.normal * lidTopOffset,
            "bottomCenter" :
                screwPoint3 - rimPlane.normal * lidBottomOffset,
            "radius" : clearanceRadius
        });

        // ----- COUNTERSINKS -----

        const countersinkTopRadius =
            countersinkRadius + cutterMargin;

        const countersinkTopOffset =
            definition.lidThickness + cutterMargin;

        const countersinkBottomOffset =
            definition.lidThickness - countersinkDepth;

        fCone(context, id + "countersink0", {
            "topCenter" :
                screwPoint0 + rimPlane.normal * countersinkTopOffset,
            "bottomCenter" :
                screwPoint0 + rimPlane.normal * countersinkBottomOffset,
            "topRadius" : countersinkTopRadius,
            "bottomRadius" : clearanceRadius
        });

        fCone(context, id + "countersink1", {
            "topCenter" :
                screwPoint1 + rimPlane.normal * countersinkTopOffset,
            "bottomCenter" :
                screwPoint1 + rimPlane.normal * countersinkBottomOffset,
            "topRadius" : countersinkTopRadius,
            "bottomRadius" : clearanceRadius
        });

        fCone(context, id + "countersink2", {
            "topCenter" :
                screwPoint2 + rimPlane.normal * countersinkTopOffset,
            "bottomCenter" :
                screwPoint2 + rimPlane.normal * countersinkBottomOffset,
            "topRadius" : countersinkTopRadius,
            "bottomRadius" : clearanceRadius
        });

        fCone(context, id + "countersink3", {
            "topCenter" :
                screwPoint3 + rimPlane.normal * countersinkTopOffset,
            "bottomCenter" :
                screwPoint3 + rimPlane.normal * countersinkBottomOffset,
            "topRadius" : countersinkTopRadius,
            "bottomRadius" : clearanceRadius
        });

        opBoolean(context, id + "cutLidScrewHoles", {
            "tools" : qUnion([
                qCreatedBy(id + "clearance0", EntityType.BODY),
                qCreatedBy(id + "clearance1", EntityType.BODY),
                qCreatedBy(id + "clearance2", EntityType.BODY),
                qCreatedBy(id + "clearance3", EntityType.BODY),
                qCreatedBy(id + "countersink0", EntityType.BODY),
                qCreatedBy(id + "countersink1", EntityType.BODY),
                qCreatedBy(id + "countersink2", EntityType.BODY),
                qCreatedBy(id + "countersink3", EntityType.BODY)
            ]),
            "targets" :
                qCreatedBy(id + "lidSolid", EntityType.BODY),
            "operationType" :
                BooleanOperationType.SUBTRACTION
        });

        // ----- CORNER BOSSES -----

        const bossTopOffset = definition.locatingDepth;

        const corner0 = evVertexPoint(context, {
            "vertex" : cornerVertices[0]
        });

        fCylinder(context, id + "bossCylinder0", {
            "topCenter" :
                corner0 - rimPlane.normal * bossTopOffset,
            "bottomCenter" :
                corner0 - rimPlane.normal * enclosureDepth,
            "radius" : bossRadius
        });

        opExtrude(context, id + "bossClip0", {
            "entities" : plugProfileFace,
            "direction" : -rimPlane.normal,
            "endBound" : BoundingType.BLIND,
            "endDepth" : enclosureDepth
        });

        opBoolean(context, id + "bossQuarter0", {
            "tools" : qUnion([
                qCreatedBy(id + "bossCylinder0", EntityType.BODY),
                qCreatedBy(id + "bossClip0", EntityType.BODY)
            ]),
            "operationType" :
                BooleanOperationType.INTERSECTION
        });

        const corner1 = evVertexPoint(context, {
            "vertex" : cornerVertices[1]
        });

        fCylinder(context, id + "bossCylinder1", {
            "topCenter" :
                corner1 - rimPlane.normal * bossTopOffset,
            "bottomCenter" :
                corner1 - rimPlane.normal * enclosureDepth,
            "radius" : bossRadius
        });

        opExtrude(context, id + "bossClip1", {
            "entities" : plugProfileFace,
            "direction" : -rimPlane.normal,
            "endBound" : BoundingType.BLIND,
            "endDepth" : enclosureDepth
        });

        opBoolean(context, id + "bossQuarter1", {
            "tools" : qUnion([
                qCreatedBy(id + "bossCylinder1", EntityType.BODY),
                qCreatedBy(id + "bossClip1", EntityType.BODY)
            ]),
            "operationType" :
                BooleanOperationType.INTERSECTION
        });

        const corner2 = evVertexPoint(context, {
            "vertex" : cornerVertices[2]
        });

        fCylinder(context, id + "bossCylinder2", {
            "topCenter" :
                corner2 - rimPlane.normal * bossTopOffset,
            "bottomCenter" :
                corner2 - rimPlane.normal * enclosureDepth,
            "radius" : bossRadius
        });

        opExtrude(context, id + "bossClip2", {
            "entities" : plugProfileFace,
            "direction" : -rimPlane.normal,
            "endBound" : BoundingType.BLIND,
            "endDepth" : enclosureDepth
        });

        opBoolean(context, id + "bossQuarter2", {
            "tools" : qUnion([
                qCreatedBy(id + "bossCylinder2", EntityType.BODY),
                qCreatedBy(id + "bossClip2", EntityType.BODY)
            ]),
            "operationType" :
                BooleanOperationType.INTERSECTION
        });

        const corner3 = evVertexPoint(context, {
            "vertex" : cornerVertices[3]
        });

        fCylinder(context, id + "bossCylinder3", {
            "topCenter" :
                corner3 - rimPlane.normal * bossTopOffset,
            "bottomCenter" :
                corner3 - rimPlane.normal * enclosureDepth,
            "radius" : bossRadius
        });

        opExtrude(context, id + "bossClip3", {
            "entities" : plugProfileFace,
            "direction" : -rimPlane.normal,
            "endBound" : BoundingType.BLIND,
            "endDepth" : enclosureDepth
        });

        opBoolean(context, id + "bossQuarter3", {
            "tools" : qUnion([
                qCreatedBy(id + "bossCylinder3", EntityType.BODY),
                qCreatedBy(id + "bossClip3", EntityType.BODY)
            ]),
            "operationType" :
                BooleanOperationType.INTERSECTION
        });

        opBoolean(context, id + "joinBosses", {
            "tools" : qUnion([
                enclosureBody,
                qCreatedBy(id + "bossQuarter0", EntityType.BODY),
                qCreatedBy(id + "bossQuarter1", EntityType.BODY),
                qCreatedBy(id + "bossQuarter2", EntityType.BODY),
                qCreatedBy(id + "bossQuarter3", EntityType.BODY)
            ]),
            "operationType" :
                BooleanOperationType.UNION
        });

        // ----- PILOT HOLES -----

        const availableBossHeight =
            enclosureDepth - definition.locatingDepth;

        if (definition.pilotDepth >= availableBossHeight)
        {
            throw regenError(
                "Pilot depth must be less than the available boss height."
            );
        }

        if (definition.pilotDiameter >= definition.bossDiameter)
        {
            throw regenError(
                "Pilot diameter must be smaller than the boss diameter."
            );
        }

        const pilotRadius =
            definition.pilotDiameter / 2;

        const pilotStartOffset =
            definition.locatingDepth - cutterMargin;

        const pilotEndOffset =
            definition.locatingDepth + definition.pilotDepth;

        fCylinder(context, id + "pilot0", {
            "topCenter" :
                screwPoint0 - rimPlane.normal * pilotStartOffset,
            "bottomCenter" :
                screwPoint0 - rimPlane.normal * pilotEndOffset,
            "radius" : pilotRadius
        });

        fCylinder(context, id + "pilot1", {
            "topCenter" :
                screwPoint1 - rimPlane.normal * pilotStartOffset,
            "bottomCenter" :
                screwPoint1 - rimPlane.normal * pilotEndOffset,
            "radius" : pilotRadius
        });

        fCylinder(context, id + "pilot2", {
            "topCenter" :
                screwPoint2 - rimPlane.normal * pilotStartOffset,
            "bottomCenter" :
                screwPoint2 - rimPlane.normal * pilotEndOffset,
            "radius" : pilotRadius
        });

        fCylinder(context, id + "pilot3", {
            "topCenter" :
                screwPoint3 - rimPlane.normal * pilotStartOffset,
            "bottomCenter" :
                screwPoint3 - rimPlane.normal * pilotEndOffset,
            "radius" : pilotRadius
        });

        opBoolean(context, id + "cutPilotHoles", {
            "tools" : qUnion([
                qCreatedBy(id + "pilot0", EntityType.BODY),
                qCreatedBy(id + "pilot1", EntityType.BODY),
                qCreatedBy(id + "pilot2", EntityType.BODY),
                qCreatedBy(id + "pilot3", EntityType.BODY)
            ]),
            "targets" : enclosureBody,
            "operationType" :
                BooleanOperationType.SUBTRACTION
        });

        // ----- CLEANUP -----

        opDeleteBodies(context, id + "deleteLidProfile", {
            "entities" :
                qCreatedBy(id + "lidProfile", EntityType.BODY)
        });

        opDeleteBodies(context, id + "deletePlugProfile", {
            "entities" :
                qCreatedBy(id + "plugProfile", EntityType.BODY)
        });
    });
