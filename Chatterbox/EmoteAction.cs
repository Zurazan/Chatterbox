using System.Collections.Generic;

namespace Chatterbox;

public class EmoteAction : ActionBase
{
	public override TriggerType ObjType => TriggerType.Emote;

	public bool MatchAny { get; set; }

	public List<ushort> IDs { get; set; } = new List<ushort>();
}
