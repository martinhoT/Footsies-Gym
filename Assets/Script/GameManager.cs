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
        public bool isTrainingEnv { get; private set; }
        public bool isTrainingEnvSynced { get; private set; }
        public bool isTrainingByExample { get; private set; }
        public string trainingAddress { get; private set; }
        public int trainingPort { get; private set; }

        private bool shouldMute = false;

        private void Awake()
        {
            DontDestroyOnLoad(this.gameObject);

            Application.targetFrameRate = 60;

            // Default values
            isTrainingEnv = false;
            isTrainingEnvSynced = false;
            isTrainingByExample = false;
            trainingAddress = "localhost";
            trainingPort = 11000;

            string[] args = Environment.GetCommandLineArgs();
            string passedArguments = "";
            bool argFastForward = false;
            int argIndex = 0;
            foreach (var arg in args)
            {
                passedArguments += arg + " ";

                switch (arg)
                {
                    case "--training":
                        isTrainingEnv = true;
                        break;

                    case "--fast-forward":
                        argFastForward = true;
                        break;

                    case "--synced":
                        isTrainingEnvSynced = true;
                        break;

                    case "--by-example":
                        isTrainingByExample = true;
                        break;

                    case "--mute":
                        shouldMute = true;
                        break;
                    
                    case "--address":
                        trainingAddress = args[argIndex + 1];
                        break;

                    case "--port":
                        trainingPort = Convert.ToUInt16(args[argIndex + 1]);
                        break;
                }

                argIndex++;
            }
            Debug.Log("Passed arguments: " + passedArguments + "\n"
                + "   Run as training environment? " + isTrainingEnv + "\n"
                + "   Fast forward training? " + argFastForward + "\n"
                + "   Synced? " + isTrainingEnvSynced + "\n"
                + "   Training by example? " + isTrainingByExample + "\n"
                + "   Mute? " + shouldMute + "\n"
                + "   Training address: " + trainingAddress + "\n"
                + "   Training port: " + trainingPort + "\n"
            );

            if (isTrainingEnv && argFastForward)
            {
                // Make the game run 20x faster for more efficient training
                Time.timeScale = 20;
                Application.targetFrameRate = 1000;
            }
        }

        private void Start()
        {
            if (shouldMute && SoundManager.Instance.isAllOn)
            {
                SoundManager.Instance.toggleAll();
            }

            if (isTrainingEnv)
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