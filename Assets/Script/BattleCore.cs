using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System.Net;
using System;
using System.Net.Sockets;
using System.Text; // TODO: remove after debugging socket messages

namespace Footsies
{
    /// <summary>
    /// Main update for battle engine
    /// Update player/ai input, fighter actions, hitbox/hurtbox collision, round start/end
    /// </summary>
    public class BattleCore : MonoBehaviour
    {
        public enum RoundStateType
        {
            Stop,
            Intro,
            Fight,
            KO,
            End,
        }

        [SerializeField]
        private float _battleAreaWidth = 10f;
        public float battleAreaWidth { get { return _battleAreaWidth; } }

        [SerializeField]
        private float _battleAreaMaxHeight = 2f;
        public float battleAreaMaxHeight { get { return _battleAreaMaxHeight; } }

        [SerializeField]
        private GameObject roundUI;

        [SerializeField]
        private List<FighterData> fighterDataList = new List<FighterData>();

        public bool debugP1Attack = false;
        public bool debugP2Attack = false;
        public bool debugP1Guard = false;
        public bool debugP2Guard = false;

        public bool debugPlayLastRoundInput = false;

        private float timer = 0;
        private uint maxRoundWon = 3;

        public Fighter fighter1 { get; private set; }
        public Fighter fighter2 { get; private set; }

        public uint fighter1RoundWon { get; private set; }
        public uint fighter2RoundWon { get; private set; }

        public List<Fighter> fighters { get { return _fighters; } }
        private List<Fighter> _fighters = new List<Fighter>();

        private float roundStartTime;
        private int frameCount;

        public RoundStateType roundState { get { return _roundState; } }
        private RoundStateType _roundState = RoundStateType.Stop;

        public System.Action<Fighter, Vector2, DamageResult> damageHandler;

        private Animator roundUIAnimator;

        private BattleAI battleAI = null;

        private static uint maxRecordingInputFrame = 60 * 60 * 5;
        private InputData[] recordingP1Input = new InputData[maxRecordingInputFrame];
        private InputData[] recordingP2Input = new InputData[maxRecordingInputFrame];
        private uint currentRecordingInputIndex = 0;

        private InputData[] lastRoundP1Input = new InputData[maxRecordingInputFrame];
        private InputData[] lastRoundP2Input = new InputData[maxRecordingInputFrame];
        private uint currentReplayingInputIndex = 0;
        private uint lastRoundMaxRecordingInput = 0;
        private bool isReplayingLastRoundInput = false;

        private InputData p1TrainingInput = null;
        private bool isTrainingEnv = false;

        public bool isDebugPause { get; private set; }
        
        private bool trainingStepPerformed = false; // read-only on the main thread
        private bool trainingStepRequested = false;

        private float introStateTime = 3f;
        private float koStateTime = 2f;
        private float endStateTime = 3f;
        private float endStateSkippableTime = 1.5f;

        Socket trainingListener;
        Socket p1TrainingSocket;

        void Awake()
        {
            // Setup dictionary from ScriptableObject data
            fighterDataList.ForEach((data) => data.setupDictionary());

            fighter1 = new Fighter();
            fighter2 = new Fighter();

            _fighters.Add(fighter1);
            _fighters.Add(fighter2);

            if(roundUI != null)
            {
                roundUIAnimator = roundUI.GetComponent<Animator>();
            }
        }
        
        void Start()
        {
            // This value should not change while we are in the battle scene.
            // We store this value since we need to know whether a socket was created so we
            // can close it when the application quits (GameManager may already be destroyed).
            isTrainingEnv = GameManager.Instance.isTrainingEnv;

            if (isTrainingEnv)
            {
                // Setup Socket server to listen for the agent's actions
                IPAddress localhostAddress = null;
                foreach (var address in System.Net.Dns.GetHostAddresses("localhost"))
                {
                    // Only accept IPv4 addresses
                    if (address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        localhostAddress = address;
                        break; // return the first one found
                    }
                }
                if (localhostAddress == null)
                {
                    Debug.Log("ERROR: could not find any suitable IPv4 address for 'localhost'! Quitting...");
                    Application.Quit();
                }
                IPEndPoint ipEndPoint = new IPEndPoint(localhostAddress, 11000); // TODO: hardcoded port and address
                trainingListener = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                trainingListener.Bind(ipEndPoint);
                trainingListener.Listen(1); // maximum queue length of 1, there is only 1 agent
                Debug.Log("Waiting for the agent to connect to address '" + localhostAddress.ToString() + "'...");
                p1TrainingSocket = trainingListener.Accept();
                Debug.Log("Agent connection received!");
                trainingListener.Close();

                // We activate debug pause to make the game advance frame-by-frame
                isDebugPause = true;
            }
        }

        void OnDestroy()
        {
            if (isTrainingEnv)
            {
                p1TrainingSocket.Shutdown(SocketShutdown.Both);
                p1TrainingSocket.Close();
            }
        }

        void FixedUpdate()
        {
            switch(_roundState)
            {
                case RoundStateType.Stop:

                    ChangeRoundState(RoundStateType.Intro);

                    break;
                case RoundStateType.Intro:

                    UpdateIntroState();

                    timer -= Time.deltaTime;
                    if (timer <= 0f)
                    {
                        ChangeRoundState(RoundStateType.Fight);
                        Debug.Log("The fight has started!");
                    }

                    if (debugPlayLastRoundInput
                        && !isReplayingLastRoundInput)
                    {
                        StartPlayLastRoundInput();
                    }

                    break;
                case RoundStateType.Fight:

                    if(CheckUpdateDebugPause())
                    {
                        break;
                    }

                    frameCount++;
                    
                    UpdateFightState();

                    var deadFighter = _fighters.Find((f) => f.isDead);
                    if(deadFighter != null)
                    {
                        ChangeRoundState(RoundStateType.KO);
                    }
                    // request another action from the training agent, as long as the environment hasn't terminated
                    else if (isTrainingEnv)
                    {
                        trainingStepPerformed = false; // the current step is over
                        RequestP1TrainingInput();
                    }

                    break;
                case RoundStateType.KO:

                    UpdateKOState();
                    timer -= Time.deltaTime;
                    if (timer <= 0f)
                    {
                        ChangeRoundState(RoundStateType.End);
                        Debug.Log("Someone got knocked out!");
                    }

                    break;
                case RoundStateType.End:

                    UpdateEndState();
                    timer -= Time.deltaTime;
                    if (timer <= 0f
                        || (timer <= endStateSkippableTime && IsKOSkipButtonPressed()))
                    {
                        ChangeRoundState(RoundStateType.Stop);
                        Debug.Log("Fight over!");
                    }

                    break;
            }
        }

        void ChangeRoundState(RoundStateType state)
        {
            _roundState = state;
            switch (_roundState)
            {
                case RoundStateType.Stop:

                    if(!isTrainingEnv
                        && (fighter1RoundWon >= maxRoundWon
                        || fighter2RoundWon >= maxRoundWon))
                    {
                        GameManager.Instance.LoadTitleScene();
                    }

                    break;
                case RoundStateType.Intro:

                    fighter1.SetupBattleStart(fighterDataList[0], new Vector2(-2f, 0f), true);
                    fighter2.SetupBattleStart(fighterDataList[0], new Vector2(2f, 0f), false);

                    timer = introStateTime;

                    roundUIAnimator.SetTrigger("RoundStart");

                    if (GameManager.Instance.isVsCPU)
                        battleAI = new BattleAI(this);

                    break;
                case RoundStateType.Fight:

                    roundStartTime = Time.fixedTime;
                    frameCount = -1;

                    currentRecordingInputIndex = 0;

                    // Environment reset, should send initial state first before receiving actions, and request the first action
                    if (isTrainingEnv)
                    {
                        SendCurrentState();
                        ReceiveP1TrainingInput();
                    }

                    break;
                case RoundStateType.KO:

                    timer = koStateTime;

                    CopyLastRoundInput();

                    fighter1.ClearInput();
                    fighter2.ClearInput();

                    battleAI = null;

                    roundUIAnimator.SetTrigger("RoundEnd");

                    break;
                case RoundStateType.End:

                    timer = endStateTime;

                    var deadFighter = _fighters.FindAll((f) => f.isDead);
                    if (deadFighter.Count == 1)
                    {
                        if (deadFighter[0] == fighter1)
                        {
                            fighter2RoundWon++;
                            fighter2.RequestWinAction();
                        }
                        else if (deadFighter[0] == fighter2)
                        {
                            fighter1RoundWon++;
                            fighter1.RequestWinAction();
                        }
                    }

                    break;
            }
        }

        void UpdateIntroState()
        {
            InputData p1Input = null;
            // Ignore the battle intro, only start listening for actions when battle actually starts
            if (isTrainingEnv)
            {
                p1Input = new InputData();
                p1Input.time = Time.fixedTime - roundStartTime;
            }
            else
            {
                p1Input = GetP1InputData();
            }
            var p2Input = GetP2InputData();
            RecordInput(p1Input, p2Input);
            fighter1.UpdateInput(p1Input);
            fighter2.UpdateInput(p2Input);

            _fighters.ForEach((f) => f.IncrementActionFrame());

            _fighters.ForEach((f) => f.UpdateIntroAction());
            _fighters.ForEach((f) => f.UpdateMovement());
            _fighters.ForEach((f) => f.UpdateBoxes());

            UpdatePushCharacterVsCharacter();
            UpdatePushCharacterVsBackground();
        }

        void UpdateFightState()
        {
            var p1Input = isTrainingEnv ? p1TrainingInput : GetP1InputData();
            var p2Input = GetP2InputData();
            RecordInput(p1Input, p2Input);
            fighter1.UpdateInput(p1Input);
            fighter2.UpdateInput(p2Input);

            _fighters.ForEach((f) => f.IncrementActionFrame());

            _fighters.ForEach((f) => f.UpdateActionRequest());
            _fighters.ForEach((f) => f.UpdateMovement());
            _fighters.ForEach((f) => f.UpdateBoxes());

            UpdatePushCharacterVsCharacter();
            UpdatePushCharacterVsBackground();
            UpdateHitboxHurtboxCollision();

            if (isTrainingEnv)
            {
                SendCurrentState();
                RequestP1TrainingInput();
            }
        }

        void UpdateKOState()
        {

        }

        void UpdateEndState()
        {
            _fighters.ForEach((f) => f.IncrementActionFrame());

            _fighters.ForEach((f) => f.UpdateActionRequest());
            _fighters.ForEach((f) => f.UpdateMovement());
            _fighters.ForEach((f) => f.UpdateBoxes());

            UpdatePushCharacterVsCharacter();
            UpdatePushCharacterVsBackground();
        }

        InputData GetP1InputData()
        {
            if(isReplayingLastRoundInput)
            {
                return lastRoundP1Input[currentReplayingInputIndex];
            }

            var time = Time.fixedTime - roundStartTime;

            InputData p1Input = new InputData();
            p1Input.input |= InputManager.Instance.gameplay.p1Left.IsPressed() ? (int)InputDefine.Left : 0;
            p1Input.input |= InputManager.Instance.gameplay.p1Right.IsPressed() ? (int)InputDefine.Right : 0;
            p1Input.input |= InputManager.Instance.gameplay.p1Attack.IsPressed() ? (int)InputDefine.Attack : 0;
            p1Input.time = time;

            if (debugP1Attack)
                p1Input.input |= (int)InputDefine.Attack;
            if (debugP1Guard)
                p1Input.input |= (int)InputDefine.Left;

            return p1Input;
        }

        // no-op if a request is still unfulfilled
        void RequestP1TrainingInput()
        {
            if (!trainingStepPerformed && !trainingStepRequested)
            {
                trainingStepRequested = true;
                ReceiveP1TrainingInput();
            }
        }

        private async void ReceiveP1TrainingInput()
        {
            var time = Time.fixedTime - roundStartTime;

            byte[] actionMessageContent = {0, 0, 0};
            ArraySegment<byte> actionMessage = new ArraySegment<byte>(actionMessageContent);

            Debug.Log("Waiting for the agent's action...");
            int bytesReceived = await p1TrainingSocket.ReceiveAsync(actionMessage, SocketFlags.None);
            Debug.Log("Agent action received! (" + (int)actionMessageContent[0] + ", " + (int)actionMessageContent[1] + ", " + (int)actionMessageContent[2] + ")");
            if (bytesReceived != 3)
            {
                Debug.Log("ERROR: abnormal number of bytes received from agent's action message (sent " + bytesReceived + ", expected 3)");
            }
            
            p1TrainingInput = new InputData();
            p1TrainingInput.input |= actionMessageContent[0] != 0 ? (int)InputDefine.Left : 0;
            p1TrainingInput.input |= actionMessageContent[1] != 0 ? (int)InputDefine.Right : 0;
            p1TrainingInput.input |= actionMessageContent[2] != 0 ? (int)InputDefine.Attack : 0;
            p1TrainingInput.time = time;

            trainingStepRequested = false;
            trainingStepPerformed = true;
        }

        InputData GetP2InputData()
        {
            if (isReplayingLastRoundInput)
            {
                return lastRoundP2Input[currentReplayingInputIndex];
            }

            var time = Time.fixedTime - roundStartTime;

            InputData p2Input = new InputData();

            if (battleAI != null)
            {
                p2Input.input |= battleAI.getNextAIInput();
            }
            else
            {
                p2Input.input |= InputManager.Instance.gameplay.p2Left.IsPressed() ? (int)InputDefine.Left : 0;
                p2Input.input |= InputManager.Instance.gameplay.p2Right.IsPressed() ? (int)InputDefine.Right : 0;
                p2Input.input |= InputManager.Instance.gameplay.p2Attack.IsPressed() ? (int)InputDefine.Attack : 0;
            }

            p2Input.time = time;

            if (debugP2Attack)
                p2Input.input |= (int)InputDefine.Attack;
            if (debugP2Guard)
                p2Input.input |= (int)InputDefine.Right;

            return p2Input;
        }

        void SendCurrentState()
        {
            fighter1 = _fighters[0];
            fighter2 = _fighters[1];
            EnvironmentState state = new EnvironmentState(
               fighter1.vitalHealth, // p1Vital
               fighter2.vitalHealth, // p2Vital
               fighter1.guardHealth, // p1Guard
               fighter2.guardHealth, // p2Guard
               fighter1.currentActionID, // p1Move
               fighter1.currentActionFrame, // p1MoveFrame
               fighter2.currentActionID, // p2Move
               fighter2.currentActionFrame, // p2MoveFrame
               fighter1.position.x, // p1Position
               fighter2.position.x, // p2Position
               frameCount // globalFrame
            );
            string state_json = JsonUtility.ToJson(state);
            Debug.Log("Sending the game's current state...");
            p1TrainingSocket.Send(Encoding.UTF8.GetBytes(state_json));
            Debug.Log("Current state received by the agent!");
        }

        private bool IsKOSkipButtonPressed()
        {
            // if (InputManager.Instance.GetButton(InputManager.Command.p1Attack))
            if (InputManager.Instance.gameplay.p1Attack.WasPressedThisFrame())
                return true;

            // if (InputManager.Instance.GetButton(InputManager.Command.p2Attack))
            if (InputManager.Instance.gameplay.p2Attack.WasPressedThisFrame())
                return true;

            return false;
        }
        
        void UpdatePushCharacterVsCharacter()
        {
            var rect1 = fighter1.pushbox.rect;
            var rect2 = fighter2.pushbox.rect;

            if (rect1.Overlaps(rect2))
            {
                if (fighter1.position.x < fighter2.position.x)
                {
                    fighter1.ApplyPositionChange((rect1.xMax - rect2.xMin) * -1 / 2, fighter1.position.y);
                    fighter2.ApplyPositionChange((rect1.xMax - rect2.xMin) * 1 / 2, fighter2.position.y);
                }
                else if (fighter1.position.x > fighter2.position.x)
                {
                    fighter1.ApplyPositionChange((rect2.xMax - rect1.xMin) * 1 / 2, fighter1.position.y);
                    fighter2.ApplyPositionChange((rect2.xMax - rect1.xMin) * -1 / 2, fighter1.position.y);
                }
            }
        }

        void UpdatePushCharacterVsBackground()
        {
            var stageMinX = battleAreaWidth * -1 / 2;
            var stageMaxX = battleAreaWidth / 2;

            _fighters.ForEach((f) =>
            {
                if (f.pushbox.xMin < stageMinX)
                {
                    f.ApplyPositionChange(stageMinX - f.pushbox.xMin, f.position.y);
                }
                else if (f.pushbox.xMax > stageMaxX)
                {
                    f.ApplyPositionChange(stageMaxX - f.pushbox.xMax, f.position.y);
                }
            });
        }

        void UpdateHitboxHurtboxCollision()
        {
            foreach(var attacker in _fighters)
            {
                Vector2 damagePos = Vector2.zero;
                bool isHit = false;
                bool isProximity = false;
                int hitAttackID = 0;

                foreach (var damaged in _fighters)
                {
                    if (attacker == damaged)
                        continue;
                    
                    foreach (var hitbox in attacker.hitboxes)
                    {
                        // continue if attack already hit
                        if(!attacker.CanAttackHit(hitbox.attackID))
                        {
                            continue;
                        }

                        foreach (var hurtbox in damaged.hurtboxes)
                        {
                            if (hitbox.Overlaps(hurtbox))
                            {
                                if (hitbox.proximity)
                                {
                                    isProximity = true;
                                }
                                else
                                {
                                    isHit = true;
                                    hitAttackID = hitbox.attackID;
                                    float x1 = Mathf.Min(hitbox.xMax, hurtbox.xMax);
                                    float x2 = Mathf.Max(hitbox.xMin, hurtbox.xMin);
                                    float y1 = Mathf.Min(hitbox.yMax, hurtbox.yMax);
                                    float y2 = Mathf.Max(hitbox.yMin, hurtbox.yMin);
                                    damagePos.x = (x1 + x2) / 2;
                                    damagePos.y = (y1 + y2) / 2;
                                    break;
                                }
                                
                            }
                        }

                        if (isHit)
                            break;
                    }

                    if (isHit)
                    {
                        attacker.NotifyAttackHit(damaged, damagePos);
                        var damageResult = damaged.NotifyDamaged(attacker.getAttackData(hitAttackID), damagePos);

                        var hitStunFrame = attacker.GetHitStunFrame(damageResult, hitAttackID);
                        attacker.SetHitStun(hitStunFrame);
                        damaged.SetHitStun(hitStunFrame);
                        damaged.SetSpriteShakeFrame(hitStunFrame / 3);

                        damageHandler(damaged, damagePos, damageResult);
                    }
                    else if (isProximity)
                    {
                        damaged.NotifyInProximityGuardRange();
                    }
                }


            }
        }

        void RecordInput(InputData p1Input, InputData p2Input)
        {
            if (currentRecordingInputIndex >= maxRecordingInputFrame)
                return;

            recordingP1Input[currentRecordingInputIndex] = p1Input.ShallowCopy();
            recordingP2Input[currentRecordingInputIndex] = p2Input.ShallowCopy();
            currentRecordingInputIndex++;

            if (isReplayingLastRoundInput)
            {
                if (currentReplayingInputIndex < lastRoundMaxRecordingInput)
                    currentReplayingInputIndex++;
            }
        }

        void CopyLastRoundInput()
        {
            for(int i = 0; i < currentRecordingInputIndex; i++)
            {
                lastRoundP1Input[i] = recordingP1Input[i].ShallowCopy();
                lastRoundP2Input[i] = recordingP2Input[i].ShallowCopy();
            }
            lastRoundMaxRecordingInput = currentRecordingInputIndex;
            
            isReplayingLastRoundInput = false;
            currentReplayingInputIndex = 0;
        }

        void StartPlayLastRoundInput()
        {
            isReplayingLastRoundInput = true;
            currentReplayingInputIndex = 0;
        }

        bool CheckUpdateDebugPause()
        {
            if (InputManager.Instance.gameplay.debugPause.WasPressedThisFrame())
            {
                isDebugPause = !isDebugPause;
            }

            if (isDebugPause)
            {
                // press f2 during debug pause to advance 1 frame
                if (InputManager.Instance.gameplay.debugPauseAdvance.WasPressedThisFrame() || trainingStepPerformed)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }

            return false;
        }

        public int GetFrameAdvantage(bool getP1)
        {
            var p1FrameLeft = fighter1.currentActionFrameCount - fighter1.currentActionFrame;
            if (fighter1.isAlwaysCancelable)
                p1FrameLeft = 0;

            var p2FrameLeft = fighter2.currentActionFrameCount - fighter2.currentActionFrame;
            if (fighter2.isAlwaysCancelable)
                p2FrameLeft = 0;

            if (getP1)
                return p2FrameLeft - p1FrameLeft;
            else
                return p1FrameLeft - p2FrameLeft;
        }
    }

}