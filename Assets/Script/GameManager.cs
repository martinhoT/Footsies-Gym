using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        private bool shouldMute = false;

        private void Awake()
        {
            DontDestroyOnLoad(this.gameObject);

            Application.targetFrameRate = 60;

            string[] args = System.Environment.GetCommandLineArgs();
            string passedArguments = "";
            bool argAskedForHelp = false;
            foreach (var arg in args)
            {
                passedArguments += arg + " ";

                switch (arg)
                {
                    case "--training":
                        isTrainingEnv = true;
                        break;

                    case "--help":
                        argAskedForHelp = true;
                        break;

                    case "--mute":
                        shouldMute = true;
                        break;
                }
            }
            Debug.Log("Passed arguments: " + passedArguments + "\n"
                + "   Run as training environment? " + isTrainingEnv + "\n"
                + "   Help? " + argAskedForHelp + "\n"
                + "   Mute? " + shouldMute + "\n"
            );
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