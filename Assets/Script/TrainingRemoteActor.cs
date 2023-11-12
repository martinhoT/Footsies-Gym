using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Footsies
{
    public class TrainingRemoteActor : TrainingActor
    {
        public string address { get; private set; }
        public int port { get; private set; }
        public bool synced { get; private set; }
        public bool noState { get; private set; }

        // Whether we already requested for the agent's input
        public bool inputRequested { get; private set; } = false;
        // Whether the agent is ready to receive the environment state, usually after input from it has been received. It's true on environment reset
        public bool inputReady { get; private set; } = true;

        private int input = 0;

        private Socket trainingListener;
        private Socket trainingSocket;

        public TrainingRemoteActor(string address, int port, bool synced, bool noState)
        {
            this.address = address;
            this.port = port;
            this.synced = synced;
            this.noState = noState;
        }

        public void Setup()
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
            trainingSocket = trainingListener.Accept();
            Debug.Log("Agent connection received!");
            trainingListener.Close();
        }

        public void Close()
        {
            trainingSocket.Shutdown(SocketShutdown.Both);
            trainingSocket.Close();
        }

        public void UpdateCurrentState(EnvironmentState state, bool battleOver)
        {
            // We will only send the current state if the remote actor is ready to receive it, which we assume it is if it has already acted
            if (!noState && inputReady)
            {
                string stateJson = JsonUtility.ToJson(state);
                Debug.Log("Sending the game's current state...");
                if (synced)
                    trainingSocket.Send(Encoding.UTF8.GetBytes(stateJson), SocketFlags.None);
                else
                    trainingSocket.SendAsync(Encoding.UTF8.GetBytes(stateJson), SocketFlags.None);
                Debug.Log("Current state received by the agent! (frame: " + state.globalFrame + ")");
            }

            // If we haven't received the terminal environment state, then we will be set to act again, otherwise we just wait to receive the next non-terminal state
            if (!battleOver)
            {
                inputReady = false;
            }
        }

        public int GetInput()
        {
            return input;
        }

        // no-op if a request is still unfulfilled
        public void RequestNextInput()
        {
            if (!inputReady && !inputRequested)
            {
                inputRequested = true;
                if (synced)
                    ReceiveTrainingInput();
                else
                    ReceiveTrainingInputAsync();
            }
            else {
                Debug.Log("ERROR: training input request could not be performed!");
            }
        }

        public bool Ready()
        {
            return !synced || inputReady;
        }

        private void ReceiveTrainingInput()
        {
            byte[] actionMessageContent = {0, 0, 0};
            ArraySegment<byte> actionMessage = new ArraySegment<byte>(actionMessageContent);

            Debug.Log("Waiting for the agent's action...");
            int bytesReceived = trainingSocket.Receive(actionMessage, SocketFlags.None);
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
            
            input = 0;
            input |= actionMessageContent[0] != 0 ? (int)InputDefine.Left : 0;
            input |= actionMessageContent[1] != 0 ? (int)InputDefine.Right : 0;
            input |= actionMessageContent[2] != 0 ? (int)InputDefine.Attack : 0;

            inputRequested = false;
            inputReady = true;
        }

        private async void ReceiveTrainingInputAsync()
        {
            byte[] actionMessageContent = {0, 0, 0};
            ArraySegment<byte> actionMessage = new ArraySegment<byte>(actionMessageContent);

            Debug.Log("Waiting for the agent's action...");
            int bytesReceived = await trainingSocket.ReceiveAsync(actionMessage, SocketFlags.None);
            Debug.Log("Agent action received ASYNC! (" + (int)actionMessageContent[0] + ", " + (int)actionMessageContent[1] + ", " + (int)actionMessageContent[2] + ")");
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
            
            input = 0;
            input |= actionMessageContent[0] != 0 ? (int)InputDefine.Left : 0;
            input |= actionMessageContent[1] != 0 ? (int)InputDefine.Right : 0;
            input |= actionMessageContent[2] != 0 ? (int)InputDefine.Attack : 0;

            inputRequested = false;
            inputReady = true;
        }
    }
}