﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Footsies
{

    public class BattleAI
    {
        public class FightState
        {
            public float distanceX;
            public bool isOpponentDamage;
            public bool isOpponentGuardBreak;
            public bool isOpponentBlocking;
            public bool isOpponentNormalAttack;
            public bool isOpponentSpecialAttack;
        }

        private BattleCore battleCore;
        private Fighter opponent;
        private bool isPlayer1;

        private Queue<int> moveQueue = new Queue<int>();
        private Queue<int> attackQueue = new Queue<int>();

        // previous fight state data
        private FightState[] fightStates = new FightState[maxFightStateRecord];
        public static readonly uint maxFightStateRecord = 10;
        private int fightStateReadIndex = 5;

        public BattleAI(BattleCore core, bool player1)
        {
            battleCore = core;
            isPlayer1 = player1;
            opponent = this.isPlayer1 ? battleCore.fighter2 : battleCore.fighter1;
        }

        public int getNextAIInput()
        {
            int input = 0;

            UpdateFightState();
            var fightState = GetCurrentFightState();
            if (fightState != null)
            {
                //Debug.Log(fightState.distanceX);
                if (moveQueue.Count > 0)
                    input |= moveQueue.Dequeue();
                else if (moveQueue.Count == 0)
                {
                    SelectMovement(fightState);
                }

                if (attackQueue.Count > 0)
                    input |= attackQueue.Dequeue();
                else if (attackQueue.Count == 0)
                {
                    SelectAttack(fightState);
                }
            }

            return input;
        }

        private void SelectMovement(FightState fightState)
        {
            if (fightState.distanceX > 4f)
            {
                var rand = Random.Range(0, 2);
                if (rand == 0)
                    AddFarApproach1();
                else
                    AddFarApproach2();
            }
            else if (fightState.distanceX > 3f)
            {
                var rand = Random.Range(0, 7);
                if (rand <= 1)
                    AddMidApproach1();
                else if (rand <= 3)
                    AddMidApproach2();
                else if (rand == 4)
                    AddFarApproach1();
                else if (rand == 5)
                    AddFarApproach2();
                else
                    AddNeutralMovement();
            }
            else if (fightState.distanceX > 2.5f)
            {
                var rand = Random.Range(0, 5);
                if (rand == 0)
                    AddMidApproach1();
                else if (rand == 1)
                    AddMidApproach2();
                else if (rand == 2)
                    AddFallBack1();
                else if (rand == 3)
                    AddFallBack2();
                else
                    AddNeutralMovement();
            }
            else if (fightState.distanceX > 2f)
            {
                var rand = Random.Range(0, 4);
                if (rand == 0)
                    AddFallBack1();
                else if (rand == 1)
                    AddFallBack2();
                else
                    AddNeutralMovement();
            }
            else
            {
                var rand = Random.Range(0, 3);
                if (rand == 0)
                    AddFallBack1();
                else if (rand == 1)
                    AddFallBack2();
                else
                    AddNeutralMovement();
            }
        }

        private void SelectAttack(FightState fightState)
        {
            if (fightState.isOpponentDamage
                || fightState.isOpponentGuardBreak
                || fightState.isOpponentSpecialAttack)
            {
                AddTwoHitImmediateAttack();
            }
            else if (fightState.distanceX > 4f)
            {
                var rand = Random.Range(0, 4);
                if (rand <= 3)
                    AddNoAttack();
                else
                    AddDelaySpecialAttack();
            }
            else if (fightState.distanceX > 3f)
            {
                if (fightState.isOpponentNormalAttack)
                {
                    AddTwoHitImmediateAttack();
                    return;
                }

                var rand = Random.Range(0, 5);
                if (rand <= 1)
                    AddNoAttack();
                else if (rand <= 3)
                    AddOneHitImmediateAttack();
                else
                    AddDelaySpecialAttack();
            }
            else if (fightState.distanceX > 2.5f)
            {
                var rand = Random.Range(0, 3);
                if (rand == 0)
                    AddNoAttack();
                else if (rand== 1)
                    AddOneHitImmediateAttack();
                else
                    AddTwoHitImmediateAttack();
            }
            else if (fightState.distanceX > 2f)
            {
                var rand = Random.Range(0, 6);
                if (rand <= 1)
                    AddOneHitImmediateAttack();
                else if (rand <= 3)
                    AddTwoHitImmediateAttack();
                else if(rand == 4)
                    AddImmediateSpecialAttack();
                else
                    AddDelaySpecialAttack();
            }
            else
            {
                var rand = Random.Range(0, 3);
                if (rand == 0)
                    AddOneHitImmediateAttack();
                else
                    AddTwoHitImmediateAttack();
            }
        }

        private void AddNeutralMovement()
        {
            for (int i = 0; i < 30; i++)
            {
                moveQueue.Enqueue(0);
            }

            Debug.Log("AddNeutral");
        }

        private void AddFarApproach1()
        {
            AddForwardInputQueue(40);
            AddBackwardInputQueue(10);
            AddForwardInputQueue(30);
            AddBackwardInputQueue(10);

            Debug.Log("AddFarApproach1");
        }

        private void AddFarApproach2()
        {
            AddForwardDashInputQueue();
            AddBackwardInputQueue(25);
            AddForwardDashInputQueue();
            AddBackwardInputQueue(25);

            Debug.Log("AddFarApproach2");
        }
        
        private void AddMidApproach1()
        {
            AddForwardInputQueue(30);
            AddBackwardInputQueue(10);
            AddForwardInputQueue(20);
            AddBackwardInputQueue(10);

            Debug.Log("AddMidApproach1");
        }

        private void AddMidApproach2()
        {
            AddForwardDashInputQueue();
            AddBackwardInputQueue(30);

            Debug.Log("AddMidApproach2");
        }

        private void AddFallBack1()
        {
            AddBackwardInputQueue(60);

            Debug.Log("AddFallBack1");
        }

        private void AddFallBack2()
        {
            AddBackwardDashInputQueue();
            AddBackwardInputQueue(60);

            Debug.Log("AddFallBack2");
        }

        private void AddNoAttack()
        {
            for (int i = 0; i < 30; i++)
            {
                attackQueue.Enqueue(0);
            }

            Debug.Log("AddNoAttack");
        }

        private void AddOneHitImmediateAttack()
        {
            attackQueue.Enqueue(GetAttackInput());
            for (int i = 0; i < 18; i++)
            {
                attackQueue.Enqueue(0);
            }

            Debug.Log("AddOneHitImmediateAttack");
        }

        private void AddTwoHitImmediateAttack()
        {
            attackQueue.Enqueue(GetAttackInput());
            for (int i = 0; i < 3; i++)
            {
                attackQueue.Enqueue(0);
            }
            attackQueue.Enqueue(GetAttackInput());
            for (int i = 0; i < 18; i++)
            {
                attackQueue.Enqueue(0);
            }

            Debug.Log("AddTwoHitImmediateAttack");
        }

        private void AddImmediateSpecialAttack()
        {
            for (int i = 0; i < 60; i++)
            {
                attackQueue.Enqueue(GetAttackInput());
            }
            attackQueue.Enqueue(0);

            Debug.Log("AddImmediateSpecialAttack");
        }

        private void AddDelaySpecialAttack()
        {
            for (int i = 0; i < 120; i++)
            {
                attackQueue.Enqueue(GetAttackInput());
            }
            attackQueue.Enqueue(0);

            Debug.Log("AddDelaySpecialAttack");
        }

        private void AddForwardInputQueue(int frame)
        {
            for(int i = 0; i < frame; i++)
            {
                moveQueue.Enqueue(GetForwardInput());
            }
        }

        private void AddBackwardInputQueue(int frame)
        {
            for (int i = 0; i < frame; i++)
            {
                moveQueue.Enqueue(GetBackwardInput());
            }
        }

        private void AddForwardDashInputQueue()
        {
            moveQueue.Enqueue(GetForwardInput());
            moveQueue.Enqueue(0);
            moveQueue.Enqueue(GetForwardInput());
        }

        private void AddBackwardDashInputQueue()
        {
            moveQueue.Enqueue(GetForwardInput());
            moveQueue.Enqueue(0);
            moveQueue.Enqueue(GetForwardInput());
        }

        private void UpdateFightState()
        {
            var currentFightState = new FightState();
            currentFightState.distanceX = GetDistanceX();
            currentFightState.isOpponentDamage = opponent.currentActionID == (int)CommonActionID.DAMAGE;
            currentFightState.isOpponentGuardBreak= opponent.currentActionID == (int)CommonActionID.GUARD_BREAK;
            currentFightState.isOpponentBlocking = (opponent.currentActionID == (int)CommonActionID.GUARD_CROUCH
                                                    || opponent.currentActionID == (int)CommonActionID.GUARD_STAND
                                                    || opponent.currentActionID == (int)CommonActionID.GUARD_M);
            currentFightState.isOpponentNormalAttack = (opponent.currentActionID == (int)CommonActionID.N_ATTACK
                                                    || opponent.currentActionID == (int)CommonActionID.B_ATTACK);
            currentFightState.isOpponentSpecialAttack = (opponent.currentActionID == (int)CommonActionID.N_SPECIAL
                                                    || opponent.currentActionID == (int)CommonActionID.B_SPECIAL);

            for (int i = 1; i < fightStates.Length; i++)
            {
                fightStates[i] = fightStates[i - 1];
            }
            fightStates[0] = currentFightState;
        }

        private FightState GetCurrentFightState()
        {
            return fightStates[fightStateReadIndex];
        }

        private float GetDistanceX()
        {
            return Mathf.Abs(battleCore.fighter2.position.x - battleCore.fighter1.position.x);
        }

        private int GetAttackInput()
        {
            return (int)InputDefine.Attack;
        }

        private int GetForwardInput()
        {
            return (int) (this.isPlayer1 ? InputDefine.Right : InputDefine.Left);
        }

        private int GetBackwardInput()
        {
            return (int) (this.isPlayer1 ? InputDefine.Left : InputDefine.Right);
        }

        // This method exists primarily to properly set the initial state of battleAIs during training.
        // TrainingBattleAIActors will keep the move and attack queues if we don't reset,
        // and since the intro sequence is skipped the fightStates array will not be properly filled.
        public void Reset()
        {
            // Don't keep moves between rounds.
            moveQueue.Clear();
            attackQueue.Clear();
            
            // In order for the BattleAI to correctly have the fightStates array filled,
            // we need to manually perform the filling before a fight.
            UpdateFightState();
            Array.Fill(fightStates, fightStates[0]);
        }

    }

}