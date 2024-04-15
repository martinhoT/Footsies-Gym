using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

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

        public bool isDebugPause { get; private set; }

        private float introStateTime = 3f;
        private float koStateTime = 2f;
        private float endStateTime = 3f;
        private float endStateSkippableTime = 1.5f;

        private TrainingManager trainingManager;
        private TrainingRemoteControl trainingRemoteControl;

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
            trainingManager = GameManager.Instance.trainingManager;
            trainingRemoteControl = GameManager.Instance.trainingRemoteControl;

            if (trainingManager.isTraining)
            {
                BattleAI p1Bot = new(this, true);
                BattleAI p2Bot = new(this, false);
                trainingRemoteControl.SetP2Saved(trainingManager.actorP2);
                trainingRemoteControl.SetP2Bot(new TrainingBattleAIActor(p2Bot));

                // Awkward case, BattleCore is the only one who can setup the TrainingBattleAIActors, since only it can properly create BattleAIs
                GameManager.Instance.botP1?.SetAI(p1Bot);
                GameManager.Instance.botP2?.SetAI(p2Bot);

                // Wait for everything to setup (mainly remote connections)
                Task.WhenAll(new Task[] {trainingManager.Setup(), trainingRemoteControl.Setup()}).Wait();

                // Skip the intro and outro sequences. Note: we still have to transition to these states since they set up the battle and prepare the next one
                introStateTime = 0f; // TODO: intro time is important for FOOTSIES since moves can be charged during this time
                koStateTime = 0f;
                endStateTime = 0f;
            }
        }

        void OnDestroy()
        {
            // OnDestroy() may be called before Start(), so we use null-conditional operators
            trainingManager?.Close();
            trainingRemoteControl?.Close();
        }

        void FixedUpdate()
        {
            TrainingRemoteControl.Command command = trainingRemoteControl.ProcessCommand();
            switch (command)
            {
                case TrainingRemoteControl.Command.RESET:
                    Debug.Log("Received RESET command");
                    ChangeRoundState(RoundStateType.Intro);
                    break;

                case TrainingRemoteControl.Command.STATE_SAVE:
                    Debug.Log("Received STATE SAVE command");
                    trainingRemoteControl.SendBattleState(SaveState());
                    break;

                case TrainingRemoteControl.Command.STATE_LOAD:
                    Debug.Log("Received STATE LOAD command");
                    LoadState(trainingRemoteControl.GetDesiredBattleState());
                    break;
                
                case TrainingRemoteControl.Command.P2_BOT:
                    Debug.Log("Received P2 BOT command");
                    if (trainingRemoteControl.isP2Bot)
                    {
                        trainingManager.actorP2 = trainingRemoteControl.p2Bot;
                    }
                    else
                    {
                        trainingManager.actorP2 = trainingRemoteControl.p2Saved;
                    }
                    break;
                
                case TrainingRemoteControl.Command.SEED:
                    Debug.Log("Setting random seed to " + trainingRemoteControl.seed.ToString());
                    Random.InitState(trainingRemoteControl.seed);
                    break;
            }

            switch(_roundState)
            {
                case RoundStateType.Stop:

                    ChangeRoundState(RoundStateType.Intro);

                    break;
                case RoundStateType.Intro:

                    if (trainingManager.actorP1 is TrainingBattleAIActor)
                        GameManager.Instance.botP1.GetAI().Reset();
                    if (trainingManager.actorP2 is TrainingBattleAIActor)
                        GameManager.Instance.botP2.GetAI().Reset();
                    battleAI?.Reset();

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

                    if(CheckUpdateDebugPause() || !trainingManager.Ready())
                    {
                        break;
                    }

                    frameCount++;
                    
                    UpdateFightState();

                    var deadFighter = _fighters.Find((f) => f.isDead);
                    bool battleOver = deadFighter != null;
                    if(battleOver)
                    {
                        ChangeRoundState(RoundStateType.KO);
                    }
                    trainingManager.Step(GetEnvironmentState(), battleOver);

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

                    if(!trainingManager.isTraining
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
                        battleAI = new BattleAI(this, false);

                    break;
                case RoundStateType.Fight:

                    roundStartTime = Time.fixedTime;
                    frameCount = -1;

                    currentRecordingInputIndex = 0;

                    // Environment reset, should send initial state first before receiving actions, and request the first action
                    trainingManager.Step(GetEnvironmentState(), false);

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
            var p1Input = GetP1InputData();
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
            var p1Input = GetP1InputData();
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

            InputData p1Input = new();
            if (trainingManager.isTraining)
            {
                p1Input.input = trainingManager.p1Input();
            }
            else
            {
                p1Input.input |= InputManager.Instance.gameplay.p1Left.IsPressed() ? (int)InputDefine.Left : 0;
                p1Input.input |= InputManager.Instance.gameplay.p1Right.IsPressed() ? (int)InputDefine.Right : 0;
                p1Input.input |= InputManager.Instance.gameplay.p1Attack.IsPressed() ? (int)InputDefine.Attack : 0;
            }
            p1Input.time = time;

            if (debugP1Attack)
                p1Input.input |= (int)InputDefine.Attack;
            if (debugP1Guard)
                p1Input.input |= (int)InputDefine.Left;

            return p1Input;
        }

        InputData GetP2InputData()
        {
            if (isReplayingLastRoundInput)
            {
                return lastRoundP2Input[currentReplayingInputIndex];
            }

            var time = Time.fixedTime - roundStartTime;

            InputData p2Input = new();

            if (trainingManager.isTraining)
            {
                p2Input.input = trainingManager.p2Input();
            }
            else if (battleAI != null)
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

        private EnvironmentState GetEnvironmentState()
        {
            return new EnvironmentState(
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
                frameCount, // globalFrame
                (currentRecordingInputIndex > 0) ? recordingP1Input[currentRecordingInputIndex - 1].input : 0, // p1MostRecentAction
                (currentRecordingInputIndex > 0) ? recordingP2Input[currentRecordingInputIndex - 1].input : 0 // p2MostRecentAction
            );
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
                if (InputManager.Instance.gameplay.debugPauseAdvance.WasPressedThisFrame())
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

        public BattleState SaveState()
        {
            return new(
                this,
                roundStartTime,
                frameCount
            );
        }

        public void LoadState(BattleState state)
        {
            fighter1.LoadState(state.p1State);
            fighter2.LoadState(state.p2State);

            roundStartTime = state.roundStartTime;
            frameCount = state.frameCount;
        }
    }

}
