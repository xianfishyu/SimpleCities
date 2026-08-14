using System;

public readonly record struct RoadRenderToken
{
    public long SceneGeneration { get; }
    public long GraphFacadeID { get; }
    public long GraphFacadeGeneration { get; }
    public long ChangeSequence { get; }
    public long RoadStyleRevision { get; }
    public long RenderRequestID { get; }

    internal RoadRenderToken(
        long SceneGeneration,
        long GraphFacadeID,
        long GraphFacadeGeneration,
        long ChangeSequence,
        long RoadStyleRevision,
        long RenderRequestID)
    {
        if (SceneGeneration <= 0)
            throw new ArgumentOutOfRangeException(nameof(SceneGeneration));
        if (GraphFacadeID <= 0)
            throw new ArgumentOutOfRangeException(nameof(GraphFacadeID));
        if (GraphFacadeGeneration <= 0)
            throw new ArgumentOutOfRangeException(nameof(GraphFacadeGeneration));
        if (ChangeSequence < 0)
            throw new ArgumentOutOfRangeException(nameof(ChangeSequence));
        if (RoadStyleRevision <= 0)
            throw new ArgumentOutOfRangeException(nameof(RoadStyleRevision));
        if (RenderRequestID <= 0)
            throw new ArgumentOutOfRangeException(nameof(RenderRequestID));

        this.SceneGeneration = SceneGeneration;
        this.GraphFacadeID = GraphFacadeID;
        this.GraphFacadeGeneration = GraphFacadeGeneration;
        this.ChangeSequence = ChangeSequence;
        this.RoadStyleRevision = RoadStyleRevision;
        this.RenderRequestID = RenderRequestID;
    }
}

internal readonly record struct RoadRenderLoadReservation(
    long SceneGeneration,
    long GraphFacadeID,
    long TargetGraphFacadeGeneration,
    long RoadStyleRevision,
    long RenderRequestID);

internal sealed class RoadPresentationTokenTracker
{
    private long _sceneGeneration = 1;
    private long _graphFacadeID;
    private long _graphFacadeGeneration;
    private long _roadStyleRevision = 1;
    private long _renderRequestID;

    internal RoadRenderToken? DesiredToken { get; private set; }
    internal RoadRenderToken? PresentedToken { get; private set; }
    internal bool IsPresentationCurrent =>
        DesiredToken is RoadRenderToken desired && desired == PresentedToken;

    internal RoadRenderToken BindGraph(long graphFacadeID, long changeSequence)
    {
        if (graphFacadeID <= 0)
            throw new ArgumentOutOfRangeException(nameof(graphFacadeID));
        ValidateChangeSequence(changeSequence);

        _graphFacadeID = graphFacadeID;
        _graphFacadeGeneration = NextIdentity(
            _graphFacadeGeneration,
            "RoadGraph facade generation");
        return Request(changeSequence);
    }

    internal RoadRenderToken RequestGraphChange(long changeSequence, bool isFullReset)
    {
        EnsureGraphBound();
        ValidateChangeSequence(changeSequence);
        if (isFullReset)
        {
            _graphFacadeGeneration = NextIdentity(
                _graphFacadeGeneration,
                "RoadGraph facade generation");
        }
        return Request(changeSequence);
    }

    internal RoadRenderToken RequestRebuild(long changeSequence)
    {
        EnsureGraphBound();
        ValidateChangeSequence(changeSequence);
        return Request(changeSequence);
    }

    internal RoadRenderToken RequestStyleRefresh(long changeSequence)
    {
        EnsureGraphBound();
        ValidateChangeSequence(changeSequence);
        _roadStyleRevision = NextIdentity(_roadStyleRevision, "road style revision");
        return Request(changeSequence);
    }

    internal bool SetSceneGeneration(
        long sceneGeneration,
        long changeSequence,
        out RoadRenderToken token)
    {
        if (sceneGeneration <= 0)
            throw new ArgumentOutOfRangeException(nameof(sceneGeneration));
        ValidateChangeSequence(changeSequence);
        if (_sceneGeneration == sceneGeneration)
        {
            token = DesiredToken ?? default;
            return false;
        }

        _sceneGeneration = sceneGeneration;
        if (_graphFacadeID == 0)
        {
            token = default;
            return false;
        }

        token = Request(changeSequence);
        return true;
    }

    internal void CommitDesired(RoadRenderToken token)
    {
        if (DesiredToken is not RoadRenderToken desired || desired != token)
            throw new InvalidOperationException("Only the current desired road presentation can be committed.");
        PresentedToken = token;
    }

    internal RoadRenderLoadReservation ReserveLoad()
    {
        EnsureGraphBound();
        if (!IsPresentationCurrent)
        {
            throw new InvalidOperationException(
                "Road presentation must be current before admitting an aggregate load.");
        }

        _renderRequestID = NextIdentity(_renderRequestID, "road render request ID");
        return new RoadRenderLoadReservation(
            _sceneGeneration,
            _graphFacadeID,
            NextIdentity(_graphFacadeGeneration, "RoadGraph facade generation"),
            _roadStyleRevision,
            _renderRequestID);
    }

    internal bool IsReservationCurrent(RoadRenderLoadReservation reservation) =>
        reservation.SceneGeneration == _sceneGeneration &&
        reservation.GraphFacadeID == _graphFacadeID &&
        reservation.TargetGraphFacadeGeneration ==
            NextIdentity(_graphFacadeGeneration, "RoadGraph facade generation") &&
        reservation.RoadStyleRevision == _roadStyleRevision &&
        reservation.RenderRequestID == _renderRequestID;

    internal RoadRenderToken CreateReservedLoadToken(
        RoadRenderLoadReservation reservation,
        long changeSequence)
    {
        ValidateChangeSequence(changeSequence);
        if (!IsReservationCurrent(reservation))
            throw new InvalidOperationException("Road render load reservation is stale.");

        return new RoadRenderToken(
            reservation.SceneGeneration,
            reservation.GraphFacadeID,
            reservation.TargetGraphFacadeGeneration,
            changeSequence,
            reservation.RoadStyleRevision,
            reservation.RenderRequestID);
    }

    internal void CommitReservedLoad(
        RoadRenderLoadReservation reservation,
        RoadRenderToken token)
    {
        RoadRenderToken expected = CreateReservedLoadToken(
            reservation,
            token.ChangeSequence);
        if (expected != token)
            throw new InvalidOperationException("Road render load token does not match its reservation.");

        _graphFacadeGeneration = reservation.TargetGraphFacadeGeneration;
        DesiredToken = token;
        PresentedToken = token;
    }

    private RoadRenderToken Request(long changeSequence)
    {
        _renderRequestID = NextIdentity(_renderRequestID, "road render request ID");
        var token = new RoadRenderToken(
            _sceneGeneration,
            _graphFacadeID,
            _graphFacadeGeneration,
            changeSequence,
            _roadStyleRevision,
            _renderRequestID);
        DesiredToken = token;
        return token;
    }

    private void EnsureGraphBound()
    {
        if (_graphFacadeID <= 0 || _graphFacadeGeneration <= 0)
            throw new InvalidOperationException("Road presentation is not bound to a graph facade.");
    }

    private static void ValidateChangeSequence(long changeSequence)
    {
        if (changeSequence < 0)
            throw new ArgumentOutOfRangeException(nameof(changeSequence));
    }

    private static long NextIdentity(long value, string name)
    {
        if (value == long.MaxValue)
            throw new InvalidOperationException($"{name} space is exhausted.");
        return value + 1;
    }
}
