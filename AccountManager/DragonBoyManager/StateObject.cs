using System.Collections.Generic;
using System.Net.Sockets;

namespace DragonBoyManager
{
	public class StateObject
	{
		public const int BufferSize = 4096;

		public byte[] buffer = new byte[BufferSize];

		public Socket workSocket = null;

		public Account account = null;

		public List<byte> pendingBytes = new List<byte>();
	}
}
