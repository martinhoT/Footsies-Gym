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

        private bool shouldMute = false;

        private void Awake()
        {
            DontDestroyOnLoad(this.gameObject);

            Application.targetFrameRate = 60;

            string[] args = Environment.GetCommandLineArgs();
            string passedArguments = "";
            // Default values
            bool argIsTrainingEnv = false;
            bool argIsTrainingEnvSynced = false;
            bool argP1Bot = false;
            string argP1TrainingAddress = "localhost";
            int argP1TrainingPort = 11000;
            bool argP2Bot = false;
            string argP2TrainingAddress = "localhost";
            int argP2TrainingPort = 11001;
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

                    case "--synced":
                        argIsTrainingEnvSynced = true;
                        break;
                    
                    case "--p1-bot":
                        argP1Bot = true;
                        break;
                        
                    case "--p2-bot":
                        argP2Bot = true;
                        break;

                    case "--mute":
                        shouldMute = true;
                        break;
                    
                    case "--p1-address":
                        argP1TrainingAddress = args[argIndex + 1];
                        break;

                    case "--p1-port":
                        argP1TrainingPort = Convert.ToUInt16(args[argIndex + 1]);
                        break;

                    case "--p2-address":
                        argP2TrainingAddress = args[argIndex + 1];
                        break;

                    case "--p2-port":
                        argP2TrainingPort = Convert.ToUInt16(args[argIndex + 1]);
                        break;
                }

                argIndex++;
            }
            Debug.Log("Passed arguments: " + passedArguments + "\n"
                + "   Run as training environment? " + argIsTrainingEnv + "\n"
                + "   Fast forward training? " + argFastForward + "\n"
                + "   Synced? " + argIsTrainingEnvSynced + "\n"
                + "   Mute? " + shouldMute + "\n"
                + "   P1 Bot? " + argP1Bot + "\n"
                + "   P1 Training address: " + argP1TrainingAddress + "\n"
                + "   P1 Training port: " + argP1TrainingPort + "\n"
                + "   P2 Bot? " + argP2Bot + "\n"
                + "   P2 Training address: " + argP2TrainingAddress + "\n"
                + "   P2 Training port: " + argP2TrainingPort + "\n"
            );

            if (argIsTrainingEnv && argFastForward)
            {
                // Make the game run 20x faster for more efficient training
                Time.timeScale = 20;
                Application.targetFrameRate = 1000;
            }

            trainingManager = new TrainingManager(argIsTrainingEnv, argIsTrainingEnvSynced, 
                argP1Bot ? null : new TrainingRemoteActor(argP1TrainingAddress, argP1TrainingPort, argIsTrainingEnvSynced),
                argP2Bot ? null : new TrainingRemoteActor(argP2TrainingAddress, argP2TrainingPort, argIsTrainingEnvSynced)
            );
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