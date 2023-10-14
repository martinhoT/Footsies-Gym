using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Footsies
{
    public class TrainingManager
    {
        // If isTraining is false, the TrainingManager methods will not do anything when called
        public bool isTraining { get; private set; } = false;
        // Whether to wait for agent inputs when training or just keep advancing
        public bool isTrainingSynced { get; private set; } = false;

        // Whether we already requested for the agent's input
        private bool agentInputRequested = false;
        // Whether the agent is ready to receive the environment state, usually after input from it has been received. It's true on environment reset
        private bool agentInputReady = true;
        public int p1TrainingInput { get; private set; } = 0;

        Socket trainingListener;
        Socket p1TrainingSocket;
        private bool isCommunicationOn = false;

        public TrainingManager(bool enabled, bool synced) {
            isTraining = enabled;
            isTrainingSynced = synced;
        }

        public bool StartCommunication(string address, int port)
        {
            if (!isTraining) { return false; }

            if (!isCommunicationOn)
            {
                // Setup Socket server to listen for the agent's actions
                IPAddress localhostAddress = null;
                foreach (var hostAddress in Dns.GetHostAddresses(address))
                {
                    // Only accept IPv4 addresses
                    if (hostAddress.AddressFamily == AddressFamily.InterNetwork)
                    {
                        localhostAddress = hostAddress;
                        break; // return the first one found
                    }
                }
                if (localhostAddress == null)
                {
                    Debug.Log("ERROR: could not find any suitable IPv4 address for 'localhost'! Quitting...");
                    Application.Quit();
                }
                IPEndPoint ipEndPoint = new IPEndPoint(localhostAddress, port);
                trainingListener = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                trainingListener.Bind(ipEndPoint);
                trainingListener.Listen(1); // maximum queue length of 1, there is only 1 agent
                Debug.Log("Waiting for the agent to connect to address '" + localhostAddress.ToString() + "'...");
                p1TrainingSocket = trainingListener.Accept();
                Debug.Log("Agent connection received!");
                trainingListener.Close();

                isCommunicationOn = true;
                return true;
            }
            
            return false;
        }

        public bool CloseCommunication()
        {
            if (!isTraining) { return false; }

            if (isCommunicationOn)
            {
                p1TrainingSocket.Shutdown(SocketShutdown.Both);
                p1TrainingSocket.Close();

                return true;
            }

            return false;
        }

        public void Step(EnvironmentState state, bool battleOver)
        {
            if (!isTraining) { return; }

            // Don't send the environment state until the agent is ready to receive it (relevant when training is async)
            if (agentInputReady)
            {
                SendState(state);

                // Request another action from the training agent, as long as the environment hasn't terminated and the previous input request has been dealt with
                if (!battleOver)
                {
                    agentInputReady = false;
                    RequestP1TrainingInput();
                }
            }
        }

        private void SendState(EnvironmentState state)
        {
            string stateJson = JsonUtility.ToJson(state);
            Debug.Log("Sending the game's current state...");
            p1TrainingSocket.SendAsync(Encoding.UTF8.GetBytes(stateJson), SocketFlags.None);
            Debug.Log("Current state received by the agent! (frame: " + state.globalFrame + ")");
        }

        // no-op if a request is still unfulfilled
        private void RequestP1TrainingInput()
        {
            if (!agentInputReady && !agentInputRequested)
            {
                agentInputRequested = true;
                ReceiveP1TrainingInput();
            }
            else {
                Debug.Log("ERROR: P1 training input request could not be performed!");
            }
        }

        private async void ReceiveP1TrainingInput()
        {
            byte[] actionMessageContent = {0, 0, 0};
            ArraySegment<byte> actionMessage = new ArraySegment<byte>(actionMessageContent);

            Debug.Log("Waiting for the agent's action...");
            int bytesReceived = await p1TrainingSocket.ReceiveAsync(actionMessage, SocketFlags.None);
            Debug.Log("Agent action received! (" + (int)actionMessageContent[0] + ", " + (int)actionMessageContent[1] + ", " + (int)actionMessageContent[2] + ")");
            // EOF has been reached, communication has likely been stopped on the agent's side
            if (bytesReceived == 0)
            {
                Debug.Log("Training agent has ceased communication, quitting...");
                Application.Quit();
            }
            else if (bytesReceived != 3)
            {
                Debug.Log("ERROR: abnormal number of bytes received from agent's action message (sent " + bytesReceived + ", expected 3)");
            }
            
            p1TrainingInput = 0;
            p1TrainingInput |= actionMessageContent[0] != 0 ? (int)InputDefine.Left : 0;
            p1TrainingInput |= actionMessageContent[1] != 0 ? (int)InputDefine.Right : 0;
            p1TrainingInput |= actionMessageContent[2] != 0 ? (int)InputDefine.Attack : 0;

            agentInputRequested = false;
            agentInputReady = true;
        }

        public bool Ready() {
            if (!isTraining) { return true; }

            if (isTrainingSynced && !agentInputReady) { return false; }

            return true;
        }
    }
}