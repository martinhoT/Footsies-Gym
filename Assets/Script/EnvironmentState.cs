using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Footsies
{
    // Environment state variables to be sent over the network to the agent
    [Serializable]
    public class EnvironmentState
    {
        public int p1Vital;
        public int p2Vital;
        public int p1Guard;
        public int p2Guard;
        public int p1Move;
        public int p1MoveFrame;
        public int p2Move;
        public int p2MoveFrame;
        public float p1Position;
        public float p2Position;
        public int globalFrame;
        public int p1MostRecentAction;
        public int p2MostRecentAction;
        public int p1Hitstun;
        public int p2Hitstun;

        public EnvironmentState(int p1Vital_, int p2Vital_, int p1Guard_, int p2Guard_, int p1Move_, int p1MoveFrame_, int p2Move_, int p2MoveFrame_, float p1Position_, float p2Position_, int globalFrame_, int p1MostRecentAction_, int p2MostRecentAction_, int p1Hitstun_, int p2Hitstun_)
        {
            p1Vital = p1Vital_;
            p2Vital = p2Vital_;
            p1Guard = p1Guard_;
            p2Guard = p2Guard_;
            p1Move = p1Move_;
            p1MoveFrame = p1MoveFrame_;
            p2Move = p2Move_;
            p2MoveFrame = p2MoveFrame_;
            p1Position = p1Position_;
            p2Position = p2Position_;
            globalFrame = globalFrame_;
            p1MostRecentAction = p1MostRecentAction_;
            p2MostRecentAction = p2MostRecentAction_;
            p1Hitstun = p1Hitstun_;
            p2Hitstun = p2Hitstun_;
        }
    }
}
