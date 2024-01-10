using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;

namespace Footsies
{
    // Remote controller that allows modifying the game, such as transitioning to a specific state
    public class TrainingRemoteControl
    {
        // Available commands:
        // - Reset: reset the battle to the beginning
        // - StateSave: request a copy of the current state
        // - StateLoad: request the game to load a specific state
        public enum Command
        {
            NONE = 0,
            RESET = 1,
            STATE_SAVE = 2,
            STATE_LOAD = 3,
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

        private Socket managerListener;
        private Socket managerSocket;

        private bool on;

        public TrainingRemoteControl(string address, int port, bool syncedComms)
        {
            this.address = address;
            this.port = port;
            this.syncedComms = syncedComms;
        }

        public void Setup()
        {
            // Setup Socket server to listen for the agent's actions
            IPAddress hostIPAddress = null;
            foreach (var hostAddress in Dns.GetHostAddresses(address))
            {
                // Only accept IPv4 addresses
                if (hostAddress.AddressFamily == AddressFamily.InterNetwork)
                {
                    hostIPAddress = hostAddress;
                    break; // return the first one found
                }
            }
            if (hostIPAddress == null)
            {
                Debug.Log("ERROR: could not find any suitable IPv4 address for '" + address + "'! Quitting...");
                Application.Quit();
            }
            IPEndPoint ipEndPoint = new IPEndPoint(hostIPAddress, port);
            managerListener = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            managerListener.Bind(ipEndPoint);
            managerListener.Listen(1); // maximum queue length of 1, there should only be 1 remote manager
            Debug.Log("Waiting for the agent to connect to address '" + hostIPAddress.ToString() + "'...");
            managerSocket = managerListener.Accept();
            Debug.Log("Agent connection received!");
            managerListener.Close();

            on = true;
        }

        public void Close()
        {
            managerSocket.Shutdown(SocketShutdown.Both);
            managerSocket.Close();
            
            on = false;
        }

        public Command ProcessCommand()
        {
            if (!on || !managerSocket.Connected || !(managerSocket.Available > 0))
                return Command.NONE;
            
            List<byte> messageContent = new();
            SocketHelper.ReceiveMessage(managerSocket, messageContent);

            string messageJson = new(Encoding.UTF8.GetChars(messageContent.ToArray()));

            Message message = JsonUtility.FromJson<Message>(messageJson);

            Command command = (Command) message.command;
            if (command == Command.STATE_LOAD)
            {
                battleState = JsonUtility.FromJson<BattleState>(message.value);
            }

            return command;
        }

        public BattleState GetDesiredBattleState()
        {
            return battleState;
        }

        public void SendBattleState(BattleState state)
        {
            if (!on)
                return;
            
            string stateJson = JsonUtility.ToJson(state);
            byte[] stateBytes = Encoding.UTF8.GetBytes(stateJson);

            if (syncedComms)
                SocketHelper.SendWithSizeSuffix(managerSocket, stateBytes);
            else
                SocketHelper.SendWithSizeSuffixAsync(managerSocket, stateBytes);
        }
    }

}