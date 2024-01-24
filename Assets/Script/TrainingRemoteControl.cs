using UnityEngine;
using System;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Footsies
{
    // Remote controller that allows modifying the game, such as transitioning to a specific state
    public class TrainingRemoteControl
    {
        // Available commands:
        // - Reset: reset the battle to the beginning
        // - StateSave: request a copy of the current state
        // - StateLoad: request the game to load a specific state
        // - P2Bot: toggle between the initial actor and the in-game bot for player 2
        public enum Command
        {
            NONE = 0,
            RESET = 1,
            STATE_SAVE = 2,
            STATE_LOAD = 3,
            P2_BOT = 4,
        }

        [Serializable]
        private class Message
        {
            public int command;
            public string value;
        }

        public string address { get; private set; }
        public int port { get; private set; }
        public bool syncedComms { get; private set; }

        private BattleState battleState;
        public TrainingActor p2Saved { get; private set; }
        public TrainingBattleAIActor p2Bot { get; private set; }
        public bool isP2Bot { get; private set; }

        private Socket managerSocket;

        private bool connected;

        public TrainingRemoteControl(string address, int port, bool syncedComms)
        {
            this.address = address;
            this.port = port;
            this.syncedComms = syncedComms;
        }

        public async Task Setup()
        {
            Debug.Log("Waiting for the agent to connect to address '" + address + "' with port " + port + "...");
            managerSocket = await SocketHelper.AcceptConnectionAsync(address, port).ConfigureAwait(false);
            if (managerSocket == null)
            {
                Debug.Log("ERROR: could not find any suitable IPv4 address for '" + address + "'! Quitting...");
                Application.Quit();
            }
            Debug.Log("Agent connection received!");

            connected = true;
        }

        public void Close()
        {
            managerSocket.Shutdown(SocketShutdown.Both);
            managerSocket.Close();
            
            connected = false;
        }

        public Command ProcessCommand()
        {
            if (!connected || !managerSocket.Connected || !(managerSocket.Available > 0))
                return Command.NONE;
            
            List<byte> messageContent = new();
            SocketHelper.ReceiveMessage(managerSocket, messageContent);

            string messageJson = new(Encoding.UTF8.GetChars(messageContent.ToArray()));

            Message message = JsonUtility.FromJson<Message>(messageJson);

            Command command = (Command) message.command;
            switch (command)
            {
                case Command.STATE_LOAD:
                    battleState = JsonUtility.FromJson<BattleState>(message.value);
                    break;
                
                case Command.P2_BOT:
                    isP2Bot = message.value.ToLower() == "true";
                    break;
            }

            return command;
        }

        public BattleState GetDesiredBattleState()
        {
            return battleState;
        }

        public void SetP2Saved(TrainingActor p2)
        {
            p2Saved = p2;
        }

        public void SetP2Bot(TrainingBattleAIActor p2)
        {
            p2Bot = p2;
        }

        public void SendBattleState(BattleState state)
        {
            if (!connected)
                return;
            
            string stateJson = JsonUtility.ToJson(state);
            byte[] stateBytes = Encoding.UTF8.GetBytes(stateJson);

            Task<int> sendTask = SocketHelper.SendWithSizeSuffixAsync(managerSocket, stateBytes);
            if (syncedComms)
                sendTask.Wait();
        }
    }

}