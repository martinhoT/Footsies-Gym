namespace Footsies
{
    public class TrainingPlayerActor : TrainingActor
    {
        private bool player1;
        private int input;

        public TrainingPlayerActor(bool player1) {
            this.player1 = player1;
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
            input = 0;
            if (player1)
            {
                input |= InputManager.Instance.gameplay.p1Left.IsPressed() ? (int)InputDefine.Left : 0;
                input |= InputManager.Instance.gameplay.p1Right.IsPressed() ? (int)InputDefine.Right : 0;
                input |= InputManager.Instance.gameplay.p1Attack.IsPressed() ? (int)InputDefine.Attack : 0;
            }
            else
            {
                input |= InputManager.Instance.gameplay.p2Left.IsPressed() ? (int)InputDefine.Left : 0;
                input |= InputManager.Instance.gameplay.p2Right.IsPressed() ? (int)InputDefine.Right : 0;
                input |= InputManager.Instance.gameplay.p2Attack.IsPressed() ? (int)InputDefine.Attack : 0;
            }
        }

        public void UpdateCurrentState(EnvironmentState state, bool battleOver) {}
    }
}