using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Footsies
{
    // This class is a wrapper on a training actor, which acts according to that actor
    // and sends the environment's state to a remote spectator, without receiving any
    // input from it
    public class TrainingActorRemoteSpectator : TrainingActor
    {
        public string address { get; private set; }
        public int port { get; private set; }
        public bool synced { get; private set; }

        private Task<int> mostRecentAsyncStateRequest = null;

        private Socket trainingListener;
        private Socket trainingSocket;

        private TrainingActor actor;

        public TrainingActorRemoteSpectator(string address, int port, bool synced)
        {
            this.address = address;
            this.port = port;
            this.synced = synced;
        }

        public TrainingActorRemoteSpectator(string address, int port, bool synced, TrainingActor actor)
        {
            this.address = address;
            this.port = port;
            this.synced = synced;
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
            Debug.Log("Sending the game's current state...");
            if (synced)
                trainingSocket.Send(Encoding.UTF8.GetBytes(stateJson), SocketFlags.None);
            else
                trainingSocket.SendAsync(Encoding.UTF8.GetBytes(stateJson), SocketFlags.None);
            Debug.Log("Current state received by the spectator! (frame: " + state.globalFrame + ")");
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
            return actor.Ready() && (!synced || (mostRecentAsyncStateRequest != null && mostRecentAsyncStateRequest.IsCompleted));
        }
    }
}