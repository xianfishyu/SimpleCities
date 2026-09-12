namespace SimpleCities.RoadCore;

public enum RoadEditStatus { Ready, NoChange, Rejected }

public sealed record RoadEditResult(RoadEditStatus Status, RoadPlan? Plan, string Reason);
