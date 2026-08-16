using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;

namespace Chatterbox;

public class EmoteTargetReceiver : ReceiverBase
{
	public Dictionary<StatusType, TriState> Status = new Dictionary<StatusType, TriState>();

	public override TriggerType ObjType => TriggerType.Emote;

	public PlayerType Type { get; set; } = PlayerType.Self;

	public PlayerCondition Condition { get; set; }

	public bool RequireAllConditions { get; set; }

	public GenderCondition Gender { get; set; }

	public RaceCondition Race { get; set; }

	public List<string> Names { get; set; } = new List<string>();

	public bool PlayerNameMatches(string playerName)
	{
		if (Names.Count != 0)
		{
			return Names.Any((string x) => playerName.Equals(x, StringComparison.OrdinalIgnoreCase));
		}
		return false;
	}

	public bool MeetsRelationConditions(EntityInfo? entity)
	{
		if (Condition == PlayerCondition.None)
		{
			return true;
		}
		if (entity == null || entity.Character == null)
		{
			return false;
		}
		if (entity.IsBlocked)
		{
			return false;
		}
		if (Type == PlayerType.Self || (Type == PlayerType.All && entity.IsLocalPlayer))
		{
			return true;
		}
		(PlayerCondition, Func<EntityInfo, bool>)[] active = new(PlayerCondition, Func<EntityInfo, bool>)[3]
		{
			(PlayerCondition.Friend, (EntityInfo e) => e.IsFriend),
			(PlayerCondition.Party, (EntityInfo e) => e.IsInParty),
			(PlayerCondition.MareSynced, (EntityInfo e) => e.IsMareSynced)
		}.Where(((PlayerCondition Flag, Func<EntityInfo, bool> Check) c) => Condition.HasFlag(c.Flag)).ToArray();
		return RequireAllConditions
			? active.All(((PlayerCondition Flag, Func<EntityInfo, bool> Check) c) => c.Check(entity))
			: active.Any(((PlayerCondition Flag, Func<EntityInfo, bool> Check) c) => c.Check(entity));
	}

	public bool MeetsGenderCondition(EntityInfo? entity)
	{
		if (Gender == GenderCondition.Any)
		{
			return true;
		}
		if (entity == null || entity.Character == null)
		{
			return false;
		}
		if (Type == PlayerType.Self || (Type == PlayerType.All && entity.IsLocalPlayer))
		{
			return true;
		}
		return entity.Gender != Chatterbox.Gender.Male
			? Gender.HasFlag(GenderCondition.Female)
			: Gender.HasFlag(GenderCondition.Male);
	}

	public bool MeetsRaceCondition(EntityInfo? entity)
	{
		if (Race == RaceCondition.Any)
		{
			return true;
		}
		if (entity == null || entity.Character == null)
		{
			return false;
		}
		if (Type == PlayerType.Self || (Type == PlayerType.All && entity.IsLocalPlayer))
		{
			return true;
		}
		return new(RaceCondition, Func<EntityInfo, bool>)[9]
			{
				(RaceCondition.Midlander, (EntityInfo e) => e.Race == Chatterbox.Race.Midlander),
				(RaceCondition.Highlander, (EntityInfo e) => e.Race == Chatterbox.Race.Highlander),
				(RaceCondition.Elezen, (EntityInfo e) => e.Race == Chatterbox.Race.Elezen),
				(RaceCondition.Miqote, (EntityInfo e) => e.Race == Chatterbox.Race.Miqote),
				(RaceCondition.Roegadyn, (EntityInfo e) => e.Race == Chatterbox.Race.Roegadyn),
				(RaceCondition.Lalafell, (EntityInfo e) => e.Race == Chatterbox.Race.Lalafell),
				(RaceCondition.AuRa, (EntityInfo e) => e.Race == Chatterbox.Race.AuRa),
				(RaceCondition.Hrothgar, (EntityInfo e) => e.Race == Chatterbox.Race.Hrothgar),
				(RaceCondition.Viera, (EntityInfo e) => e.Race == Chatterbox.Race.Viera)
			}.Where(((RaceCondition Flag, Func<EntityInfo, bool> Check) c) => Race.HasFlag(c.Flag)).ToArray().Any(((RaceCondition Flag, Func<EntityInfo, bool> Check) c) => c.Check(entity));
	}

	public bool MeetsStatusConditions(EntityInfo? entity)
	{
		if (Status == null || Status.Count == 0)
		{
			return true;
		}
		if (entity == null || entity.Character == null)
		{
			return false;
		}
		bool anyAllowMatched = false;
		foreach (KeyValuePair<StatusType, TriState> state in Status)
		{
			if (state.Value != TriState.Ignored)
			{
				bool hasStatus = ((state.Key == StatusType.InCombat) ? entity.InCombat : entity.Character.HasOnlineStatus((OnlineStatusTypeRaw)state.Key));
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
