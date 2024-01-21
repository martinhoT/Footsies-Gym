using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Footsies
{
    // Helper methods for socket communication, to avoid code duplication
    public class SocketHelper
    {

        public static void SendWithSizeSuffix(Socket socket, byte[] message)
        {
            byte[] messageWithSuffix = AddSizeSuffix(message);
            socket.Send(messageWithSuffix, SocketFlags.None);
        }

        public static Task<int> SendWithSizeSuffixAsync(Socket socket, byte[] message)
        {
            byte[] messageWithSuffix = AddSizeSuffix(message);
            return socket.SendAsync(messageWithSuffix, SocketFlags.None);
        }

        public static Task<int> SendWithSizeSuffixAsync(Socket socket, byte[] message, CancellationToken cancellationToken)
        {
            byte[] messageWithSuffix = AddSizeSuffix(message);
            return socket.SendAsync(messageWithSuffix, SocketFlags.None, cancellationToken).AsTask();
        }

        public static void ReceiveMessage(Socket socket, List<byte> message)
        {
            byte[] sizeSuffixBuffer = new byte[4];

            // Determine message length
            ReceiveBytes(socket, sizeSuffixBuffer, 4);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(sizeSuffixBuffer, 0, 4);
            int sizeSuffix = BitConverter.ToInt32(sizeSuffixBuffer, 0);

            // Get actual message
            byte[] messageBuffer = new byte[sizeSuffix];
            ReceiveBytes(socket, messageBuffer, sizeSuffix);
            message.AddRange(messageBuffer);
        }

        private static byte[] AddSizeSuffix(byte[] message)
        {
            // Get size of the message and add it as a suffix
            byte[] sizeSuffix = BitConverter.GetBytes(message.Length);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(sizeSuffix);

            byte[] messageWithSuffix = new byte[sizeSuffix.Length + message.Length];
            sizeSuffix.CopyTo(messageWithSuffix, 0);
            message.CopyTo(messageWithSuffix, sizeSuffix.Length);

            return messageWithSuffix;
        }

        private static bool ReceiveBytes(Socket socket, byte[] content, int nBytes)
        {
            int bytesReceived = 0;
            while (bytesReceived < nBytes)
            {
                int justReceived = socket.Receive(content, 0, nBytes - bytesReceived, SocketFlags.None);
                if (justReceived == 0)
                    break;
                
                bytesReceived += justReceived;
            }

            return bytesReceived > 0;
        }
    }
}
