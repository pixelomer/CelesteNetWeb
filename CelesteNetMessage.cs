using Celeste.Mod.CelesteNet.DataTypes;
using System;

namespace Celeste.Mod.CelesteNetWeb {
	public struct CelesteNetMessage {
		public string Text;
		public string DisplayName;
		public DateTime Date;

		public CelesteNetMessage(DataChat dataChat) {
			this.Text = dataChat.Text;
			this.DisplayName = dataChat.Player.DisplayName;
			this.Date = dataChat.Date;
		}
	}
}