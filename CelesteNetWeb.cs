using Celeste.Mod.CelesteNet;
using Celeste.Mod.CelesteNet.Client;
using Celeste.Mod.CelesteNet.Client.Components;
using Celeste.Mod.CelesteNet.DataTypes;
using MonoMod.RuntimeDetour;
using System.Reflection;

namespace Celeste.Mod.CelesteNetWeb {
	public class CelesteNetWeb : EverestModule {
		public static CelesteNetWeb Instance;
		private CelesteNetClientContext Context;
		private Server Server;

		private Hook HandleMessageHook;

		public CelesteNetWeb() {
			Instance = this;
		}

		private void CelesteNetContextCreated(CelesteNetClientContext context) {
			Context = context;
		}
		
		private void ShouldSendMessage(object sender, ShouldSendMessageEventArgs e) {
			Context.Chat.Send(e.Message);
		}

		// Set up any hooks, event handlers and your mod in general here.
		// Load runs before Celeste itself has initialized properly.
		public override void Load() {
			Server = new Server();
			Server.ShouldSendMessage += ShouldSendMessage;
			CelesteNet.Client.CelesteNetClientContext.OnCreate += CelesteNetContextCreated;
			HandleMessageHook = new Hook(
				typeof(CelesteNetChatComponent).GetMethod("Handle", BindingFlags.Public | BindingFlags.Instance),
				typeof(CelesteNetWeb).GetMethod("HandleMessage", BindingFlags.NonPublic | BindingFlags.Instance),
				this
			);
		}

		private delegate void orig_HandleMessage(CelesteNetChatComponent self, CelesteNetConnection con, DataChat msg);
		private void HandleMessage(orig_HandleMessage orig, CelesteNetChatComponent self, CelesteNetConnection connection, DataChat originalMessage) {
			orig(self, connection, originalMessage);
			CelesteNetMessage message = new CelesteNetMessage(originalMessage);
			if (Context == self.Context) {
				Server.HandleMessage(message);
			}
		}

		// Optional, initialize anything after Celeste has initialized itself properly.
		public override void Initialize() {
		}

		// Optional, do anything requiring either the Celeste or mod content here.
		public override void LoadContent(bool firstLoad) {
		}

		// Unload the entirety of your mod's content. Free up any native resources.
		public override void Unload() {
			Context = null;
			CelesteNet.Client.CelesteNetClientContext.OnCreate -= CelesteNetContextCreated;
			HandleMessageHook.Dispose();
			Server.Close();
			Server = null;
		}
	}
}