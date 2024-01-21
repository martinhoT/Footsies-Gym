using UnityEngine;
using System;
using System.Net.Sockets;
using System.Text;

namespace Footsies
{
    public class TrainingManager
    {
        // If isTraining is false, the TrainingManager methods will not do anything when called
        public bool isTraining { get; private set; } = false;
        // Whether to wait for agent inputs when training or just keep advancing
        public bool isTrainingSynced { get; private set; } = false;

        public TrainingActor actorP1;
        public TrainingActor actorP2;

        private bool isAlreadySetup = false;

        public TrainingManager(bool enabled, bool synced, TrainingActor actorP1, TrainingActor actorP2) {
            isTraining = enabled;
            isTrainingSynced = synced;
            this.actorP1 = actorP1;
            this.actorP2 = actorP2;
        }

        public bool Setup()
        {
            if (!isTraining) { return false; }

            if (!isAlreadySetup)
            {
                actorP1.Setup();
                actorP2.Setup();

                isAlreadySetup = true;
                return true;
            }
            
            return false;
        }

        public bool Close()
        {
            if (!isTraining) { return false; }

            if (isAlreadySetup)
            {
                actorP1.Close();
                actorP2.Close();

                return true;
            }

            return false;
        }

        public void Step(EnvironmentState state, bool battleOver)
        {
            if (!isTraining) { return; }

            actorP1.UpdateCurrentState(state, battleOver);

            // Request another action from the training agent, as long as the environment hasn't terminated
            if (!battleOver)
            {
                actorP1.RequestNextInput();
            }

            actorP2.UpdateCurrentState(state, battleOver);

            if (!battleOver)
            {
                actorP2.RequestNextInput();
            }
        }

        public int p1Input()
        {
            return actorP1.GetInput();
        }

        public int p2Input()
        {
            return actorP2.GetInput();
        }

        public bool Ready()
        {
            return !isTraining || !isTrainingSynced || (actorP1.Ready() && actorP2.Ready());
        }
    }
}