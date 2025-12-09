using System.Collections.Generic;
using Godot;




public class Station(string name, StationType type, float stationLength)
{
    public string Name = name;
    public StationType Type = type;
    public float StationLength = stationLength;
}


public enum StationType
{
    带越行线的两台四线,
    不带越行线的两台四线,
    四台七线,
    四台七线_天津,  // 天津站特殊布局：正线中间夹着站台
}