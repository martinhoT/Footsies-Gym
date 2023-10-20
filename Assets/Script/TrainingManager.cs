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

        private TrainingActor actorP1;
        private TrainingActor actorP2;

        private bool isAlreadySetup = false;

        public TrainingManager(bool enabled, bool synced, TrainingActor actorP1, TrainingActor actorP2) {
            isTraining = enabled;
            isTrainingSynced = synced;
            this.actorP1 = actorP1;
            this.actorP2 = actorP2;
        }

        public bool IsP1ActorSet() { return actorP1 != null; }
        public bool IsP2ActorSet() { return actorP2 != null; }

        public void SetP1Actor(TrainingActor actor) { actorP1 = actor; }
        public void SetP2Actor(TrainingActor actor) { actorP2 = actor; }

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

            // Don't send the environment state until the agents are ready to receive it (relevant when training is async)
            // Otherwise, for the TrainingRemoteActor agents, the socket will be filled with state messages, with only one of them being up-to-date
            if (actorP1.Ready())
            {
                actorP1.UpdateCurrentState(state);

                // Request another action from the training agent, as long as the environment hasn't terminated and the previous input request has been dealt with
                if (!battleOver)
                {
                    actorP1.RequestNextInput();
                }
            }

            if (actorP2.Ready())
            {
                actorP2.UpdateCurrentState(state);

                if (!battleOver)
                {
                    actorP2.RequestNextInput();
                }
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
            return !isTraining || (actorP1.Ready() && actorP2.Ready());
        }
    }
}