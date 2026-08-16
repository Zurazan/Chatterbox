using System;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using HookBackend = Dalamud.Plugin.Services.IGameInteropProvider.HookBackend;

namespace Chatterbox;

public class EmoteHook
{
	public delegate void OnEmoteFuncDelegate(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2);

	public unsafe delegate bool CancelEmoteFuncDelegate(EmoteController* emoteController, nint unknown);

	private Plugin plugin;

	private Hook<OnEmoteFuncDelegate>? HookEmote { get; init; }

	private Hook<CancelEmoteFuncDelegate>? HookCancelEmote { get; init; }

	public event Action<EntityInfo, EntityInfo?, ushort, Trigger>? OnEmote;

	public EmoteHook(Plugin plugin)
	{
		this.plugin = plugin;
		try
		{
			HookEmote = Plugin.GameInteropProvider.HookFromSignature<OnEmoteFuncDelegate>("E8 ?? ?? ?? ?? 48 8D 8B ?? ?? ?? ?? 4C 89 74 24", (OnEmoteFuncDelegate)OnEmoteDetour, (HookBackend)0);
			HookEmote.Enable();
		}
		catch (Exception ex)
		{
			Plugin.Log.Error(ex, "EmoteHook Exception", Array.Empty<object>());
		}
	}

	public bool IsEmoteCancelPreventionEnabled()
	{
		if (HookCancelEmote != null)
		{
			return HookCancelEmote.IsEnabled;
		}
		return false;
	}

	public void DisableEmoteCancelPrevention()
	{
		if (HookCancelEmote != null && HookCancelEmote.IsEnabled)
		{
			HookCancelEmote.Disable();
		}
	}

	public void EnableEmoteCancelPrevention()
	{
		if (HookCancelEmote != null && !HookCancelEmote.IsEnabled)
		{
			HookCancelEmote.Enable();
		}
	}

	public void PerformEmote(Emote? emote, Trigger trigger, EmoteReaction emoteReaction, EntityInfo? instigator, EntityInfo? receiver)
	{
		if (emote == null)
		{
			return;
		}
		EntityInfo localPlayer = PlayerManager.LocalPlayer;
		if (localPlayer != null)
		{
			switch (emoteReaction.TargetType)
			{
			case ReactionTargetType.Untarget:
				Plugin.Targets.Target = null;
				break;
			case ReactionTargetType.TargetInstigator:
				instigator?.SetAsTarget();
				break;
			case ReactionTargetType.TargetReceiver:
				receiver?.SetAsTarget();
				break;
			case ReactionTargetType.TargetSelf:
				localPlayer.SetAsTarget();
				break;
			}
			if (emoteReaction.LookAtType != ReactionLookAtType.Target)
			{
				ForbidEmoteLookAtChange();
			}
			switch (emoteReaction.LookAtType)
			{
			case ReactionLookAtType.Instigator:
				localPlayer.FaceTowardsEntity(instigator);
				break;
			case ReactionLookAtType.Receiver:
				localPlayer.FaceTowardsEntity(receiver);
				break;
			case ReactionLookAtType.InstigatorInverse:
				localPlayer.FaceTowardsEntity(instigator, inverse: true);
				break;
			case ReactionLookAtType.ReceiverInverse:
				localPlayer.FaceTowardsEntity(receiver, inverse: true);
				break;
			case ReactionLookAtType.InstigatorDirection:
				localPlayer.FaceSameAsEntity(instigator);
				break;
			case ReactionLookAtType.ReceiverDirection:
				localPlayer.FaceSameAsEntity(receiver);
				break;
			case ReactionLookAtType.InstigatorDirectionInverse:
				localPlayer.FaceSameAsEntity(instigator, inverse: true);
				break;
			case ReactionLookAtType.ReceiverDirectionInverse:
				localPlayer.FaceSameAsEntity(receiver, inverse: true);
				break;
			}
			if (!string.IsNullOrWhiteSpace(emote.Command))
			{
				PerformEmoteCommand(emote.Command, trigger, emoteReaction);
			}
			else if (emote.IsPose)
			{
				PerformPoseEmote(emote, trigger, emoteReaction);
			}
			if (emoteReaction.LookAtType != ReactionLookAtType.Target)
			{
				AllowEmoteLookAtChange();
			}
		}
	}

	private void PerformPoseEmote(Emote emote, Trigger trigger, EmoteReaction emoteReaction)
	{
	}

	public void PerformEmoteCommand(string command, Trigger trigger, EmoteReaction emoteReaction)
	{
		plugin.Chat.SendMessage(ChatType.Emote, command);
	}

	public void ForbidEmoteLookAtChange()
	{
		Game.ForceDisableMovement++;
	}

	public void AllowEmoteLookAtChange()
	{
		if (Game.ForceDisableMovement > 0)
		{
			Game.ForceDisableMovement--;
		}
	}

	public void Dispose()
	{
		HookEmote?.Dispose();
	}

	private void OnEmoteDetour(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2)
	{
		if (Plugin.Config.Enabled && PlayerManager.LocalPlayer != null)
		{
			EntityInfo instigatorEntity = PlayerManager.NearbyPlayers.FirstOrDefault((EntityInfo x) => (ulong)(nint)((IGameObject)x.Character).Address == instigatorAddr);
			if (instigatorEntity != null)
			{
				EntityInfo localPlayer = PlayerManager.LocalPlayer;
				EntityInfo receiverEntity = null;
				foreach (Trigger trigger in Plugin.Config.Triggers)
				{
					if (!trigger.Enabled || trigger.Type != TriggerType.Emote || trigger.Instigator == null)
					{
						continue;
					}
					Instigator? instigator = trigger.Instigator;
					if (instigator != null && instigator.Type == PlayerType.None)
					{
						continue;
					}
					Instigator? instigator2 = trigger.Instigator;
					if ((instigator2 == null || instigator2.Type != PlayerType.Ignore) && trigger.ReceivedAction is EmoteAction triggerAction && (triggerAction.MatchAny || triggerAction.IDs.Contains(emoteId)) && (trigger.Instigator == null || ((trigger.Instigator.Type == PlayerType.All || ((trigger.Instigator.Type != PlayerType.Self || ((IGameObject)instigatorEntity.Character).Address == ((IGameObject)localPlayer.Character).Address) && (trigger.Instigator.Type != PlayerType.Others || ((IGameObject)instigatorEntity.Character).Address != ((IGameObject)localPlayer.Character).Address) && (trigger.Instigator.Type != PlayerType.Player || trigger.Instigator.PlayerNameMatches(instigatorEntity.Name)) && (trigger.Instigator.Type != PlayerType.Target || (localPlayer.IsTargetValid && ((IGameObject)instigatorEntity.Character).Address == localPlayer.Target.Address)) && (trigger.Instigator.Type != PlayerType.Targeter || (instigatorEntity.IsTargetValid && instigatorEntity.Target.Address == ((IGameObject)localPlayer.Character).Address)))) && (trigger.Instigator.Type == PlayerType.Self || !trigger.Instigator.BlacklistNameMatches(instigatorEntity.Name)) && (trigger.Instigator.Type == PlayerType.None || ((trigger.Instigator.Type == PlayerType.Self || (trigger.Instigator.MeetsRelationConditions(instigatorEntity) && trigger.Instigator.MeetsGenderCondition(instigatorEntity) && trigger.Instigator.MeetsRaceCondition(instigatorEntity))) && trigger.Instigator.MeetsStatusConditions(instigatorEntity))))))
					{
						receiverEntity = PlayerManager.NearbyPlayers.FirstOrDefault((EntityInfo x) => x.GameObject.GameObjectId == targetId);
						if ((!(trigger.Receiver is EmoteTargetReceiver triggerReceiver) || ((triggerReceiver.Type != PlayerType.All || receiverEntity != null) && (triggerReceiver.Type == PlayerType.All || ((triggerReceiver.Type != PlayerType.None || receiverEntity == null) && (triggerReceiver.Type != PlayerType.Self || ((receiverEntity != null) ? new nint?(((IGameObject)receiverEntity.Character).Address) : ((nint?)null)) == (nint?)(nint)((IGameObject)localPlayer.Character).Address) && (triggerReceiver.Type != PlayerType.Others || (receiverEntity != null && ((receiverEntity != null) ? new nint?(((IGameObject)receiverEntity.Character).Address) : ((nint?)null)) != (nint?)(nint)((IGameObject)localPlayer.Character).Address)) && (triggerReceiver.Type != PlayerType.Player || receiverEntity != null) && (triggerReceiver.Type != PlayerType.Player || receiverEntity == null || triggerReceiver.PlayerNameMatches(receiverEntity.Name)) && (triggerReceiver.Type != PlayerType.Target || (localPlayer.IsTargetValid && ((receiverEntity != null) ? new nint?(((IGameObject)receiverEntity.Character).Address) : ((nint?)null)) == (nint?)(nint)localPlayer.Target.Address)))) && (triggerReceiver.Type == PlayerType.Ignore || triggerReceiver.Type == PlayerType.None || ((triggerReceiver.Type == PlayerType.Self || (triggerReceiver.MeetsRelationConditions(receiverEntity) && triggerReceiver.MeetsGenderCondition(receiverEntity) && triggerReceiver.MeetsRaceCondition(receiverEntity))) && triggerReceiver.MeetsStatusConditions(receiverEntity))))) && (trigger.ReactionOptions == null || !trigger.ReactionOptions.PassthroughRestrictions || (localPlayer.CanReactionInterruptCurrentState(trigger.ReactionOptions) && (!trigger.ReactionOptions.RestrictRange || ((instigatorEntity.IsLocalPlayer || instigatorEntity.IsWithinReactionAngleAndDistanceToLocalPlayer(trigger.ReactionOptions)) && (!instigatorEntity.IsLocalPlayer || receiverEntity == null || receiverEntity.IsWithinReactionAngleAndDistanceToLocalPlayer(trigger.ReactionOptions)))) && trigger.ReactionOptions.MeetsTerritoryConditions())))
						{
							OnEmote?.Invoke(instigatorEntity, receiverEntity, emoteId, trigger);
							break;
						}
					}
				}
			}
		}
		HookEmote?.Original(unk, instigatorAddr, emoteId, targetId, unk2);
	}
}
