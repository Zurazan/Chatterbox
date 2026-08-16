using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace Chatterbox;

public static class GameObjectEx
{
	public unsafe static GameObject* ToCsGameObject(this IGameObject o)
	{
		return (GameObject*)o.Address;
	}

	public unsafe static GameObject* ToCsGameObject(this IPlayerCharacter o)
	{
		return (GameObject*)((IGameObject)o).Address;
	}

	public unsafe static GameObject* ToCsGameObject(this IBattleNpc o)
	{
		return (GameObject*)((IGameObject)o).Address;
	}

	public unsafe static Character* ToCsPlayerCharacter(this IGameObject o)
	{
		return (Character*)o.Address;
	}

	public unsafe static Character* ToCsPlayerCharacter(this IPlayerCharacter o)
	{
		return (Character*)((IGameObject)o).Address;
	}

	public unsafe static BattleChara* ToCsBattleChara(this IGameObject o)
	{
		return (BattleChara*)o.Address;
	}

	public unsafe static BattleChara* ToCsBattleChara(this IBattleNpc o)
	{
		return (BattleChara*)((IGameObject)o).Address;
	}

	public static IGameObject? ToDalamudGameObject(this IPlayerCharacter o)
	{
		return Plugin.Objects.CreateObjectReference(((IGameObject)o).Address);
	}

	public static IGameObject? ToDalamudGameObject(this IBattleNpc o)
	{
		return Plugin.Objects.CreateObjectReference(((IGameObject)o).Address);
	}

	public unsafe static void SetAsTarget(this IGameObject o)
	{
		TargetSystem.Instance()->Target = o.ToCsGameObject();
	}

	public unsafe static void SetAsSoftTarget(this IGameObject o)
	{
		TargetSystem.Instance()->SoftTarget = o.ToCsGameObject();
	}

	public unsafe static void SetAsFocusTarget(this IGameObject o)
	{
		TargetSystem.Instance()->FocusTarget = o.ToCsGameObject();
	}

	public unsafe static void SetAsMouseOverTarget(this IGameObject o)
	{
		TargetSystem.Instance()->MouseOverTarget = o.ToCsGameObject();
	}

	public static void SetAsTarget(this IPlayerCharacter o)
	{
		Plugin.Targets.Target = o.ToDalamudGameObject();
	}

	public static void SetAsSoftTarget(this IPlayerCharacter o)
	{
		Plugin.Targets.SoftTarget = o.ToDalamudGameObject();
	}

	public static void SetAsFocusTarget(this IPlayerCharacter o)
	{
		Plugin.Targets.FocusTarget = o.ToDalamudGameObject();
	}

	public static void SetAsMouseOverTarget(this IPlayerCharacter o)
	{
		Plugin.Targets.MouseOverTarget = o.ToDalamudGameObject();
	}

	public static void SetAsTarget(this IBattleNpc o)
	{
		Plugin.Targets.Target = o.ToDalamudGameObject();
	}

	public static void SetAsSoftTarget(this IBattleNpc o)
	{
		Plugin.Targets.SoftTarget = o.ToDalamudGameObject();
	}

	public static void SetAsFocusTarget(this IBattleNpc o)
	{
		Plugin.Targets.FocusTarget = o.ToDalamudGameObject();
	}

	public static void SetAsMouseOverTarget(this IBattleNpc o)
	{
		Plugin.Targets.MouseOverTarget = o.ToDalamudGameObject();
	}

	public static bool IsFromCurrentWorld(this IPlayerCharacter pc)
	{
		return pc.CurrentWorld.RowId == pc.HomeWorld.RowId;
	}

	public static bool IsFromCurrentDatacenter(this IPlayerCharacter pc)
	{
		World value = pc.CurrentWorld.Value;
		uint rowId = value.DataCenter.RowId;
		value = pc.HomeWorld.Value;
		return rowId == value.DataCenter.RowId;
	}

	public unsafe static void OpenCharaCard(this IPlayerCharacter pc)
	{
		AgentCharaCard.Instance()->OpenCharaCard(pc.ToCsGameObject());
	}

	public unsafe static void OpenExamine(this IPlayerCharacter pc)
	{
		AgentInspect.Instance()->ExamineCharacter(((IGameObject)pc).EntityId, false);
	}

	public static bool HasOnlineStatus(this IPlayerCharacter pc, OnlineStatusTypeRaw status)
	{
		return ((ICharacter)pc).OnlineStatus.RowId == (uint)status;
	}

	public static bool HasOnlineStatus(this IPlayerCharacter pc, uint status)
	{
		return ((ICharacter)pc).OnlineStatus.RowId == status;
	}
}
