using UnityEngine;
using UnityEngine.SceneManagement;
using System;

namespace Footsies
{
    public class GameManager : Singleton<GameManager>
    {
        public enum SceneIndex
        {
            Title = 1,
            Battle = 2,
        }

        public AudioClip menuSelectAudioClip;

        public SceneIndex currentScene { get; private set; }
        public bool isVsCPU { get; private set; }
        public TrainingManager trainingManager { get; private set; }
        public TrainingRemoteControl trainingRemoteControl { get; private set; }

        // These two variables will contain the respective TrainingBattleAIActor, if they were instanced
        // here in the GameManager. We provide these instances so that BattleCore can properly initialize
        // the necessary BattleAIs. Doing it this way allows these actors to be wrapped by other
        // TrainingActor classes (such as RemoteSpectator) while having access to the unwrapped bot for
        // proper initialization
        public TrainingBattleAIActor botP1 { get; private set; }
        public TrainingBattleAIActor botP2 { get; private set; }

        private bool shouldMute = false;

        private void Awake()
        {
            DontDestroyOnLoad(this.gameObject);

            Application.targetFrameRate = 60;

            string[] args = Environment.GetCommandLineArgs();
            string passedArguments = "";
            // Default values
            bool argIsTrainingEnv = false;
            int argTrainingSyncMode = 0; // 0: async | 1: sync non-blocking | 2: sync blocking
            string argRemoteControlAddress = "localhost";
            int argRemoteControlPort = 11002;
            bool argP1Bot = false;
            bool argP1Player = false;
            bool argP1Spectator = false;
            string argP1TrainingAddress = "localhost";
            int argP1TrainingPort = 11000;
            bool argP1NoState = false;
            bool argP2Bot = false;
            bool argP2Player = false;
            bool argP2Spectator = false;
            string argP2TrainingAddress = "localhost";
            int argP2TrainingPort = 11001;
            bool argP2NoState = false;
            bool argFastForward = false;
            
            int argIndex = 0;
            foreach (var arg in args)
            {
                passedArguments += arg + " ";

                switch (arg)
                {
                    case "--training":
                        argIsTrainingEnv = true;
                        break;

                    case "--fast-forward":
                        argFastForward = true;
                        break;
                    
                    case "--synced-non-blocking":
                        argTrainingSyncMode = 1;
                        break;

                    case "--synced-blocking":
                        argTrainingSyncMode = 2;
                        break;

                    case "--p1-bot":
                        argP1Bot = true;
                        break;
                        
                    case "--p2-bot":
                        argP2Bot = true;
                        break;
                    
                    case "--p1-player":
                        argP1Player = true;
                        break;

                    case "--p2-player":
                        argP2Player = true;
                        break;

                    case "--p1-spectator":
                        argP1Spectator = true;
                        break;

                    case "--p2-spectator":
                        argP2Spectator = true;
                        break;

                    case "--mute":
                        shouldMute = true;
                        break;
                    
                    case "--remote-control-address":
                        argRemoteControlAddress = args[argIndex + 1];
                        break;
                    
                    case "--remote-control-port":
                        argRemoteControlPort = Convert.ToUInt16(args[argIndex + 1]);
                        break;

                    case "--p1-address":
                        argP1TrainingAddress = args[argIndex + 1];
                        break;

                    case "--p1-port":
                        argP1TrainingPort = Convert.ToUInt16(args[argIndex + 1]);
                        break;
                    
                    case "--p1-no-state":
                        argP1NoState = true;
                        break;

                    case "--p2-address":
                        argP2TrainingAddress = args[argIndex + 1];
                        break;

                    case "--p2-port":
                        argP2TrainingPort = Convert.ToUInt16(args[argIndex + 1]);
                        break;
                    
                    case "--p2-no-state":
                        argP2NoState = true;
                        break;
                }

                argIndex++;
            }
            Debug.Log("Passed arguments: " + passedArguments + "\n"
                + "   Run as training environment? " + argIsTrainingEnv + "\n"
                + "   Fast forward training? " + argFastForward + "\n"
                + "   Sync mode? " + (
                    (argTrainingSyncMode == 2)
                        ? "synced blocking"
                        : (argTrainingSyncMode == 1)
                            ? "synced non-blocking"
                            : "async"
                ) + "\n"
                + "   Mute? " + shouldMute + "\n"
                + "   Remote Control address: " + argRemoteControlAddress + "\n"
                + "   Remote Control port: " + argRemoteControlPort + "\n"
                + "   P1 Bot? " + argP1Bot + "\n"
                + "   P1 Player? " + argP1Player + "\n"
                + "   P1 Spectator? " + argP1Spectator + "\n"
                + "   P1 Training address: " + argP1TrainingAddress + "\n"
                + "   P1 Training port: " + argP1TrainingPort + "\n"
                + "   Send environment state to P1? " + !argP1NoState + "\n"
                + "   P2 Bot? " + argP2Bot + "\n"
                + "   P2 Player? " + argP2Player + "\n"
                + "   P1 Spectator? " + argP2Spectator + "\n"
                + "   P2 Training address: " + argP2TrainingAddress + "\n"
                + "   P2 Training port: " + argP2TrainingPort + "\n"
                + "   Send environment state to P2? " + !argP2NoState + "\n"
            );

            if (argIsTrainingEnv && argFastForward)
            {
                // Make the game run 100x faster for more efficient training
                Time.timeScale = 100;
                Application.targetFrameRate = 5000;
            }

            if (argP1Bot)
                botP1 = new TrainingBattleAIActor();
            if (argP2Bot)
                botP2 = new TrainingBattleAIActor();

            TrainingActor actorP1 = argP1Bot ? botP1
                         : (argP1Player ? new TrainingPlayerActor(true)
                                        : new TrainingRemoteActor(argP1TrainingAddress, argP1TrainingPort, argTrainingSyncMode == 2, argP1NoState));

            TrainingActor actorP2 = argP2Bot ? botP2
                         : (argP2Player ? new TrainingPlayerActor(false)
                                        : new TrainingRemoteActor(argP2TrainingAddress, argP2TrainingPort, argTrainingSyncMode == 2, argP2NoState));

            // WARNING: because each player only has an address-port pair, it doesn't make sense to create a spectator of a RemoteActor
            if (argP1Spectator)
                actorP1 = new TrainingActorRemoteSpectator(argP1TrainingAddress, argP1TrainingPort, argTrainingSyncMode == 2, actorP1);
            if (argP2Spectator)
                actorP2 = new TrainingActorRemoteSpectator(argP2TrainingAddress, argP2TrainingPort, argTrainingSyncMode == 2, actorP2);

            trainingManager = new TrainingManager(argIsTrainingEnv, argTrainingSyncMode > 0, actorP1, actorP2);

            trainingRemoteControl = new TrainingRemoteControl(argRemoteControlAddress, argRemoteControlPort, argTrainingSyncMode == 2);
        }

        private void Start()
        {
            if (shouldMute && SoundManager.Instance.isAllOn)
            {
                SoundManager.Instance.toggleAll();
            }

            if (trainingManager.isTraining)
            {
                LoadVsCPUScene();
            }
            else
            {
                LoadTitleScene();
            }
        }

        private void Update()
        {
            if(currentScene == SceneIndex.Battle)
            {
                if(InputManager.Instance.gameplay.cancel.WasPressedThisFrame())
                {
                    LoadTitleScene();
                }
            }
        }

        public void LoadTitleScene()
        {
            SceneManager.LoadScene((int)SceneIndex.Title);
            currentScene = SceneIndex.Title;
        }

        public void LoadVsPlayerScene()
        {
            isVsCPU = false;
            LoadBattleScene();
        }

        public void LoadVsCPUScene()
        {
            isVsCPU = true;
            LoadBattleScene();
        }

        private void LoadBattleScene()
        {
            SceneManager.LoadScene((int)SceneIndex.Battle);
            currentScene = SceneIndex.Battle;

            if(menuSelectAudioClip != null)
            {
                SoundManager.Instance.playSE(menuSelectAudioClip);
            }
        }
    }

}