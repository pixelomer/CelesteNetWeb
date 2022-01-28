using System.Net;
using System;
using System.Collections.Generic;
using System.Text;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Celeste.Mod.CelesteNetWeb {
	public class ShouldSendMessageEventArgs : EventArgs {
		public string Message { get; set; }
	}

	public class DidHandleMessageEventArgs : EventArgs {
		public CelesteNetMessage Message { get; set; }
	}

	public static class MessageType {
		// Server --> Client
		public const byte ReceivedMessage = 0;
		public const byte Error = 2;
		public const byte Hello = 3;
		public const byte MessageHistory = 4;

		// Client --> Server
		public const byte SendMessage = 1;
	}

	public class Message {
		public byte Type;
		public string[] Contents;

		public Message(byte type) {
			Type = type;
			Contents = new string[0];
		}

		public Message(byte type, string[] contents) {
			Type = type;
			Contents = contents;
		}

		public static Message Parse(byte[] data) {
			int seek = 0;
			byte readByte() {
				return data[seek++];
			}
			ushort readShort() {
				ushort value = 0;
				value |= (ushort)(readByte() << 8);
				value |= readByte();
				return value;
			}
			string readString() {
				ushort byteLength = readShort();
				string str = Encoding.UTF8.GetString(data, seek, byteLength);
				seek += byteLength;
				return str;
			}
			byte type = readByte();
			ushort stringCount = readShort();
			string[] contents = new string[stringCount];
			for (ushort i=0; i<stringCount; i++) {
				contents[i] = readString();
			}
			Message message = new Message(type, contents);
			return message;
		}

		public byte[] Encode() {
			// Determine final message size
			int messageLength = (
				1   // Type byte
				+ 2 // Content length (number of strings)
			);
			if (Contents.Length > 0xFFFF) {
				throw new Exception("Cannot encode message. A message cannot contain more than 65535 strings.");
			}
			foreach (string str in Contents) {
				int stringByteLength = Encoding.UTF8.GetByteCount(str);
				if (stringByteLength > 0xFFFF) {
					throw new Exception("Strings in a message cannot exceed 65535 bytes.");
				}
				messageLength += 2 + stringByteLength;
			}
			
			// Actually encode the message
			int seek = 0;
			byte[] data = new byte[messageLength];
			void writeByte(byte value) {
				data[seek++] = value;
			}
			void writeShort(ushort value) {
				writeByte((byte)(value >> 8));
				writeByte((byte)(value & 0xFF));
			}
			void writeString(string value) {
				byte[] stringBytes = Encoding.UTF8.GetBytes(value);
				writeShort((ushort)stringBytes.Length);
				stringBytes.CopyTo(data, seek);
				seek += stringBytes.Length;
			}
			writeByte(Type);
			writeShort((ushort)Contents.Length);
			foreach (string str in Contents) {
				writeString(str);
			}
			return data;
		}
	}

	public class Server {
		private class ServerBehavior : WebSocketBehavior {
			private void HandleMessage(object sender, DidHandleMessageEventArgs e) {
				byte[] data = Server.BytesForMessages(MessageType.ReceivedMessage, new CelesteNetMessage[] { e.Message });
				Send(data);
			}

			protected override void OnMessage(MessageEventArgs e) {
				Message incoming = Message.Parse(e.RawData);
				if (incoming.Type == MessageType.SendMessage) {
					ShouldSendMessageEventArgs eventArgs = new ShouldSendMessageEventArgs();
					eventArgs.Message = incoming.Contents[0];
					Server.Instance.ShouldSendMessage(Server.Instance, eventArgs);
				}
			}

			protected override void OnOpen() {
				Server.Instance.DidHandleMessage += HandleMessage;
				Message hello = new Message(MessageType.Hello, new string[] { "Hello!" });
				Send(hello.Encode());
			}

			protected override void OnClose(CloseEventArgs e) {
				Server.Instance.DidHandleMessage -= HandleMessage;
			}
		}

		private HttpServer HttpServer;
		private HashSet<WebSocket> ConnectedSockets;
		private static byte[] HomepageHTML;
		private static byte[] NotFoundHTML;
		public short Port { get; private set; }
		public event EventHandler<ShouldSendMessageEventArgs> ShouldSendMessage;
		public event EventHandler<DidHandleMessageEventArgs> DidHandleMessage;
		public static Server Instance;
		
		static Server() {
			HomepageHTML = Encoding.UTF8.GetBytes(
@"<!DOCTYPE html>
<html>
	<body>
		<h1>CelesteNet Web</h1>
		<p>CelesteNet Web runs here. For details, see the <a href=https://github.com/pixelomer/CelesteNetWeb>CelesteNet Web Github page</a>.</p>
	</body>
</html>"
			);
			NotFoundHTML = Encoding.UTF8.GetBytes(
@"<!DOCTYPE html>
<html>
	<body>
		<h2>404 Not Found</h2>
		<p><a href=/>CelesteNet Web homepage</a></p>
	</body>
</html>"
			);
		}

		public Server(short port = 4422) {
			if (Instance != null) {
				throw new Exception();
			}
			Instance = this;
			Port = port;
			ConnectedSockets = new HashSet<WebSocket>();
			HttpServer = new HttpServer(IPAddress.Loopback, port);
			HttpServer.OnGet += DidReceiveHTTPGet;
			HttpServer.AddWebSocketService<ServerBehavior>("/");
			HttpServer.Start();
		}

		private void DidReceiveHTTPGet(object sender, HttpRequestEventArgs e) {
			string path = e.Request.Url.AbsolutePath;
			var response = e.Response;
			try {
				response.ContentEncoding = Encoding.UTF8;
				response.ContentType = "text/html";
				byte[] data;
				if (path == "/") {
					response.StatusCode = 200;
					data = HomepageHTML;
				}
				else {
					response.StatusCode = 404;
					data = NotFoundHTML;
				}
				response.ContentLength64 = data.Length;
				response.Close(data, true);
			}
			catch (Exception ex) {
				Console.WriteLine($"An exception was thrown while handling a request for path {path}\n{ex}");
			}
		}

		static public byte[] BytesForMessages(byte type, CelesteNetMessage[] messages) {
			string[] contents = new string[messages.Length * 3];
			for (int i=0; i<messages.Length; i++) {
				CelesteNetMessage message = messages[i];
				contents[i * 3 + 0] = message.DisplayName ?? "";
				contents[i * 3 + 1] = message.Text ?? "";
				contents[i * 3 + 2] = ((int)Math.Floor(message.Date.GetUnixEpoch())).ToString();
			}
			Message socketMessage = new Message(type, contents);
			return socketMessage.Encode();
		}

		public void HandleMessage(CelesteNetMessage message) {
			DidHandleMessageEventArgs eventArgs = new DidHandleMessageEventArgs();
			eventArgs.Message = message;
			DidHandleMessage(this, eventArgs);
		}

		public void Close() {
			HttpServer.Stop();
		}
	}

	public static class Helpers {
		public static double GetUnixEpoch(this DateTime dateTime)
		{
			var unixTime = dateTime.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			return unixTime.TotalSeconds;
		}
	}
}