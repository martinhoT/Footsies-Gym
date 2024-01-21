using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Drawing.Printing;

namespace Footsies
{
    public class TrainingRemoteActor : TrainingActor
    {
        public string address { get; private set; }
        public int port { get; private set; }
        public bool syncedComms { get; private set; }
        public bool noState { get; private set; }

        // Whether we already requested for the agent's input
        public bool inputRequested { get; private set; } = false;
        // Whether the agent is ready to receive the environment state, usually after input from it has been received. It's true on environment reset
        public bool inputReady { get; private set; } = true;


        private int input = 0;
        private Task inputRequest;
        private Task<int> stateRequest;

        private Socket trainingListener;
        private Socket trainingSocket;

        public TrainingRemoteActor(string address, int port, bool syncedComms, bool noState)
        {
            this.address = address;
            this.port = port;
            this.syncedComms = syncedComms;
            this.noState = noState;
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
            IPEndPoint ipEndPoint = new(hostIPAddress, port);
            trainingListener = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            trainingListener.Bind(ipEndPoint);
            trainingListener.Listen(1); // maximum queue length of 1, there is only 1 agent
            Debug.Log("Waiting for the agent to connect to address '" + hostIPAddress.ToString() + "'...");
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
            if (!noState)
            {
                string stateJson = JsonUtility.ToJson(state);
                byte[] stateBytes = Encoding.UTF8.GetBytes(stateJson);

                Debug.Log("Sending the game's current state (frame: " + state.globalFrame + ")");
                stateRequest = SocketHelper.SendWithSizeSuffixAsync(trainingSocket, stateBytes);
                if (syncedComms)
                    stateRequest.Wait();
            }
        }

        public int GetInput()
        {
            return input;
        }

        // no-op if a request is still unfulfilled
        public void RequestNextInput()
        {
            // recycle the same input request if the previous one hasn't completed yet
            if (inputRequest != null && !inputRequest.IsCompleted)
            {
                Debug.Log("Requested input from agent, but a request already exists, ignoring");
                return;
            }

            inputRequest = RequestTrainingInput();
            if (syncedComms)
            {
                inputRequest.Wait();
            }
        }

        public bool Ready()
        {
            return inputRequest == null || inputRequest.IsCompleted;
        }

        private async Task RequestTrainingInput()
        {
            byte[] actionMessageContent = {0, 0, 0};
            ArraySegment<byte> actionMessage = new(actionMessageContent);

            Debug.Log("Waiting for the agent's action...");
            // Corrected implementation of ReceiveAsync with a cancellation token... (https://github.com/mono/mono/issues/20902)
            var receiveTask = trainingSocket.ReceiveAsync(actionMessage, SocketFlags.None);
            int bytesReceived = await receiveTask.ConfigureAwait(false);
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
        }
    }
}