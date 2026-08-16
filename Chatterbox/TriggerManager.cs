using System;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;

namespace Chatterbox;

public class TriggerManager
{
	private Plugin plugin;

	public CounterManager CounterManager;

	private ReactionQueue ReactionQueue;

	public TriggerManager(Plugin plugin)
	{
		this.plugin = plugin;
		CounterManager = new CounterManager(plugin);
		ReactionQueue = new ReactionQueue(plugin, this);
		plugin.EmoteHook.OnEmote += OnEmote;
		plugin.Chat.OnChat += OnChat;
	}

	public void Update()
	{
		CounterManager.Update();
	}

	public void Dispose()
	{
		plugin.EmoteHook.OnEmote -= OnEmote;
		plugin.Chat.OnChat -= OnChat;
		ReactionQueue.Dispose();
		CounterManager.Dispose();
	}

	public void PreviewTitle(Trigger trigger, Counter counter)
	{
		CounterManager.SetTitle(trigger, counter, "Primu", "Chatterbox", "Miyu", "Myon");
	}

	public void PreviewToast(Counter counter)
	{
		CounterManager.SetToast(counter, "Primu", "Chatterbox", "Miyu", "Myon");
	}

	public void PreviewEcho(Counter counter)
	{
		CounterManager.SetEcho(counter, "Primu", "Chatterbox", "Miyu", "Myon");
	}

	public void PreviewQueue(Trigger trigger)
	{
		if (trigger.Reactions == null || trigger.Reactions.Count == 0)
		{
			return;
		}
		EntityInfo localPlayer = PlayerManager.LocalPlayer;
		if (localPlayer == null)
		{
			return;
		}
		if (trigger.Type == TriggerType.Emote)
		{
			EmoteTargetReceiver receiver = trigger.Receiver as EmoteTargetReceiver;
			if (localPlayer.IsTargetValid)
			{
				IGameObject target = localPlayer.Target;
				IPlayerCharacter targetPlayer = (IPlayerCharacter)(object)((target is IPlayerCharacter) ? target : null);
				if (targetPlayer != null)
				{
					EntityInfo targetEntity = new EntityInfo(targetPlayer);
					Instigator? instigator = trigger.Instigator;
					EntityInfo instEntity = ((instigator != null && instigator.Type == PlayerType.Self) ? localPlayer : targetEntity);
					EntityInfo recEntity = ((receiver != null && receiver.Type == PlayerType.Self) ? localPlayer : ((receiver == null || receiver.Type != PlayerType.None) ? targetEntity : null));
					ReactionQueue.EnqueueEmote(instEntity, recEntity, 0, trigger, preview: true);
					return;
				}
			}
			if ((receiver == null || receiver.Type != PlayerType.None) && (receiver == null || receiver.Type != PlayerType.Others))
			{
				if (receiver == null)
				{
					_ = 1;
				}
				else
					_ = receiver.Type != PlayerType.Player;
			}
			ReactionQueue.EnqueueEmote(localPlayer, ((receiver == null || receiver.Type != PlayerType.None) && (receiver == null || receiver.Type != PlayerType.Others)) ? localPlayer : null, 0, trigger, preview: true);
			return;
		}
		if (trigger.Type == TriggerType.Text)
		{
			ReactionQueue.EnqueueText(localPlayer.Name, string.Empty, null, ChatType.Echo, trigger, preview: true);
		}
	}

	private void OnEmote(EntityInfo instigator, EntityInfo? receiver, ushort emoteId, Trigger trigger)
	{
		if (trigger.Reactions == null || trigger.Reactions.Count == 0)
		{
			CounterManager.UpdateCounter(trigger, instigator, receiver);
			return;
		}
		long now = Environment.TickCount64;
		ReactionOptions options = trigger.ReactionOptions ?? new ReactionOptions();
		if (now - trigger.LastReactionTime >= options.ReactionCooldown)
		{
			trigger.LastReactionTime = now;
			ReactionQueue.EnqueueEmote(instigator, receiver, emoteId, trigger, CounterManager);
		}
	}

	private void OnChat(string instigatorName, string instigatorMessage, EntityInfo? instigator, ChatType channel, Trigger trigger)
	{
		if (trigger.Reactions == null || trigger.Reactions.Count == 0)
		{
			CounterManager.UpdateCounter(trigger, instigatorName);
			return;
		}
		long now = Environment.TickCount64;
		ReactionOptions options = trigger.ReactionOptions ?? new ReactionOptions();
		if (now - trigger.LastReactionTime >= options.ReactionCooldown)
		{
			trigger.LastReactionTime = now;
			if (ReactionQueue.EnqueueText(instigatorName, instigatorMessage, instigator, channel, trigger, CounterManager))
			{
				Plugin.Log.Info($"Text trigger '{trigger.Name}' was accepted into the reaction queue.", Array.Empty<object>());
			}
		}
		else
		{
			Plugin.Log.Info($"Text trigger '{trigger.Name}' matched during its {options.ReactionCooldown}ms cooldown.", Array.Empty<object>());
		}
	}

	public Task PerformEmoteReaction(QueuedEmoteEvent qr, EmoteReaction reaction)
	{
		return Plugin.Framework.RunOnFrameworkThread((Action)delegate
		{
			if (reaction.CopyInstigator)
			{
				if (!qr.IsPreview)
				{
					Emote instEmote = plugin.Emotes.FirstOrDefault((Emote e) => e.ID == qr.EmoteId);
					Emote emote = plugin.Emotes.FirstOrDefault((Emote e) => e.Name == instEmote?.Name && (!string.IsNullOrWhiteSpace(e.Command) || e.IsPose));
					plugin.EmoteHook.PerformEmote(emote, qr.Trigger, reaction, qr.Instigator, qr.Receiver);
				}
			}
			else
			{
				Emote emote2 = plugin.Emotes.FirstOrDefault((Emote e) => e.ID == reaction.ID && (!string.IsNullOrWhiteSpace(e.Command) || e.IsPose));
				plugin.EmoteHook.PerformEmote(emote2, qr.Trigger, reaction, qr.Instigator, qr.Receiver);
			}
		});
	}

	public Task PerformEmoteReaction(QueuedTextEvent qr, EmoteReaction reaction)
	{
		return Plugin.Framework.RunOnFrameworkThread((Action)delegate
		{
			Emote emote = plugin.Emotes.FirstOrDefault((Emote e) => e.ID == reaction.ID && (!string.IsNullOrWhiteSpace(e.Command) || e.IsPose));
			plugin.EmoteHook.PerformEmote(emote, qr.Trigger, reaction, qr.Instigator, null);
		});
	}

	public Task PerformTextReaction(QueuedEmoteEvent qr, TextReaction reaction)
	{
		return Plugin.Framework.RunOnFrameworkThread((Action)delegate
		{
			if (reaction.Channel != ChatType.None && reaction.Channel != ChatType.Emote && !string.IsNullOrWhiteSpace(reaction.Template))
			{
				string message = reaction.Template.Replace("%ifn%", qr.Instigator.Forename).Replace("%isn%", qr.Instigator.Surname);
				plugin.Chat.SendMessage(qr.IsPreview ? ChatType.Echo : reaction.Channel, message, qr.Instigator.Forename, qr.Instigator.Surname, qr.Instigator.HomeWorld);
			}
		});
	}

	public Task PerformTextReaction(QueuedTextEvent qr, TextReaction reaction)
	{
		return Plugin.Framework.RunOnFrameworkThread((Action)delegate
		{
			if ((reaction.SameChannel || (reaction.Channel != ChatType.None && reaction.Channel != ChatType.Emote)) && (reaction.CopyInstigator || !string.IsNullOrWhiteSpace(reaction.Template)))
			{
				Instigator? instigator = qr.Trigger.Instigator;
				if (instigator == null || instigator.Type != PlayerType.Self || reaction.Channel == ChatType.Echo || (!reaction.SameChannel && reaction.Channel != qr.Channel))
				{
					string forename = qr.InstigatorName.GetForename();
					(string, string?) surnameWorld = qr.InstigatorName.GetSurnameWorld();
					string item = surnameWorld.Item1;
					string item2 = surnameWorld.Item2;
					string text = (reaction.CopyInstigator ? qr.InstigatorMessage : reaction.Template.Replace("%ifn%", forename).Replace("%isn%", item));
					plugin.Chat.SendMessage(qr.IsPreview ? ChatType.Echo : (reaction.SameChannel ? qr.Channel : reaction.Channel), qr.IsPreview ? (qr.InstigatorName + ": " + text) : text, forename, item, item2);
				}
			}
		});
	}
}
