using UnityEngine;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Footsies
{
    public class TrainingRemoteActor : TrainingActor
    {
        public string address { get; private set; }
        public int port { get; private set; }
        public bool syncedComms { get; private set; }
        public bool noState { get; private set; }

        private bool connected = false;
        private int input = 0;
        private Task inputRequest;
        private Task<int> stateRequest;

        private Socket trainingSocket;

        public TrainingRemoteActor(string address, int port, bool syncedComms, bool noState)
        {
            this.address = address;
            this.port = port;
            this.syncedComms = syncedComms;
            this.noState = noState;
        }

        public async Task Setup()
        {
            Debug.Log("Waiting for the agent to connect to address '" + address + "' with port " + port + "...");
            trainingSocket = await SocketHelper.AcceptConnectionAsync(address, port).ConfigureAwait(false);
            if (trainingSocket == null)
            {
                Debug.Log("ERROR: could not find any suitable IPv4 address for '" + address + "'! Quitting...");
                Application.Quit();
            }
            Debug.Log("Agent connection received!");

            connected = true;
        }

        public void Close()
        {
            trainingSocket.Shutdown(SocketShutdown.Both);
            trainingSocket.Close();

            connected = false;
        }

        public void UpdateCurrentState(EnvironmentState state, bool battleOver)
        {
            if (!noState)
            {
                string stateJson = JsonUtility.ToJson(state);
                byte[] stateBytes = Encoding.UTF8.GetBytes(stateJson);

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
            return connected && (inputRequest == null || inputRequest.IsCompleted);
        }

        private async Task RequestTrainingInput()
        {
            byte[] actionMessageContent = {0, 0, 0};
            ArraySegment<byte> actionMessage = new(actionMessageContent);

            // Corrected implementation of ReceiveAsync with a cancellation token... (https://github.com/mono/mono/issues/20902)
            var receiveTask = trainingSocket.ReceiveAsync(actionMessage, SocketFlags.None);
            int bytesReceived = await receiveTask.ConfigureAwait(false);

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