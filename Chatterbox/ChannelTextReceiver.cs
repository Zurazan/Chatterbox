using System.Collections.Generic;
using System.Linq;

namespace Chatterbox;

public class ChannelTextReceiver : ReceiverBase
{
	public Dictionary<StatusType, TriState> Status = new Dictionary<StatusType, TriState>();

	public override TriggerType ObjType => TriggerType.Text;

	public bool MatchAny { get; set; }

	public ChatType Channel { get; set; }

	public bool MeetsChannelCondition(ChatType channel)
	{
		if (!MatchAny)
		{
			if (Channel != ChatType.None)
			{
				return Channel.HasFlag(channel);
			}
			return false;
		}
		return true;
	}

	public bool MeetsStatusConditions()
	{
		if (Status == null || Status.Count == 0)
		{
			return true;
		}
		if (PlayerManager.LocalPlayer == null || PlayerManager.LocalPlayer.Character == null)
		{
			return false;
		}
		bool anyAllowMatched = false;
		foreach (KeyValuePair<StatusType, TriState> state in Status)
		{
			if (state.Value != TriState.Ignored)
			{
				bool hasStatus = ((state.Key == StatusType.InCombat) ? PlayerManager.LocalPlayer.InCombat : PlayerManager.LocalPlayer.Character.HasOnlineStatus((OnlineStatusTypeRaw)state.Key));
				if ((state.Value == TriState.Disallow) & hasStatus)
				{
					return false;
				}
				if ((state.Value == TriState.Allow) & hasStatus)
				{
					anyAllowMatched = true;
				}
			}
		}
		return !Status.Values.Any((TriState v) => v == TriState.Allow) | anyAllowMatched;
	}
}
