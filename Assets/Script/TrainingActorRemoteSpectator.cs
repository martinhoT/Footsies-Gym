using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Footsies
{
    // This class is a wrapper on a training actor, which acts according to that actor
    // and sends the environment's state to a remote spectator, without receiving any
    // input from it
    public class TrainingActorRemoteSpectator : TrainingActor
    {
        public string address { get; private set; }
        public int port { get; private set; }
        public bool syncedComms { get; private set; }

        private Task<int> stateRequest = null;
        private bool connected = false;

        private Socket trainingListener;
        private Socket trainingSocket;

        private TrainingActor actor;

        public TrainingActorRemoteSpectator(string address, int port, bool syncedComms)
        {
            this.address = address;
            this.port = port;
            this.syncedComms = syncedComms;
        }

        public TrainingActorRemoteSpectator(string address, int port, bool syncedComms, TrainingActor actor)
        {
            this.address = address;
            this.port = port;
            this.syncedComms = syncedComms;
            this.actor = actor;
        }

        public void SetTrainingActor(TrainingActor actor) {
            this.actor = actor;
        }

        public async Task Setup()
        {
            await actor.Setup().ConfigureAwait(false);

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
            actor.Close();

            trainingSocket.Shutdown(SocketShutdown.Both);
            trainingSocket.Close();

            connected = false;
        }

        public void UpdateCurrentState(EnvironmentState state, bool battleOver)
        {
            actor.UpdateCurrentState(state, battleOver);

            string stateJson = JsonUtility.ToJson(state);
            byte[] stateBytes = Encoding.UTF8.GetBytes(stateJson);

            Debug.Log("Sending the game's current state (frame: " + state.globalFrame + ")");
            stateRequest = SocketHelper.SendWithSizeSuffixAsync(trainingSocket, stateBytes);
            if (syncedComms)
                stateRequest.Wait();
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
            return connected && actor.Ready() && (stateRequest == null || stateRequest.IsCompleted);
        }
    }
}