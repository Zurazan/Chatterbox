using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Common.Math;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using Vector2 = System.Numerics.Vector2;
using Vector3 = FFXIVClientStructs.FFXIV.Common.Math.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace Chatterbox;

public class EntityInfo
{
	public IGameObject GameObject;

	private string _Name = string.Empty;

	private string _HomeWorld = string.Empty;

	private string _CompanyTag = string.Empty;

	public bool IsMareSynced { get; set; }

	internal IPlayerCharacter Character
	{
		get
		{
			return (IPlayerCharacter)GameObject;
		}
	}

	internal unsafe GameObject* GameObjectPtr => GameObject.ToCsGameObject();

	internal unsafe Character* CharacterPtr => GameObject.ToCsPlayerCharacter();

	internal string Name
	{
		get
		{
			if (string.IsNullOrWhiteSpace(_Name))
			{
				_Name = GameObject.Name.TextValue;
			}
			return _Name;
		}
	}

	internal string Forename
	{
		get
		{
			if (!Name.Contains(' '))
			{
				return string.Empty;
			}
			return Name.Split(' ')[0];
		}
	}

	internal string Surname
	{
		get
		{
			if (!Name.Contains(' '))
			{
				return string.Empty;
			}
			return Name.Split(' ')[1];
		}
	}

	internal bool IsLocalPlayer
	{
		get
		{
			if (PlayerManager.LocalPlayer != null)
			{
				return GameObject.GameObjectId == PlayerManager.LocalPlayer.GameObject.GameObjectId;
			}
			return false;
		}
	}

	internal unsafe Gender Gender
	{
		get
		{
			if (CharacterPtr->Sex != 0)
			{
				return Gender.Female;
			}
			return Gender.Male;
		}
	}

	internal unsafe Race Race
	{
		get
		{
			switch (CharacterPtr->ModelContainer.ModelSkeletonId - 20000)
			{
			case 101:
			case 201:
				return Race.Midlander;
			case 301:
			case 401:
				return Race.Highlander;
			case 501:
			case 601:
				return Race.Elezen;
			case 701:
			case 801:
				return Race.Miqote;
			case 901:
			case 1001:
				return Race.Roegadyn;
			case 1101:
			case 1201:
				return Race.Lalafell;
			case 1301:
			case 1401:
				return Race.AuRa;
			case 1501:
			case 1601:
				return Race.Hrothgar;
			case 1701:
			case 1801:
				return Race.Viera;
			default:
				return Race.Unknown;
			}
		}
	}

	internal JobInfo Job
	{
		get
		{
			return new JobInfo((Character != null) ? ((ICharacter)Character).ClassJob.RowId : 0u);
		}
	}

	internal unsafe string CompanyTag
	{
		get
		{
			if (string.IsNullOrWhiteSpace(_CompanyTag))
			{
				_CompanyTag = CharacterPtr->FreeCompanyTagString;
			}
			return _CompanyTag;
		}
	}

	internal string HomeWorld
	{
		get
		{
			if (string.IsNullOrWhiteSpace(_HomeWorld))
			{
				object obj;
				if (Character == null)
				{
					obj = "";
				}
				else
				{
					World? valueNullable = Character.HomeWorld.ValueNullable;
					if (!valueNullable.HasValue)
					{
						obj = null;
					}
					else
					{
						World valueOrDefault = valueNullable.GetValueOrDefault();
						obj = ((object)valueOrDefault.Name/*cast due to constrained. prefix*/).ToString();
					}
					if (obj == null)
					{
						obj = "";
					}
				}
				_HomeWorld = (string)obj;
			}
			return _HomeWorld;
		}
	}

	internal byte Level
	{
		get
		{
			IPlayerCharacter character = Character;
			if (character == null)
			{
				return 0;
			}
			return ((ICharacter)character).Level;
		}
	}

	internal Vector3 Position
	{
		get
		{
			return (Vector3)GameObject.Position;
		}
	}

	internal unsafe float Angle => GameObjectPtr->Rotation;

	internal unsafe double DistanceFromLocalPlayer
	{
		get
		{
			EntityInfo localPlayer = PlayerManager.LocalPlayer;
			if (localPlayer == null)
			{
				return 0.0;
			}
			Vector3 val = localPlayer.CharacterPtr->Position - Position;
			return val.Magnitude;
		}
	}

	internal unsafe float WorldSpaceAngleFromLocalPlayer
	{
		get
		{
			EntityInfo localPlayer = PlayerManager.LocalPlayer;
			if (localPlayer == null)
			{
				return 0f;
			}
			Vector3 offset = Position - localPlayer.CharacterPtr->Position;
			float angle = (float)Math.Atan2(offset.Z, 0f - offset.X);
			angle -= (float)Math.PI / 2f;
			if ((double)angle < -Math.PI)
			{
				angle += (float)Math.PI * 2f;
			}
			if ((double)angle > Math.PI)
			{
				angle -= (float)Math.PI * 2f;
			}
			return angle;
		}
	}

	internal unsafe double AngleFromLocalPlayer
	{
		get
		{
			EntityInfo localPlayer = PlayerManager.LocalPlayer;
			if (localPlayer == null)
			{
				return 0.0;
			}
			Vector3 direction = Position - localPlayer.CharacterPtr->Position;
			direction = Vector3.Normalize(direction);
			float rotation = localPlayer.Angle;
			float dx = (float)((double)(0f - direction.X) * Math.Cos(0f - rotation) - (double)direction.Z * Math.Sin(0f - rotation));
			return Math.Atan2((float)((double)(0f - direction.X) * Math.Sin(rotation) + (double)(0f - direction.Z) * Math.Cos(rotation)), dx);
		}
	}

	internal unsafe double AngleFromTarget
	{
		get
		{
			IGameObject targetObject = Target;
			if (targetObject == null)
			{
				return 0.0;
			}
			Vector3 direction = Position - targetObject.ToCsGameObject()->Position;
			direction = Vector3.Normalize(direction);
			float rotation = targetObject.Rotation;
			float dx = (float)((double)(0f - direction.X) * Math.Cos(0f - rotation) - (double)direction.Z * Math.Sin(0f - rotation));
			return Math.Atan2((float)((double)(0f - direction.X) * Math.Sin(rotation) + (double)(0f - direction.Z) * Math.Cos(rotation)), dx);
		}
	}

	internal string DirectionStr
	{
		get
		{
			if (PlayerManager.LocalPlayer == null || !IsValid)
			{
				return "";
			}
			double degrees = AngleFromLocalPlayer * (180.0 / Math.PI);
			if (degrees < 0.0)
			{
				degrees += 360.0;
			}
			if (degrees >= 337.5 || degrees < 22.5)
			{
				return "→";
			}
			if (degrees >= 22.5 && degrees < 67.5)
			{
				return "↗";
			}
			if (degrees >= 67.5 && degrees < 112.5)
			{
				return "↑";
			}
			if (degrees >= 112.5 && degrees < 157.5)
			{
				return "↖";
			}
			if (degrees >= 157.5 && degrees < 202.5)
			{
				return "←";
			}
			if (degrees >= 202.5 && degrees < 247.5)
			{
				return "↙";
			}
			if (degrees >= 247.5 && degrees < 292.5)
			{
				return "↓";
			}
			if (degrees >= 292.5 && degrees < 337.5)
			{
				return "↘";
			}
			return "";
		}
	}

	internal unsafe bool IsFriend => CharacterPtr->IsFriend;

	internal unsafe bool IsBlocked
	{
		get
		{
			return (int)InfoProxyBlacklist.Instance()->GetBlockResultType(CharacterPtr->AccountId, CharacterPtr->ContentId) != 1;
		}
	}

	internal unsafe bool IsEnemyPlayer => CharacterPtr->IsHostile;

	internal unsafe bool IsInParty
	{
		get
		{
			if (IsValid)
			{
				return CharacterPtr->IsPartyMember;
			}
			return false;
		}
	}

	internal bool IsKnownPlayer
	{
		get
		{
			if (!IsInParty)
			{
				return IsFriend;
			}
			return true;
		}
	}

	internal bool IsDead => GameObject.IsDead;

	internal unsafe bool InCombat => CharacterPtr->InCombat;

	internal unsafe ushort EmoteId => CharacterPtr->EmoteController.EmoteId;

	internal bool IsEmote
	{
		get
		{
			if (!IsLoopingEmote && !IsSleeping)
			{
				return Plugin.SpecialEmotes.FirstOrDefault((SpecialEmote x) => x.ID == EmoteId) == null;
			}
			return false;
		}
	}

	internal unsafe bool IsLoopingEmote
	{
		get
		{
			if ((int)GetPoseType() == 0)
			{
				return CharacterPtr->Mode.HasFlag((CharacterModes)3);
			}
			return false;
		}
	}

	internal bool IsMoving
	{
		get
		{
			return (int)GetPoseType() == 255;
		}
	}

	internal bool IsStandingIdle
	{
		get
		{
			if ((int)GetPoseType() == 0)
			{
				return !IsLoopingEmote;
			}
			return false;
		}
	}

	internal bool IsChairSitting
	{
		get
		{
			return (int)GetPoseType() == 2;
		}
	}

	internal bool IsGroundSitting
	{
		get
		{
			return (int)GetPoseType() == 3;
		}
	}

	internal bool IsSleeping
	{
		get
		{
			return (int)GetPoseType() == 4;
		}
	}

	internal ulong ObjectId => GameObject.GameObjectId;

	internal bool IsValid
	{
		get
		{
			if (GameObject != null && GameObject.IsValid())
			{
				return Name == GameObject.Name.TextValue;
			}
			return false;
		}
	}

	internal bool IsTargetValid
	{
		get
		{
			if (GameObject.TargetObject != null && GameObject.TargetObject.IsValid())
			{
				return GameObject.TargetObject.IsTargetable;
			}
			return false;
		}
	}

	internal IGameObject Target => GameObject.TargetObject;

	internal ulong TargetObjectId => GameObject.TargetObjectId;

	internal unsafe ulong SoftTargetObjectId
	{
		get
		{
			return (GameObjectId)CharacterPtr->GetSoftTargetId();
		}
	}

	internal bool IsTargetingMe
	{
		get
		{
			if (PlayerManager.LocalPlayer != null)
			{
				return TargetObjectId == ((IGameObject)PlayerManager.LocalPlayer.Character).GameObjectId;
			}
			return false;
		}
	}

	public EntityInfo(IPlayerCharacter baseObject)
	{
		GameObject = (IGameObject)(object)baseObject;
	}

	public EntityInfo(IGameObject baseObject)
	{
		GameObject = baseObject;
	}

	internal void FaceTowardsEntity(EntityInfo? entity, bool inverse = false)
	{
		EntityInfo localPlayer = PlayerManager.LocalPlayer;
		if (localPlayer != null && localPlayer.Character == Character && entity != null)
		{
			if (inverse)
			{
				SetRotation(entity.WorldSpaceAngleFromLocalPlayer + (float)Math.PI);
			}
			else
			{
				SetRotation(entity.WorldSpaceAngleFromLocalPlayer);
			}
		}
	}

	internal void FaceSameAsEntity(EntityInfo? entity, bool inverse = false)
	{
		EntityInfo localPlayer = PlayerManager.LocalPlayer;
		if (localPlayer != null && localPlayer.Character == Character && entity != null)
		{
			if (inverse)
			{
				SetRotation(entity.Angle + (float)Math.PI);
			}
			else
			{
				SetRotation(entity.Angle);
			}
		}
	}

	internal unsafe void SetRotation(float angle)
	{
		EntityInfo localPlayer = PlayerManager.LocalPlayer;
		if (localPlayer != null && localPlayer.Character == Character)
		{
			GameObjectPtr->SetRotation(angle);
		}
	}

	internal unsafe void SetRotationOffset(float angle)
	{
		EntityInfo localPlayer = PlayerManager.LocalPlayer;
		if (localPlayer != null && localPlayer.Character == Character)
		{
			GameObjectPtr->SetRotation(GameObjectPtr->Rotation + angle);
		}
	}

	internal void DrawDirection(Vector2 center, float radius, float outline, Vector4 col, Vector4 outlineCol)
	{
		double angleFromLocalPlayer = AngleFromLocalPlayer;
		float cosAngle = (float)Math.Cos(angleFromLocalPlayer);
		float sinAngle = (float)Math.Sin(angleFromLocalPlayer);
		ImDrawListPtr windowDrawList;
		if (outline > 0f)
		{
			float outlineRadius = radius + outline;
			windowDrawList = ImGui.GetWindowDrawList();
			windowDrawList.AddCircleFilled(center, outlineRadius, ImGui.GetColorU32(outlineCol));
			Vector2 ofacingDirection = new Vector2(cosAngle, sinAngle);
			Vector2 op1 = center + ofacingDirection * (outlineRadius * 2f);
			Vector2 perpDirection = new Vector2(0f - ofacingDirection.Y, ofacingDirection.X);
			Vector2 op2 = center + perpDirection * outlineRadius;
			Vector2 op3 = center - perpDirection * outlineRadius;
			windowDrawList = ImGui.GetWindowDrawList();
			windowDrawList.AddTriangleFilled(op1, op2, op3, ImGui.GetColorU32(outlineCol));
		}
		windowDrawList = ImGui.GetWindowDrawList();
		windowDrawList.AddCircleFilled(center, radius, ImGui.GetColorU32(col));
		Vector2 facingDirection = new Vector2(cosAngle, sinAngle);
		Vector2 p1 = center + facingDirection * (radius * 2f);
		Vector2 perpFacingDirection = new Vector2(0f - facingDirection.Y, facingDirection.X);
		Vector2 p2 = center + perpFacingDirection * radius;
		Vector2 p3 = center - perpFacingDirection * radius;
		windowDrawList = ImGui.GetWindowDrawList();
		windowDrawList.AddTriangleFilled(p1, p2, p3, ImGui.GetColorU32(col));
	}

	internal bool IsWithinReactionAngleAndDistanceToLocalPlayer(ReactionOptions options)
	{
		if (PlayerManager.LocalPlayer == null)
		{
			return false;
		}
		if (!options.RestrictRange)
		{
			return true;
		}
		double distance = DistanceFromLocalPlayer;
		if (distance < (double)options.RestrictedDistanceMin || distance > (double)options.RestrictedDistanceMax)
		{
			return false;
		}
		if (options.RestrictedAngleArea <= 0f)
		{
			return true;
		}
		double angleFromLocalPlayer = AngleFromLocalPlayer;
		double correctionAngle = -Math.PI / 2.0;
		double dirRad = options.RestrictedAngleDirection.DegreesToRadians() + correctionAngle;
		double halfCone = Math.PI * 2.0 * (double)Math.Clamp(options.RestrictedAngleArea, 0f, 1f) / 2.0;
		return Math.Abs(NormalizeAngle(angleFromLocalPlayer - dirRad)) <= halfCone;
	}

	private static double NormalizeAngle(double angle)
	{
		while (angle > Math.PI)
		{
			angle -= Math.PI * 2.0;
		}
		while (angle < -Math.PI)
		{
			angle += Math.PI * 2.0;
		}
		return angle;
	}

	internal unsafe EmoteController.PoseType GetPoseType()
	{
		return (EmoteController.PoseType)(byte)CharacterPtr->EmoteController.GetPoseKind();
	}

	internal unsafe void SetEmote(ushort emoteId)
	{
		EmoteManager.Instance()->ExecuteEmote(emoteId, (EmoteController.PlayEmoteOption*)null);
	}

	internal bool CanReactionInterruptCurrentState(ReactionOptions? reactionOptions)
	{
		if (reactionOptions == null)
		{
			return true;
		}
		if (reactionOptions.StateConditions.HasFlag(StateConditionType.Moving) && IsMoving)
		{
			return false;
		}
		if (reactionOptions.StateConditions.HasFlag(StateConditionType.LoopingEmote) && IsLoopingEmote)
		{
			return false;
		}
		if (reactionOptions.StateConditions.HasFlag(StateConditionType.Emote) && IsEmote)
		{
			return false;
		}
		if (reactionOptions.StateConditions.HasFlag(StateConditionType.Standing) && IsStandingIdle)
		{
			return false;
		}
		if (reactionOptions.StateConditions.HasFlag(StateConditionType.GroundSit) && IsGroundSitting)
		{
			return false;
		}
		if (reactionOptions.StateConditions.HasFlag(StateConditionType.ChairSit) && IsChairSitting)
		{
			return false;
		}
		if (reactionOptions.StateConditions.HasFlag(StateConditionType.Sleeping) && IsSleeping)
		{
			return false;
		}
		return true;
	}

	internal void Validate(IGameObject o)
	{
		GameObject = o;
	}

	internal void SetAsTarget()
	{
		if (IsValid)
		{
			GameObject.SetAsTarget();
		}
	}

	internal void SetAsMouseOverTarget()
	{
		if (IsValid)
		{
			GameObject.SetAsMouseOverTarget();
		}
	}

	internal void SetAsFocusTarget()
	{
		if (IsValid)
		{
			GameObject.SetAsFocusTarget();
		}
	}

	internal void SetAsSoftTarget()
	{
		if (IsValid)
		{
			GameObject.SetAsSoftTarget();
		}
	}

	internal bool IsTargetOf(IGameObject o)
	{
		return o.TargetObjectId == ObjectId;
	}

	internal unsafe void OpenPlate()
	{
		AgentCharaCard.Instance()->OpenCharaCard(GameObjectPtr);
	}

	internal unsafe void OpenExamine()
	{
		AgentInspect.Instance()->ExamineCharacter(GameObject.EntityId, false);
	}

	internal unsafe void SendTell()
	{
		UIModule.Instance()->ProcessChatBoxEntry(Utf8String.FromString("/tell " + Name + "@" + HomeWorld), (IntPtr)0, false);
	}

	internal unsafe void InviteToParty()
	{
		InfoProxyPartyInvite.Instance()->InviteToParty(CharacterPtr->ContentId, CharacterPtr->GetName(), CharacterPtr->HomeWorld);
	}

	internal string GetRegionCode(string worldName)
	{
		ExcelSheet<World> worldSheet = Plugin.DataManager.GetExcelSheet<World>((ClientLanguage?)null, (string)null);
		if (!this.TryGetFirst<World>((IEnumerable<World>)worldSheet, (Predicate<World>)delegate(World x)
		{
			return string.Equals(x.Name.ToString(), worldName, StringComparison.InvariantCultureIgnoreCase);
		}, out World world) || !IsWorldValid(world))
		{
			return string.Empty;
		}
		return GetRegionCode(world);
	}

	internal unsafe bool IsWorldValid(World world)
	{
		string name = world.Name.ToString() ?? string.Empty;
		if (string.IsNullOrEmpty(name) || GetRegionCode(world) == string.Empty)
		{
			return false;
		}
		return char.IsUpper(name[0]);
	}

	internal string GetRegionCode(World world)
	{
		WorldDCGroupType? valueNullable = world.DataCenter.ValueNullable;
		uint? num;
		if (!valueNullable.HasValue)
		{
			num = null;
		}
		else
		{
			WorldDCGroupType valueOrDefault = valueNullable.GetValueOrDefault();
			num = valueOrDefault.Region.RowId;
		}
		return num switch
		{
			1u => "jp", 
			2u => "na", 
			3u => "eu", 
			4u => "eu", 
			_ => string.Empty, 
		};
	}

	internal bool TryGetFirst<T>(IEnumerable<T> values, Predicate<T> predicate, out T result) where T : struct
	{
		using IEnumerator<T> e = values.GetEnumerator();
		while (e.MoveNext())
		{
			if (predicate(e.Current))
			{
				result = e.Current;
				return true;
			}
		}
		result = default(T);
		return false;
	}
}
