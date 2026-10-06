using System;
using UnityEngine;

namespace Game.Gameplay.Player.Legs
{
    public enum LegID
    {
        FrontLeft = 0,
        BackRight = 1,
        FrontRight = 2,
        BackLeft = 3
    }

    public enum LegActionState
    {
        Planted,
        Stepping,
        Airborne,
        Kicking
    }
}
