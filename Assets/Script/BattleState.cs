using System;

namespace Footsies
{
    // Class representing the state of the game at a particular time step
    [Serializable]
    public class BattleState
    {

        public FighterState p1State;
        public FighterState p2State;

        public float roundStartTime;
        public int frameCount;

        public BattleState(BattleCore core, float roundStartTime, int frameCount)
        {
            p1State = core.fighter1.SaveState();
            p2State = core.fighter2.SaveState();

            this.roundStartTime = roundStartTime;
            this.frameCount = frameCount;
        }
    }

}