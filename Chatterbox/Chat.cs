using System;
using System.Linq;
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Chatterbox;

public class Chat
{
	private Plugin plugin;

	public event Action<string, string, EntityInfo?, ChatType, Trigger>? OnChat;

	internal Chat(Plugin plugin)
	{
		this.plugin = plugin;
	}

	internal void OnChatMessage(IChatMessage m)
	{
		OnChatMessage(m.LogKind, m.Timestamp, m.Sender, m.Message, m.IsHandled);
	}

	internal void OnChatMessage(XivChatType type, int timestamp, SeString sender, SeString message, bool isHandled)
	{
		ChatType receiverChannel = ConvertXIVChatTypeToChatType(type);
		EntityInfo localPlayer = PlayerManager.LocalPlayer;
		if (!Plugin.Config.Enabled || localPlayer == null || receiverChannel == ChatType.None)
		{
			return;
		}
		string text;
		if ((int)type == 12)
		{
			text = localPlayer.Name;
		}
		else
		{
			bool flag = (int)type - 14 <= 1 || (int)type == 32;
			text = ((flag && sender.TextValue.Length > 0) ? sender.TextValue.Substring(1) : sender.TextValue);
		}
		string messageSender = text;
		messageSender = ((messageSender.Length <= 1) ? messageSender : (char.IsLetter(messageSender[0]) ? messageSender : messageSender.Substring(1)));
		string messageText = message.TextValue;
		EntityInfo instigatorEntity = null;
		foreach (Trigger trigger in Plugin.Config.Triggers)
		{
			if (!trigger.Enabled || trigger.Type != TriggerType.Text)
			{
				continue;
			}
			Instigator? instigator = trigger.Instigator;
			if (instigator != null && instigator.Type == PlayerType.None)
			{
				continue;
			}
			Instigator? instigator2 = trigger.Instigator;
			if ((instigator2 != null && instigator2.Type == PlayerType.Ignore) || !(trigger.ReceivedAction is TextAction triggerAction) || !triggerAction.MessageContainsInputs(messageText))
			{
				continue;
			}
			Plugin.Log.Info($"Text trigger '{trigger.Name}' received matching input in {receiverChannel}.", Array.Empty<object>());
			if (!(trigger.Receiver is ChannelTextReceiver triggerReceiver) || !triggerReceiver.MeetsChannelCondition(receiverChannel) || !triggerReceiver.MeetsStatusConditions())
			{
				Plugin.Log.Info($"Text trigger '{trigger.Name}' matched input but was blocked by channel or status conditions. Received channel: {receiverChannel}.", Array.Empty<object>());
				continue;
			}
			if (trigger.Instigator != null)
			{
				instigatorEntity = PlayerManager.NearbyPlayers.FirstOrDefault((EntityInfo x) => x.Character != null && messageSender.StartsWith(((IGameObject)x.Character).Name.TextValue, StringComparison.OrdinalIgnoreCase));
				if ((trigger.Instigator.RequireNearby && instigatorEntity == null) || (trigger.Instigator.Type != PlayerType.All && ((trigger.Instigator.Type == PlayerType.Self && !messageSender.StartsWith(localPlayer.Name, StringComparison.OrdinalIgnoreCase)) || (trigger.Instigator.Type == PlayerType.Others && messageSender.StartsWith(localPlayer.Name, StringComparison.OrdinalIgnoreCase)) || (trigger.Instigator.Type == PlayerType.Player && !trigger.Instigator.PlayerNameMatches(messageSender)) || (trigger.Instigator.Type == PlayerType.Target && (instigatorEntity == null || !localPlayer.IsTargetValid || localPlayer.Target.Address != ((IGameObject)instigatorEntity.Character).Address)) || (trigger.Instigator.Type == PlayerType.Targeter && (instigatorEntity == null || !instigatorEntity.IsTargetValid || instigatorEntity.Target.Address != ((IGameObject)localPlayer.Character).Address)))) || (trigger.Instigator.Type != PlayerType.Self && trigger.Instigator.BlacklistNameMatches(messageSender)) || (instigatorEntity != null && ((trigger.Instigator.Type != PlayerType.Self && (!trigger.Instigator.MeetsRelationConditions(instigatorEntity) || !trigger.Instigator.MeetsGenderCondition(instigatorEntity) || !trigger.Instigator.MeetsRaceCondition(instigatorEntity))) || !trigger.Instigator.MeetsStatusConditions(instigatorEntity))))
				{
					Plugin.Log.Info($"Text trigger '{trigger.Name}' matched input but was blocked by instigator conditions. Nearby player resolved: {instigatorEntity != null}.", Array.Empty<object>());
					continue;
				}
			}
			if (trigger.ReactionOptions == null || !trigger.ReactionOptions.PassthroughRestrictions || (localPlayer.CanReactionInterruptCurrentState(trigger.ReactionOptions) && (!trigger.ReactionOptions.RestrictRange || instigatorEntity == null || instigatorEntity.IsLocalPlayer || instigatorEntity.IsWithinReactionAngleAndDistanceToLocalPlayer(trigger.ReactionOptions)) && trigger.ReactionOptions.MeetsTerritoryConditions()))
			{
				OnChat?.Invoke(messageSender, messageText, instigatorEntity, receiverChannel, trigger);
				break;
			}
			Plugin.Log.Info($"Text trigger '{trigger.Name}' matched input but was blocked by passthrough reaction restrictions.", Array.Empty<object>());
		}
	}

	private ChatType ConvertXIVChatTypeToChatType(XivChatType channel)
	{
		int channelValue = (int)channel;
		if (channelValue <= 32)
		{
			switch (channelValue - 10)
			{
			case 0:
				return ChatType.Say;
			case 1:
				return ChatType.Shout;
			case 4:
				return ChatType.Party;
			case 5:
				return ChatType.Alliance;
			case 2:
				return ChatType.Tell;
			case 3:
				return ChatType.Tell;
			}
			switch (channelValue - 24)
			{
			case 5:
				return ChatType.Emote;
			case 4:
				return ChatType.CustomEmote;
			case 6:
				return ChatType.Yell;
			case 8:
				return ChatType.Party;
			case 0:
				return ChatType.FC;
			}
		}
		else
		{
			if ((int)channel == 37)
			{
				return ChatType.CWLS1;
			}
			if ((int)channel == 56)
			{
				return ChatType.Echo;
			}
			switch (channelValue - 101)
			{
			case 0:
				return ChatType.CWLS2;
			case 1:
				return ChatType.CWLS3;
			case 2:
				return ChatType.CWLS4;
			case 3:
				return ChatType.CWLS5;
			case 4:
				return ChatType.CWLS6;
			case 5:
				return ChatType.CWLS7;
			case 6:
				return ChatType.CWLS8;
			}
		}
		return ChatType.None;
	}

	private string GetCommandPrefixForChannel(ChatType channel)
	{
		return channel switch
		{
			ChatType.Command => "", 
			ChatType.Emote => "", 
			ChatType.CustomEmote => "/em ", 
			ChatType.Echo => "/echo ", 
			ChatType.Say => "/say ", 
			ChatType.Yell => "/yell ", 
			ChatType.Shout => "/shout ", 
			ChatType.Party => "/p ", 
			ChatType.Alliance => "/a ", 
			ChatType.FC => "/fc ", 
			ChatType.Tell => "/tell ", 
			ChatType.CWLS1 => "/cwl1 ", 
			ChatType.CWLS2 => "/cwl2 ", 
			ChatType.CWLS3 => "/cwl3 ", 
			ChatType.CWLS4 => "/cwl4 ", 
			ChatType.CWLS5 => "/cwl5 ", 
			ChatType.CWLS6 => "/cwl6 ", 
			ChatType.CWLS7 => "/cwl7 ", 
			ChatType.CWLS8 => "/cwl8 ", 
			_ => "/echo ", 
		};
	}

	public unsafe void SendMessage(ChatType channel, string message, string targetForename = "", string targetSurname = "", string? targetWorld = "")
	{
		if (channel == ChatType.Echo)
		{
			SendEcho(message);
			return;
		}
		string commandPrefix = GetCommandPrefixForChannel(channel);
		string msg = "";
		if (channel == ChatType.Tell)
		{
			string name = targetForename + " " + targetSurname;
			string world = (string.IsNullOrWhiteSpace(targetWorld) ? string.Empty : ("@" + targetWorld));
			msg = $"{commandPrefix}{name}{world} {message}";
		}
		else
		{
			msg = commandPrefix + message;
		}
		Plugin.Framework.RunOnFrameworkThread((Action)delegate
		{
			Utf8String* chatEntry = null;
			try
			{
				chatEntry = Utf8String.FromString(msg);
				UIModule* uiModule = UIModule.Instance();
				if (chatEntry == null || uiModule == null)
				{
					throw new InvalidOperationException("The game chat module is unavailable.");
				}
				uiModule->ProcessChatBoxEntry(chatEntry, (IntPtr)0, false);
			}
			catch (Exception ex)
			{
				Plugin.Log.Error(ex, "Failed to send a Chatterbox chat reaction", Array.Empty<object>());
				Plugin.ChatGui.PrintError("A configured reaction could not be sent. See /xllog for details.", "Chatterbox", null);
			}
			finally
			{
				if (chatEntry != null)
				{
					chatEntry->Dtor(true);
				}
			}
		});
	}

	public void SendEcho(string message)
	{
		Plugin.ChatGui.Print(message, "Chatterbox", null);
	}
}
