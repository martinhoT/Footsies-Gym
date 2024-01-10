using System;
using System.Collections.Generic;
using UnityEngine;

namespace Footsies
{
    // Internal state of a fighter at a particular time step
    [Serializable]
    public class FighterState
    {
        [Serializable]
        public class StateRect {
            public float x;
            public float y;
            public float width;
            public float height;
        }

        [Serializable]
        public class StateHitbox {
            public StateRect rect;
            public bool proximity;
            public int attackID;
        }

        public float[] position;
        public float velocity_x;
        public bool isFaceRight;

        public StateHitbox[] hitboxes;
        public StateRect[] hurtboxes;
        public StateRect pushbox;

        public int vitalHealth;
        public int guardHealth;

        public int currentActionID;
        public int currentActionFrame;
        public int currentActionHitCount;

        public int currentHitStunFrame;

        public int[] input;
        public int[] inputDown;
        public int[] inputUp;

        public bool isInputBackward;
        public bool isReserveProximityGuard;

        public int bufferActionID;
        public int reserveDamageActionID;

        public int spriteShakePosition;
        public int maxSpriteShakeFrame;

        public bool hasWon;

        public FighterState(Fighter fighter, int inputRecordFrame, int[] input, int[] inputDown, int[] inputUp, bool isInputBackward, bool isReserveProximityGuard, int bufferActionID, int reserveDamageActionID, int spriteShakePosition, int maxSpriteShakeFrame, bool hasWon)
        {
            position = new float[2] {fighter.position.x, fighter.position.y};
            velocity_x = fighter.velocity_x;
            isFaceRight = fighter.isFaceRight;

            hitboxes = new StateHitbox[fighter.hitboxes.Count];
            for (int i = 0; i < fighter.hitboxes.Count; i++)
            {
                Hitbox hitbox = fighter.hitboxes[i];

                hitboxes[i] = new()
                {
                    rect = new() {
                        x = hitbox.rect.x,
                        y = hitbox.rect.y,
                        width = hitbox.rect.width,
                        height = hitbox.rect.height,
                    },
                    proximity = hitbox.proximity,
                    attackID = hitbox.attackID,
                };
            }

            hurtboxes = new StateRect[fighter.hurtboxes.Count];
            for (int i = 0; i < fighter.hurtboxes.Count; i++)
            {
                Hurtbox hurtbox = fighter.hurtboxes[i];

                hurtboxes[i] = new()
                {
                    x = hurtbox.rect.x,
                    y = hurtbox.rect.y,
                    width = hurtbox.rect.width,
                    height = hurtbox.rect.height,
                };
            }
            
            pushbox = new()
            {
                x = fighter.pushbox.rect.x,
                y = fighter.pushbox.rect.y,
                width = fighter.pushbox.rect.width,
                height = fighter.pushbox.rect.height,
            };

            vitalHealth = fighter.vitalHealth;
            guardHealth = fighter.guardHealth;

            currentActionID = fighter.currentActionID;
            currentActionFrame = fighter.currentActionFrame;
            currentActionHitCount = fighter.currentActionHitCount;

            currentHitStunFrame = fighter.currentHitStunFrame;

            // private fields
            this.input = new int[inputRecordFrame];
            this.inputDown = new int[inputRecordFrame];
            this.inputUp = new int[inputRecordFrame];

            input.CopyTo(this.input, 0);
            inputDown.CopyTo(this.inputDown, 0);
            inputUp.CopyTo(this.inputUp, 0);

            this.isInputBackward = isInputBackward;
            this.isReserveProximityGuard = isReserveProximityGuard;

            this.bufferActionID = bufferActionID;
            this.reserveDamageActionID = reserveDamageActionID;

            this.spriteShakePosition = spriteShakePosition;
            this.maxSpriteShakeFrame = maxSpriteShakeFrame;

            this.hasWon = hasWon;
        }
    }
}