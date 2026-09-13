using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleCities.RoadCore;

/// <summary>Debug-only malformed presentation fixtures exercise the real hidden ArrayMesh preflight.</summary>
public partial class V4DisplayPreflightProbe : RefCounted
{
    public Godot.Collections.Dictionary Run()
    {
        var results = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(100, 0), RoadProfileId.Street);
        RoadSnapshot snapshot = network.Snapshot;
        RoadSurfaceData valid = RoadPresentation.Prepare(snapshot.Nodes, snapshot.Edges) ?? throw new InvalidOperationException("Missing fixture surface.");
        RoadSurfacePiece ribbon = valid.Pieces[0];

        Check("empty_target_accepts_no_mesh", () =>
        {
            using var display = V4RoadDisplay.Prepare(new RoadNetwork().Snapshot, null);
            return display.Mesh is null;
        });
        Check("valid_ribbon_mesh_and_owner_agree", () =>
        {
            using var display = V4RoadDisplay.Prepare(snapshot, valid);
            var hits = display.QueryHit(new Vector2(50, 0));
            return display.Mesh?.SurfaceGetArrayLen(0) == 4 && display.Mesh.SurfaceGetArrayIndexLen(0) == 6 &&
                hits.Results.Count == 1 && hits.Results[0].Location.Source == snapshot.Token &&
                hits.Results[0].Location.Edge == ribbon.Edge.Id && hits.Results[0].Location.Parameter == 0.5;
        });
        Reject("missing_surface_rejected", null);
        Reject("missing_ribbon_rejected", Surface(snapshot, []));
        Reject("piece_budget_rejected_before_resource_creation", Surface(snapshot, [ribbon, ribbon]));
        Reject("foreign_owner_rejected", Surface(snapshot, [Piece(new RoadEdge(ribbon.Edge.Id, ribbon.Edge.Start,
            ribbon.Edge.End, RoadProfileId.Highway, ribbon.Edge.Points), ribbon.Start, ribbon.End, 0, 1, ribbon.Corners.ToArray())]));
        Reject("nan_parameter_rejected", Surface(snapshot, [Piece(ribbon.Edge, ribbon.Start, ribbon.End, double.NaN, 1, ribbon.Corners.ToArray())]));
        Reject("out_of_range_parameter_rejected", Surface(snapshot, [Piece(ribbon.Edge, ribbon.Start, ribbon.End, 0, 2, ribbon.Corners.ToArray())]));
        Reject("location_geometry_mismatch_rejected", Surface(snapshot, [Piece(ribbon.Edge, new(25, 0), ribbon.End, 0, 1, ribbon.Corners.ToArray())]));
        Reject("ribbon_cannot_claim_junction", Surface(snapshot, [Piece(ribbon.Edge, ribbon.Start, ribbon.End, 0, 1,
            ribbon.Corners.ToArray(), ribbon.Edge.Start)]));
        Reject("nonfinite_vertex_rejected", Surface(snapshot, [Piece(ribbon.Edge, ribbon.Start, ribbon.End, 0, 1,
            [new(double.NaN, 6), new(106, 6), new(106, -6), new(-6, -6)])]));
        Reject("vertex_resource_envelope_rejected", Surface(snapshot, [Piece(ribbon.Edge, ribbon.Start, ribbon.End, 0, 1,
            [new(-6, 6), new(100000, 6), new(100000, -6), new(-6, -6)])]));
        Reject("nonconvex_mesh_rejected", Surface(snapshot, [Piece(ribbon.Edge, ribbon.Start, ribbon.End, 0, 1,
            [new(-6, 6), new(106, -6), new(106, 6), new(-6, -6)])]));
        Reject("degenerate_mesh_rejected", Surface(snapshot, [Piece(ribbon.Edge, ribbon.Start, ribbon.End, 0, 1,
            [new(0, 0), new(25, 0), new(75, 0), new(100, 0)])]));

        foreach (int cell in new[] { 25, 50, 100, 200 })
        {
            Accept($"valid_mixed_junction_cell_{cell}", new MapDefinition(cell),
                [(new(-cell, 0), new(cell, 0), RoadProfileId.Highway), (new(-cell, -cell), new(0, 0), RoadProfileId.Dirt)]);
            Accept($"valid_diagonal_cell_{cell}", new MapDefinition(cell),
                [(new(0, 0), new(cell, cell), RoadProfileId.Dirt)]);
        }
        Accept("valid_boundary_junction", new MapDefinition(),
            [(new(4000, -100), new(4000, 100), RoadProfileId.Highway), (new(3900, 0), new(4000, 0), RoadProfileId.Dirt)]);
        Accept("valid_polyline_and_profile_join", new MapDefinition(),
            [(new(0, 0), new(300, 0), RoadProfileId.Street), (new(300, 0), new(300, 400), RoadProfileId.Street),
             (new(300, 400), new(400, 500), RoadProfileId.Highway)]);
        Accept("valid_loop_with_branch", new MapDefinition(),
            [(new(0, 0), new(100, 0), RoadProfileId.Street), (new(100, 0), new(100, 100), RoadProfileId.Street),
             (new(100, 100), new(0, 100), RoadProfileId.Street), (new(0, 100), new(0, 0), RoadProfileId.Street),
             (new(0, 0), new(-100, 0), RoadProfileId.Highway)]);

        return new Godot.Collections.Dictionary
        {
            ["passed"] = results.All(result => result["passed"].AsBool()),
            ["checks"] = results.Count,
            ["results"] = results,
        };

        void Reject(string name, RoadSurfaceData? malformed) => Check(name, () =>
        {
            try { using var display = V4RoadDisplay.Prepare(snapshot, malformed); return false; }
            catch (InvalidOperationException) { return true; }
        });

        void Accept(string name, MapDefinition map, (RoadPoint Start, RoadPoint End, RoadProfileId Profile)[] strokes) => Check(name, () =>
        {
            var target = new RoadNetwork(map);
            foreach (var stroke in strokes) Build(target, stroke.Start, stroke.End, stroke.Profile);
            using var display = V4RoadDisplay.Prepare(target.Snapshot, RoadPresentation.Prepare(target.Snapshot.Nodes, target.Snapshot.Edges));
            return display.Mesh?.GetSurfaceCount() == 1;
        });

        void Check(string name, Func<bool> assertion)
        {
            string error = "";
            bool passed;
            try { passed = assertion(); }
            catch (Exception exception) { passed = false; error = exception.Message; }
            results.Add(new Godot.Collections.Dictionary { ["name"] = name, ["passed"] = passed, ["error"] = error });
        }
    }

    private static RoadSurfacePiece Piece(RoadEdge edge, RoadPoint start, RoadPoint end, double from, double to,
        RoadPoint[] corners, NodeId? node = null) =>
        Activator.CreateInstance(typeof(RoadSurfacePiece), BindingFlags.Instance | BindingFlags.NonPublic, null,
            [edge, start, end, from, to, corners, node], null) as RoadSurfacePiece ?? throw new InvalidOperationException("Cannot create malformed surface piece fixture.");

    private static RoadSurfaceData Surface(RoadSnapshot snapshot, IEnumerable<RoadSurfacePiece> pieces) =>
        Activator.CreateInstance(typeof(RoadSurfaceData), BindingFlags.Instance | BindingFlags.NonPublic, null,
            [snapshot.Nodes, snapshot.Edges, pieces], null) as RoadSurfaceData ?? throw new InvalidOperationException("Cannot create malformed surface fixture.");

    private static void Build(RoadNetwork network, RoadPoint start, RoadPoint end, RoadProfileId profile)
    {
        RoadBuildResult result = network.PlanBuild(new(network.Snapshot.Token, start, end, profile));
        if (result.Status != RoadBuildStatus.Ready || result.Plan is null || !network.TryCommit(result.Plan))
            throw new InvalidOperationException($"Preflight fixture could not build: {result.Reason}");
    }
}
