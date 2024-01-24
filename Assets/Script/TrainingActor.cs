using System.Threading.Tasks;

namespace Footsies
{
    public interface TrainingActor
    {
        // Setup any necessary resources before beginning training
        Task Setup();

        // Close all used resources after finishing training
        void Close();

        // Request a new input from the actor
        void RequestNextInput();

        // Communicate to the actor the new environment state
        void UpdateCurrentState(EnvironmentState state, bool battleOver);

        // Tell the training environment whether the actor is ready to pass to the next environment state
        bool Ready();

        // Get the actor's current input
        int GetInput();
    }
}