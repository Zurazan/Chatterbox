using System;
using System.Linq;
using System.Runtime.CompilerServices;
using FFXIVClientStructs.FFXIV.Common.Math;

namespace Chatterbox;

public class RestoreContext
{
	private Plugin plugin;

	[CompilerGenerated]
	private readonly Vector3 _003CPosition_003Ek__BackingField;

	public RestoreType Type { get; }

	public ushort? EmoteId { get; }

	public EntityInfo? Target { get; }

	public double Rotation { get; }

	public Vector3 Position
	{
		[CompilerGenerated]
		get
		{
			return _003CPosition_003Ek__BackingField;
		}
	}

	public RestoreContext(Plugin plugin, RestoreType type)
	{
		this.plugin = plugin;
		Type = type;
		EntityInfo lp = PlayerManager.LocalPlayer;
		if (lp != null)
		{
			EmoteId = (lp.IsLoopingEmote ? new ushort?(lp.EmoteId) : ((ushort?)null));
			Target = (lp.IsTargetValid ? new EntityInfo(lp.Target) : null);
			Rotation = lp.Angle;
			_003CPosition_003Ek__BackingField = lp.Position;
		}
	}

	public void Restore()
	{
		if (plugin.IsDisposed)
		{
			return;
		}
		Plugin.Framework.RunOnFrameworkThread((Action)delegate
		{
			if (plugin.IsDisposed)
			{
				return;
			}
			EntityInfo localPlayer = PlayerManager.LocalPlayer;
			if (localPlayer != null)
			{
				if (Type.HasFlag(RestoreType.Emote) && EmoteId.HasValue)
				{
					Emote emote = plugin.Emotes.FirstOrDefault((Emote x) => x.ID == EmoteId);
					if (emote != null && !emote.IsPose)
					{
						try
						{
							Game.ForceDisableMovement++;
							localPlayer.SetEmote(emote.ID);
						}
						finally
						{
							Game.ForceDisableMovement--;
						}
					}
				}
				if (Type.HasFlag(RestoreType.Target))
				{
					if (Target != null && Target.IsValid)
					{
						Target.SetAsTarget();
					}
					else
					{
						Plugin.Targets.Target = null;
					}
				}
				if (Type.HasFlag(RestoreType.Rotation))
				{
					localPlayer.SetRotation((float)Rotation);
				}
				Type.HasFlag(RestoreType.Position);
			}
		});
	}
}
