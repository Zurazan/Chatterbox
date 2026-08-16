using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace Chatterbox;

internal static class PlayerManager
{
	internal static List<EntityInfo> NearbyPlayers = new List<EntityInfo>();

	internal static EntityInfo? _localPlayer = null;

	internal static EntityInfo? LocalPlayer
	{
		get
		{
			Plugin.Framework.RunOnFrameworkThread((Action)delegate
			{
				IPlayerCharacter localPlayer = Plugin.Objects.LocalPlayer;
				if (localPlayer == null)
				{
					_localPlayer = null;
				}
				else if (_localPlayer == null || _localPlayer.Character != localPlayer)
				{
					_localPlayer = new EntityInfo(localPlayer)
					{
						IsMareSynced = true
					};
				}
			});
			return _localPlayer;
		}
	}

	internal unsafe static short CurrentWard => (short)(HousingManager.Instance()->GetCurrentWard() + 1);

	internal static bool IsInWard => CurrentWard > 0;

	internal unsafe static short CurrentPlot => (short)(HousingManager.Instance()->GetCurrentPlot() + 1);

	internal static bool IsInPlot => CurrentPlot > 0;

	internal unsafe static short CurrentRoom => HousingManager.Instance()->GetCurrentRoom();

	internal static bool IsInRoom => CurrentRoom > 0;

	internal unsafe static bool IsInside => HousingManager.Instance()->IsInside();

	internal unsafe static bool IsOutside => HousingManager.Instance()->IsOutside();

	internal unsafe static bool IsInWorkshop => HousingManager.Instance()->IsInWorkshop();

	internal static bool IsInWardArea
	{
		get
		{
			if (IsInWard && !IsInPlot)
			{
				return IsOutside;
			}
			return false;
		}
	}

	internal static bool IsInPlotOutside
	{
		get
		{
			if (IsInWard && IsInPlot)
			{
				return IsOutside;
			}
			return false;
		}
	}

	internal static bool IsInPlotInside
	{
		get
		{
			if (IsInWard && IsInPlot && !IsInRoom)
			{
				return IsInside;
			}
			return false;
		}
	}

	internal static bool IsInFCRoom
	{
		get
		{
			if (IsInWard && IsInPlot && IsInRoom)
			{
				return IsInside;
			}
			return false;
		}
	}

	internal static bool IsInAptRoom
	{
		get
		{
			if (IsInWard && !IsInPlot && IsInRoom)
			{
				return IsInside;
			}
			return false;
		}
	}

	internal static bool IsInAptLobby
	{
		get
		{
			if (IsInWard && !IsInPlot && !IsInRoom)
			{
				return IsInside;
			}
			return false;
		}
	}

	internal static bool NotResidential
	{
		get
		{
			if (!IsInWard && !IsInPlot && !IsInRoom && !IsInside && !IsOutside)
			{
				return !IsInWorkshop;
			}
			return false;
		}
	}

	internal static EntityInfo? GetTargetAsEntity()
	{
		IPlayerCharacter localPlayer = Plugin.Objects.LocalPlayer;
		IGameObject target = ((localPlayer != null) ? ((IGameObject)localPlayer).TargetObject : null);
		if (target == null)
		{
			return null;
		}
		return new EntityInfo(target);
	}

	internal static void UpdatePlayerList()
	{
		if (!Plugin.Config.Enabled)
		{
			return;
		}
		List<EntityInfo> nearbyPlayers = new List<EntityInfo>();
		EntityInfo? localPlayer = LocalPlayer;
		if (localPlayer == null)
		{
			NearbyPlayers = new List<EntityInfo>();
			return;
		}
		HashSet<nint> addressList = Mare.MareGetNearbyPlayerAddresses();
		localPlayer.IsMareSynced = addressList != null;
		nearbyPlayers.Add(localPlayer);
		foreach (IPlayerCharacter character in ((IEnumerable<IGameObject>)Plugin.Objects).Where((IGameObject x) => x.IsValid() && x.GameObjectId != ((IGameObject)localPlayer.Character).GameObjectId).OfType<IPlayerCharacter>())
		{
			bool mareSynced = addressList != null && addressList.FirstOrDefault((nint x) => x == (nint)((IGameObject)character).Address) != 0;
			EntityInfo entity = new EntityInfo(character)
			{
				IsMareSynced = mareSynced
			};
			nearbyPlayers.Add(entity);
		}
		NearbyPlayers = nearbyPlayers;
	}
}
