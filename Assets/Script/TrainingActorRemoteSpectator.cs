using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System;

namespace Footsies
{
    // This class is a wrapper on a training actor, which acts according to that actor
    // and sends the environment's state to a remote spectator, without receiving any
    // input from it
    public class TrainingActorRemoteSpectator : TrainingActor
    {
        public string address { get; private set; }
        public int port { get; private set; }
        public bool synced_comms { get; private set; }

        private Task<int> mostRecentAsyncStateRequest = null;

        private Socket trainingListener;
        private Socket trainingSocket;

        private TrainingActor actor;

        public TrainingActorRemoteSpectator(string address, int port, bool synced_comms)
        {
            this.address = address;
            this.port = port;
            this.synced_comms = synced_comms;
        }

        public TrainingActorRemoteSpectator(string address, int port, bool synced_comms, TrainingActor actor)
        {
            this.address = address;
            this.port = port;
            this.synced_comms = synced_comms;
            this.actor = actor;
        }

        public void SetTrainingActor(TrainingActor actor) {
            this.actor = actor;
        }

        public void Setup()
        {
            actor.Setup();

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
            actor.Close();

            trainingSocket.Shutdown(SocketShutdown.Both);
            trainingSocket.Close();
        }

        public void UpdateCurrentState(EnvironmentState state, bool battleOver)
        {
            actor.UpdateCurrentState(state, battleOver);

            string stateJson = JsonUtility.ToJson(state);
            byte[] stateBytes = Encoding.UTF8.GetBytes(stateJson);

            // Get size of the message and add it as a suffix
            byte[] sizeSuffix = BitConverter.GetBytes(stateBytes.Length);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(sizeSuffix);

            byte[] message = new byte[sizeSuffix.Length + stateBytes.Length];
            sizeSuffix.CopyTo(message, 0);
            stateBytes.CopyTo(message, sizeSuffix.Length);

            Debug.Log("Sending the game's current state...");
            if (synced_comms)
                trainingSocket.Send(message, SocketFlags.None);
            else
                mostRecentAsyncStateRequest = trainingSocket.SendAsync(message, SocketFlags.None);
            Debug.Log("Current state received by the spectator! (frame: " + state.globalFrame + ")");

            mostRecentAsyncStateRequest = null;
        }

        public int GetInput()
        {
            return actor.GetInput();
        }

        public void RequestNextInput()
        {
            actor.RequestNextInput();
        }

        public bool Ready()
        {
            return actor.Ready() && (mostRecentAsyncStateRequest == null || mostRecentAsyncStateRequest.IsCompleted);
        }
    }
}