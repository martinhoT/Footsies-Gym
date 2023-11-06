namespace Footsies
{
    public class TrainingBattleAIActor : TrainingActor
    {
        private BattleAI battleAI;
        private int input;

        public TrainingBattleAIActor() {}

        public TrainingBattleAIActor(BattleAI ai) {
            battleAI = ai;
        }

        public void SetAI(BattleAI ai) {
            battleAI = ai;
        }

        public void Setup() {}

        public void Close() {}

        public int GetInput()
        {
            return input;
        }

        public bool Ready()
        {
            return true;
        }

        public void RequestNextInput()
        {
            input = battleAI.getNextAIInput();
        }

        // don't care didn't ask
        public void UpdateCurrentState(EnvironmentState state, bool battleOver) {}
    }
}