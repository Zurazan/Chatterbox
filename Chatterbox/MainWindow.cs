using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using Newtonsoft.Json;
using Action = System.Action;

namespace Chatterbox;

public class MainWindow : Window
{
	private readonly Plugin plugin;

	private int SelectedTriggerIndex;

	private bool IsComboOpen_TriggerEmotes;

	private bool IsComboOpen_ReactionEmotes;

	private string TriggerFilter = "";

	private bool DrawRangePreview;

	private float RangePreviewOpacity;

	private static bool ResidentialOnly = true;

	private static List<(uint Id, string Name, bool IsResidential)> TerritoryUiList = new List<(uint, string, bool)>();

	private static readonly StatusType[] StatusTypes = (from field in typeof(StatusType).GetFields(BindingFlags.Static | BindingFlags.Public)
		orderby field.MetadataToken
		select field.GetValue(null)).OfType<StatusType>().ToArray();

	private Trigger? SelectedTrigger
	{
		get
		{
			if (SelectedTriggerIndex < 0 || SelectedTriggerIndex >= Plugin.Config.Triggers.Count)
			{
				return null;
			}
			return Plugin.Config.Triggers[SelectedTriggerIndex];
		}
	}

	public MainWindow(Plugin plugin) : base("Chatterbox v1.2.1.0 | Trigger Studio")
	{
		SelectedTriggerIndex = -1;
		RangePreviewOpacity = 0.2f;
		this.plugin = plugin;
		((Window)this).SizeCondition = (ImGuiCond)4;
		((Window)this).Size = new Vector2(780f, 640f) * ImGuiHelpers.GlobalScale;
		WindowSizeConstraints value = default(WindowSizeConstraints);
		value.MinimumSize = new Vector2(560f, 440f) * ImGuiHelpers.GlobalScale;
		((Window)this).SizeConstraints = value;
	}

	public override void Draw()
	{
		if (!((Window)this).IsOpen)
		{
			return;
		}
		ChatterboxTheme.Push();
		try
		{
			if (Plugin.Config.Triggers.Count > 0 && SelectedTriggerIndex == -1)
			{
				SelectedTriggerIndex = 0;
			}
			else if (Plugin.Config.Triggers.Count == 0)
			{
				SelectedTriggerIndex = -1;
			}
			ChatterboxTheme.DrawBanner("CHATTERBOX", "Trigger Studio", "Build expressive emote and chat reactions without leaving the familiar workflow.");
			DrawHeader();
			DrawTriggersList();
		}
		finally
		{
			ChatterboxTheme.Pop();
		}
	}

	private void DrawHeader()
	{
		bool hasSelectedTrigger = SelectedTrigger != null;
		Vector4 workspaceColor = ChatterboxTheme.Accent;
		ImGui.TextColored(in workspaceColor, new ImU8String("WORKSPACE"));
		ImGui.SameLine();
		Vector4 countColor = ChatterboxTheme.Muted;
		ImGui.TextColored(in countColor, new ImU8String($"{Plugin.Config.Triggers.Count} trigger{(Plugin.Config.Triggers.Count == 1 ? "" : "s")}"));
		ImGui.Spacing();
		if (ImGui.Button(new ImU8String("+ New Trigger"), default(Vector2)))
		{
			ImGui.OpenPopup(new ImU8String("##newTrigger"), (ImGuiPopupFlags)0);
		}
		ImGuiEx.SetItemTooltip("Create new trigger.", (ImGuiHoveredFlags)0);
		if (ImGui.BeginPopup(new ImU8String("##newTrigger"), (ImGuiWindowFlags)0))
		{
			if (ImGui.Selectable(new ImU8String("Create Empty Trigger"), false, (ImGuiSelectableFlags)0, default(Vector2)))
			{
				Trigger newTrigger = new Trigger();
				Plugin.Config.Triggers.Add(newTrigger);
				Plugin.Config.Save();
			}
			ImGui.Spacing();
			ImGui.Separator();
			ImGui.Spacing();
			ImGui.PushStyleColor(ImGuiCol.Text, ChatterboxTheme.Accent);
			ImGui.Selectable(new ImU8String("Create from Preset:"), false, (ImGuiSelectableFlags)1, default(Vector2));
			ImGui.PopStyleColor();
			ImGui.Separator();
			if (ImGui.Selectable(new ImU8String("Hug Counter"), false, (ImGuiSelectableFlags)0, default(Vector2)))
			{
				Trigger t = new Trigger();
				t.Enabled = true;
				t.Name = "Hug Counter";
				t.Description = "Count & display title when receiving a hug.";
				t.Type = TriggerType.Emote;
				EmoteAction emoteAction = new EmoteAction();
				int num = 2;
				List<ushort> list = new List<ushort>(num);
				CollectionsMarshal.SetCount(list, num);
				Span<ushort> span = CollectionsMarshal.AsSpan(list);
				span[0] = 112;
				span[1] = 113;
				emoteAction.IDs = list;
				t.ReceivedAction = emoteAction;
				t.Instigator = new Instigator
				{
					Type = PlayerType.Others
				};
				t.Receiver = new EmoteTargetReceiver
				{
					Type = PlayerType.Self
				};
				t.Counter = new Counter
				{
					DisplayTitle = true,
					TitleTemplate = "Hugged x%n%"
				};
				t.ReactionOptions = new ReactionOptions
				{
					ReactionCooldown = 0,
					InterruptType = ReactionInterruptType.Any,
					StateConditions = StateConditionType.None,
					RestoreType = RestoreType.None,
					RestrictRange = true,
					RestrictedDistanceMin = 0f,
					RestrictedDistanceMax = 0.5f,
					RestrictedAngleDirection = 0,
					RestrictedAngleArea = 1f
				};
				t.Reactions = null;
				Plugin.Config.Triggers.Add(t);
				Plugin.Config.Save();
			}
			ImGuiEx.SetItemTooltip("Setup a trigger for counting the number of hugs received & display in Honorific title.\n\n- Requires 'Honorific' plugin.", (ImGuiHoveredFlags)0);
			if (ImGui.Selectable(new ImU8String("Pat Counter"), false, (ImGuiSelectableFlags)0, default(Vector2)))
			{
				Trigger t2 = new Trigger();
				t2.Enabled = true;
				t2.Name = "Pat Counter";
				t2.Description = "Count & display title when receiving a pat.";
				t2.Type = TriggerType.Emote;
				EmoteAction emoteAction2 = new EmoteAction();
				int num = 1;
				List<ushort> list2 = new List<ushort>(num);
				CollectionsMarshal.SetCount(list2, num);
				CollectionsMarshal.AsSpan(list2)[0] = 105;
				emoteAction2.IDs = list2;
				t2.ReceivedAction = emoteAction2;
				t2.Instigator = new Instigator
				{
					Type = PlayerType.Others
				};
				t2.Receiver = new EmoteTargetReceiver
				{
					Type = PlayerType.Self
				};
				t2.Counter = new Counter
				{
					DisplayTitle = true,
					TitleTemplate = "Patted x%n%"
				};
				t2.ReactionOptions = new ReactionOptions
				{
					ReactionCooldown = 0,
					InterruptType = ReactionInterruptType.Any,
					StateConditions = StateConditionType.None,
					RestoreType = RestoreType.None,
					RestrictRange = true,
					RestrictedDistanceMin = 0f,
					RestrictedDistanceMax = 0.6f,
					RestrictedAngleDirection = 0,
					RestrictedAngleArea = 1f
				};
				t2.Reactions = null;
				Plugin.Config.Triggers.Add(t2);
				Plugin.Config.Save();
			}
			ImGuiEx.SetItemTooltip("Setup a trigger for counting the number of pats received & display in Honorific title.\n\n- Requires 'Honorific' plugin.", (ImGuiHoveredFlags)0);
			if (ImGui.Selectable(new ImU8String("Dote Counter"), false, (ImGuiSelectableFlags)0, default(Vector2)))
			{
				Trigger t3 = new Trigger();
				t3.Enabled = true;
				t3.Name = "Dote Counter";
				t3.Description = "Count & display title when receiving a dote/kiss.";
				t3.Type = TriggerType.Emote;
				EmoteAction emoteAction3 = new EmoteAction();
				int num = 3;
				List<ushort> list3 = new List<ushort>(num);
				CollectionsMarshal.SetCount(list3, num);
				Span<ushort> span2 = CollectionsMarshal.AsSpan(list3);
				span2[0] = 46;
				span2[1] = 146;
				span2[2] = 147;
				emoteAction3.IDs = list3;
				t3.ReceivedAction = emoteAction3;
				t3.Instigator = new Instigator
				{
					Type = PlayerType.Others
				};
				t3.Receiver = new EmoteTargetReceiver
				{
					Type = PlayerType.Self
				};
				t3.Counter = new Counter
				{
					DisplayTitle = true,
					TitleTemplate = "Chuu x%n%"
				};
				t3.ReactionOptions = null;
				t3.Reactions = null;
				Plugin.Config.Triggers.Add(t3);
				Plugin.Config.Save();
			}
			ImGuiEx.SetItemTooltip("Setup a trigger for counting the number of dotes received & display in Honorific title.\n\n- Requires 'Honorific' plugin.", (ImGuiHoveredFlags)0);
			if (ImGui.Selectable(new ImU8String("Mimic Emotes"), false, (ImGuiSelectableFlags)0, default(Vector2)))
			{
				Trigger t4 = new Trigger();
				t4.Enabled = true;
				t4.Name = "Mimic Emotes";
				t4.Description = "Copy emotes that other players use while targeting you.";
				t4.Type = TriggerType.Emote;
				t4.ReceivedAction = new EmoteAction
				{
					IDs = new List<ushort>(),
					MatchAny = true
				};
				t4.Instigator = new Instigator
				{
					Type = PlayerType.Others
				};
				t4.Receiver = new EmoteTargetReceiver
				{
					Type = PlayerType.Self
				};
				t4.Counter = null;
				t4.ReactionOptions = new ReactionOptions
				{
					ReactionCooldown = 0,
					InterruptType = ReactionInterruptType.None,
					StateConditions = (StateConditionType.Moving | StateConditionType.Sleeping | StateConditionType.Emote),
					RestoreType = RestoreType.None
				};
				int num = 1;
				List<ReactionBase> list4 = new List<ReactionBase>(num);
				CollectionsMarshal.SetCount(list4, num);
				CollectionsMarshal.AsSpan(list4)[0] = new EmoteReaction
				{
					Duration = 1500,
					ID = 0,
					CopyInstigator = true,
					TargetType = ReactionTargetType.TargetInstigator,
					LookAtType = ReactionLookAtType.Target
				};
				t4.Reactions = list4;
				Plugin.Config.Triggers.Add(t4);
				Plugin.Config.Save();
			}
			ImGuiEx.SetItemTooltip("Setup a trigger for copying emotes that other players use while targeting you.", (ImGuiHoveredFlags)0);
			if (ImGui.Selectable(new ImU8String("Spank Reaction"), false, (ImGuiSelectableFlags)0, default(Vector2)))
			{
				Trigger t5 = new Trigger();
				t5.Enabled = true;
				t5.Name = "Spank Reaction";
				t5.Description = "React to being spanked & display title.";
				t5.Type = TriggerType.Emote;
				EmoteAction emoteAction4 = new EmoteAction();
				int num = 1;
				List<ushort> list5 = new List<ushort>(num);
				CollectionsMarshal.SetCount(list5, num);
				CollectionsMarshal.AsSpan(list5)[0] = 213;
				emoteAction4.IDs = list5;
				t5.ReceivedAction = emoteAction4;
				t5.Instigator = new Instigator
				{
					Type = PlayerType.Others
				};
				t5.Receiver = new EmoteTargetReceiver
				{
					Type = PlayerType.Self
				};
				t5.Counter = new Counter
				{
					DisplayTitle = true,
					TitleTemplate = "Spank Count: %n%"
				};
				t5.ReactionOptions = new ReactionOptions
				{
					ReactionCooldown = 0,
					InterruptType = ReactionInterruptType.Same,
					StateConditions = (StateConditionType.Moving | StateConditionType.GroundSit | StateConditionType.ChairSit | StateConditionType.Sleeping),
					RestoreType = RestoreType.Emote,
					RestrictRange = true,
					RestrictedDistanceMin = 0.1f,
					RestrictedDistanceMax = 0.5f,
					RestrictedAngleDirection = 180,
					RestrictedAngleArea = 0.35f
				};
				num = 1;
				List<ReactionBase> list6 = new List<ReactionBase>(num);
				CollectionsMarshal.SetCount(list6, num);
				CollectionsMarshal.AsSpan(list6)[0] = new EmoteReaction
				{
					Duration = 1500,
					ID = 32,
					LookAtType = ReactionLookAtType.Maintain
				};
				t5.Reactions = list6;
				Plugin.Config.Triggers.Add(t5);
				Plugin.Config.Save();
			}
			ImGuiEx.SetItemTooltip("Setup a trigger for reacting to being spanked.\n\n- Requires 'Spanked Reaction' mod or similar, replacing the 'Shocked' emote.", (ImGuiHoveredFlags)0);
			if (ImGui.Selectable(new ImU8String("Return Dote"), false, (ImGuiSelectableFlags)0, default(Vector2)))
			{
				Trigger t6 = new Trigger();
				t6.Enabled = true;
				t6.Name = "Return Dote";
				t6.Description = "Respond with a dote when receiving a dote.";
				t6.Type = TriggerType.Emote;
				EmoteAction emoteAction5 = new EmoteAction();
				int num = 3;
				List<ushort> list7 = new List<ushort>(num);
				CollectionsMarshal.SetCount(list7, num);
				Span<ushort> span3 = CollectionsMarshal.AsSpan(list7);
				span3[0] = 46;
				span3[1] = 146;
				span3[2] = 147;
				emoteAction5.IDs = list7;
				t6.ReceivedAction = emoteAction5;
				t6.Instigator = new Instigator
				{
					Type = PlayerType.Others
				};
				t6.Receiver = new EmoteTargetReceiver
				{
					Type = PlayerType.Self
				};
				t6.Counter = new Counter
				{
					DisplayTitle = true,
					TitleTemplate = "Chuu x%n%"
				};
				t6.ReactionOptions = new ReactionOptions
				{
					ReactionCooldown = 0,
					InterruptType = ReactionInterruptType.None,
					StateConditions = (StateConditionType.Moving | StateConditionType.GroundSit | StateConditionType.ChairSit | StateConditionType.Sleeping | StateConditionType.Emote | StateConditionType.LoopingEmote),
					RestoreType = RestoreType.None,
					RestrictRange = true,
					RestrictedDistanceMin = 0f,
					RestrictedDistanceMax = 17f,
					RestrictedAngleDirection = 0,
					RestrictedAngleArea = 1f
				};
				num = 1;
				List<ReactionBase> list8 = new List<ReactionBase>(num);
				CollectionsMarshal.SetCount(list8, num);
				CollectionsMarshal.AsSpan(list8)[0] = new EmoteReaction
				{
					Duration = 1500,
					ID = 146,
					TargetType = ReactionTargetType.TargetInstigator,
					LookAtType = ReactionLookAtType.Target
				};
				t6.Reactions = list8;
				Plugin.Config.Triggers.Add(t6);
				Plugin.Config.Save();
			}
			ImGuiEx.SetItemTooltip("Setup a trigger for responding with a dote when receiving a dote within a limited range.", (ImGuiHoveredFlags)0);
			ImGui.EndPopup();
		}
		ImGui.SameLine();
		if (ImGuiEx.IconButton((FontAwesomeIcon)61610, "movetriggerup") & hasSelectedTrigger)
		{
			Trigger trigger = SelectedTrigger;
			if (trigger != null)
			{
				Plugin.Config.Triggers.RemoveAt(SelectedTriggerIndex);
				SelectedTriggerIndex = Math.Max(SelectedTriggerIndex - 1, 0);
				Plugin.Config.Triggers.Insert(SelectedTriggerIndex, trigger);
				Plugin.Config.Save();
			}
		}
		ImGuiEx.SetItemTooltip("Move selected trigger up.", (ImGuiHoveredFlags)0);
		ImGui.SameLine();
		if (ImGuiEx.IconButton((FontAwesomeIcon)61611, "movetriggerdown") & hasSelectedTrigger)
		{
			Trigger trigger2 = SelectedTrigger;
			if (trigger2 != null)
			{
				Plugin.Config.Triggers.RemoveAt(SelectedTriggerIndex);
				SelectedTriggerIndex = Math.Min(SelectedTriggerIndex + 1, Plugin.Config.Triggers.Count);
				Plugin.Config.Triggers.Insert(SelectedTriggerIndex, trigger2);
				Plugin.Config.Save();
			}
		}
		ImGuiEx.SetItemTooltip("Move selected trigger down.", (ImGuiHoveredFlags)0);
		ImGui.SameLine();
		ImGuiEx.IconButton((FontAwesomeIcon)62189, "removetrigger");
		ImGuiEx.SetItemTooltip("Remove selected trigger.", (ImGuiHoveredFlags)0);
		if (hasSelectedTrigger && ImGui.BeginPopupContextItem(new ImU8String("##removeContext"), (ImGuiPopupFlags)0))
		{
			if (ImGuiEx.IconSelectable((FontAwesomeIcon)62189))
			{
				Plugin.Config.Triggers.RemoveAt(SelectedTriggerIndex);
				SelectedTriggerIndex = Math.Min(SelectedTriggerIndex, Plugin.Config.Triggers.Count - 1);
				Plugin.Config.Save();
			}
			ImGui.EndPopup();
		}
		ImGui.SameLine();
		if (ImGuiEx.IconButton((FontAwesomeIcon)61637, "copytrigger") & hasSelectedTrigger)
		{
			Trigger trigger3 = SelectedTrigger;
			ImGui.SetClipboardText(new ImU8String(CompressToBase64(JsonConvert.SerializeObject((object)trigger3, Plugin.Converters.ToArray()))));
		}
		ImGuiEx.SetItemTooltip("Copy the selected trigger to clipboard.", (ImGuiHoveredFlags)0);
		ImGui.SameLine();
		if (ImGuiEx.IconButton((FontAwesomeIcon)61674, "pastetrigger") && TryImportTrigger<Trigger>(ImGui.GetClipboardText().Trim(), out Trigger trigger4) && trigger4 != null)
		{
			trigger4.Guid = Guid.NewGuid();
			trigger4.Enabled = false;
			trigger4.UseSharedCounter = false;
			if (trigger4.Counter is Counter counter)
			{
				counter.Amount = 0;
			}
			else
			{
				trigger4.Counter = new Counter();
			}
			Plugin.Config.Triggers.Add(trigger4);
			Plugin.Config.Save();
		}
		ImGuiEx.SetItemTooltip("Paste trigger from clipboard.", (ImGuiHoveredFlags)0);
		ImGui.SameLine();
		if (ImGui.Checkbox(new ImU8String("Enable Plugin"), ref Plugin.Config.Enabled))
		{
			Plugin.Config.Save();
		}
		ImGui.Spacing();
		ImGui.Separator();
		ImGui.Spacing();
	}

	private string CompressToBase64(string input)
	{
		byte[] inputBytes = Encoding.UTF8.GetBytes(input);
		using MemoryStream output = new MemoryStream();
		using (GZipStream gzip = new GZipStream(output, CompressionMode.Compress))
		{
			gzip.Write(inputBytes, 0, inputBytes.Length);
		}
		return Convert.ToBase64String(output.ToArray());
	}

	private string DecompressFromBase64(string base64)
	{
		using MemoryStream input = new MemoryStream(Convert.FromBase64String(base64));
		using GZipStream gzip = new GZipStream(input, CompressionMode.Decompress);
		using MemoryStream output = new MemoryStream();
		gzip.CopyTo(output);
		return Encoding.UTF8.GetString(output.ToArray());
	}

	private bool TryImportTrigger<Trigger>(string base64, out Trigger? result)
	{
		result = default(Trigger);
		if (string.IsNullOrWhiteSpace(base64))
		{
			return false;
		}
		string json;
		try
		{
			json = DecompressFromBase64(base64);
		}
		catch
		{
			return false;
		}
		try
		{
			Trigger obj2 = JsonConvert.DeserializeObject<Trigger>(json, Plugin.Converters.ToArray());
			if (obj2 == null)
			{
				return false;
			}
			if (obj2 != null)
			{
				result = obj2;
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	private void DrawTriggersList()
	{
		float scale = ImGuiHelpers.GlobalScale;
		ImGui.BeginChild(new ImU8String("ChatterboxTriggerRail"), new Vector2(210f * scale, 0f), true, (ImGuiWindowFlags)0);
		Vector4 accent = ChatterboxTheme.Accent;
		ImGui.TextColored(in accent, new ImU8String("TRIGGER LIBRARY"));
		Vector4 muted = ChatterboxTheme.Muted;
		int enabledCount = Plugin.Config.Triggers.Count((Trigger trigger) => trigger.Enabled);
		ImGui.TextColored(in muted, new ImU8String($"{enabledCount} enabled / {Plugin.Config.Triggers.Count} total"));
		ImGui.Spacing();
		ImGui.SetNextItemWidth(-1f);
		ImGui.InputTextWithHint(new ImU8String("##triggerFilter"), new ImU8String("Search triggers..."), ref TriggerFilter, 64, (ImGuiInputTextFlags)0, (ImGui.ImGuiInputTextCallbackDelegate)null);
		ImGui.Spacing();
		if (Plugin.Config.Triggers.Count == 0)
		{
			Vector4 highlight = ChatterboxTheme.Highlight;
		ImGui.TextColored(in highlight, new ImU8String("No triggers yet"));
			ImGui.TextWrapped(new ImU8String("Create one from a preset or start with an empty trigger."));
		}
		else
		{
			bool foundMatch = false;
			for (int i = 0; i < Plugin.Config.Triggers.Count; i++)
			{
				Trigger trigger = Plugin.Config.Triggers[i];
				if (!string.IsNullOrWhiteSpace(TriggerFilter) && !trigger.Name.Contains(TriggerFilter, StringComparison.OrdinalIgnoreCase) && !trigger.Description.Contains(TriggerFilter, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				foundMatch = true;
				bool isActive = trigger.Enabled;
				ImGui.PushID((IntPtr)i);
				Vector4 triggerColor = isActive ? ChatterboxTheme.Text : ChatterboxTheme.Muted;
				ImGui.PushStyleColor((ImGuiCol)0, triggerColor);
				string status = isActive ? "ON " : "OFF";
				string label = $"{status}  {trigger.Name}\n      {trigger.Type} event##trigger{i}";
				if (ImGui.Selectable(new ImU8String(label), SelectedTriggerIndex == i, (ImGuiSelectableFlags)0, new Vector2(0f, 42f * scale)))
				{
					SelectedTriggerIndex = i;
				}
				ImGui.PopStyleColor();
				if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked((ImGuiMouseButton)0))
				{
					trigger.Enabled = !isActive;
					Plugin.Config.Save();
				}
				ImGui.PopID();
			}
			if (!foundMatch)
			{
				ImGui.TextWrapped(new ImU8String("No triggers match this search."));
			}
		}
		ImGui.EndChild();
		ImGui.SameLine();
		ImGui.BeginChild(new ImU8String("ChatterboxEditor"), Vector2.Zero, true, (ImGuiWindowFlags)0);
		Trigger? selectedTrigger = SelectedTrigger;
		if (selectedTrigger != null)
		{
			Vector4 editorAccent = ChatterboxTheme.Accent;
		ImGui.TextColored(in editorAccent, new ImU8String("TRIGGER EDITOR"));
			ImGui.SameLine();
			Vector4 eventColor = ChatterboxTheme.Highlight;
		ImGui.TextColored(in eventColor, new ImU8String($"{selectedTrigger.Type.ToString().ToUpperInvariant()} EVENT"));
			ImGui.Separator();
			DrawTriggerEditor(selectedTrigger);
		}
		else
		{
			ImGui.Dummy(new Vector2(0f, 28f * scale));
			Vector4 emptyColor = ChatterboxTheme.Accent;
		ImGui.TextColored(in emptyColor, new ImU8String("SELECT A TRIGGER"));
			ImGui.TextWrapped(new ImU8String("Choose a trigger from the library to edit its event, conditions, counter, and reactions."));
		}
		ImGui.EndChild();
	}

	private void DrawTriggerEditor(Trigger? trigger)
	{
		if (trigger == null)
		{
			return;
		}
		if (ImGuiEx.Checkbox($"Enable Trigger##enable{SelectedTriggerIndex}", trigger.Enabled, delegate(bool x)
		{
			trigger.Enabled = x;
		}))
		{
			Plugin.Config.Save();
		}
		ImGuiEx.SetItemTooltip("Enable this trigger.", (ImGuiHoveredFlags)0);
		ImGui.SameLine();
		ImGuiIOPtr iO = ImGui.GetIO();
		ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
		if (ImGuiEx.InputTextWithHint("##triggerName", "Trigger Name", trigger.Name, delegate(string x)
		{
			trigger.Name = x;
		}, 64))
		{
			Plugin.Config.Save();
		}
		ImGuiEx.SetItemTooltip("Name which describes this trigger's function.", (ImGuiHoveredFlags)0);
		iO = ImGui.GetIO();
		ImGui.SetNextItemWidth(276f * iO.FontGlobalScale);
		if (ImGuiEx.InputTextWithHint("##triggerDesc", "Trigger Description", trigger.Description, delegate(string x)
		{
			trigger.Description = x;
		}, 500))
		{
			Plugin.Config.Save();
		}
		ImGuiEx.SetItemTooltip("Optional description to further detail this trigger's function.", (ImGuiHoveredFlags)0);
		ImGui.Spacing();
		Vector2 regionSize = ImGui.GetContentRegionAvail();
		ImU8String val = new ImU8String("##triggerEditor");
		Vector2 vector = regionSize;
		vector.X = regionSize.X * 1f;
		var val2 = ImRaii.Child(val, vector);
		try
		{
			DrawTriggerEvent(trigger);
			DrawTriggerInstigator(trigger);
			DrawTriggerReceiver(trigger);
			DrawTriggerCounter(trigger);
			DrawReactionOptions(trigger);
			DrawTriggerReactionQueue(trigger);
		}
		finally
		{
			val2.Dispose();
		}
	}

	private void DrawTriggerEvent(Trigger trigger)
	{
		if (!ImGuiEx.TreeNode(((trigger.Type == TriggerType.None) ? "Trigger Event - Event Type must be set." : "Trigger Event") + "##triggerEvent", null, (trigger.Type == TriggerType.None) ? ImGuiColors.DalamudRed : default(Vector4), (ImGuiTreeNodeFlags)0))
		{
			return;
		}
		ImGuiIOPtr iO = ImGui.GetIO();
		ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
		ImU8String val = new ImU8String("##triggerType");
		ImU8String val2 = default(ImU8String);
		val2 = new ImU8String(12, 1);
		val2.AppendLiteral("Event Type: ");
		val2.AppendFormatted<TriggerType>(trigger.Type);
		if (ImGui.BeginCombo(val, val2, (ImGuiComboFlags)0))
		{
			foreach (TriggerType value in Enum.GetValues(typeof(TriggerType)))
			{
				bool selected = trigger.Type == value;
				ImU8String val3 = new ImU8String(0, 1);
				val3.AppendFormatted<TriggerType>(value);
				if (ImGui.Selectable(val3, selected, (ImGuiSelectableFlags)0, default(Vector2)))
				{
					trigger.Type = value;
					Plugin.Config.Save();
				}
			}
			ImGui.EndCombo();
		}
		ImGuiEx.SetItemTooltip("Emote: React when an instigator performs selected emotes.\nText: React when a chat message contains selected words or phrases.", (ImGuiHoveredFlags)0);
		ImGui.Spacing();
		if (trigger.Type == TriggerType.Emote)
		{
			if (!(trigger.ReceivedAction is EmoteAction))
			{
				trigger.ReceivedAction = new EmoteAction();
				Plugin.Config.Save();
			}
			EmoteAction action = (EmoteAction)trigger.ReceivedAction;
			ImGui.BeginDisabled(action.MatchAny);
			iO = ImGui.GetIO();
			ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
			if (ImGui.BeginCombo(new ImU8String("##triggerEmotes"), new ImU8String(action.MatchAny ? "All Emotes Selected" : $"{action.IDs.Count} Emotes Selected"), (ImGuiComboFlags)0))
			{
				if (!IsComboOpen_TriggerEmotes)
				{
					IsComboOpen_TriggerEmotes = true;
					plugin.Emotes = plugin.Emotes.OrderByDescending((Emote emote2) => action.IDs.Contains(emote2.ID)).ThenBy<Emote, string>((Emote emote2) => emote2.Name, StringComparer.OrdinalIgnoreCase).ToList();
				}
				foreach (Emote emote in plugin.Emotes)
				{
					if (!emote.TriggersEmoteHook)
					{
						continue;
					}
					bool selected2 = action.IDs.Contains(emote.ID);
					ImGuiEx.IconCheckbox(selected2);
					ImGui.SameLine();
					if (ImGui.Selectable(new ImU8String(emote.ToString()), selected2, (ImGuiSelectableFlags)1, default(Vector2)))
					{
						if (selected2)
						{
							action.IDs.Remove(emote.ID);
						}
						else
						{
							action.IDs.Add(emote.ID);
						}
						Plugin.Config.Save();
					}
				}
				ImGui.EndCombo();
			}
			else if (IsComboOpen_TriggerEmotes)
			{
				IsComboOpen_TriggerEmotes = false;
			}
			ImGui.EndDisabled();
			ImGuiEx.SetItemTooltip("Select the emotes that will trigger counter/reactions.", (ImGuiHoveredFlags)0);
			ImGui.SameLine();
			if (ImGuiEx.Checkbox("Any##matchAnyEmotes", action.MatchAny, delegate(bool x)
			{
				action.MatchAny = x;
			}))
			{
				Plugin.Config.Save();
			}
			ImGuiEx.SetItemTooltip("Allow any emote to trigger counter/reactions.\nThis is useful if you'd like to mimic any received emote.", (ImGuiHoveredFlags)0);
		}
		else if (trigger.Type == TriggerType.Text)
		{
			if (!(trigger.ReceivedAction is TextAction))
			{
				trigger.ReceivedAction = new TextAction();
				Plugin.Config.Save();
			}
			TextAction action2 = (TextAction)trigger.ReceivedAction;
			iO = ImGui.GetIO();
			ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
			ImU8String val4 = new ImU8String("##triggerTexts");
			ImU8String val5 = default(ImU8String);
			val5 = new ImU8String(16, 1);
			val5.AppendFormatted<int>(action2.Inputs.Count);
			val5.AppendLiteral(" Inputs to Match");
			if (ImGui.BeginCombo(val4, val5, (ImGuiComboFlags)0))
			{
				Action removeAction = null;
				ImU8String val6 = default(ImU8String);
				ImU8String val7 = default(ImU8String);
				for (int i = 0; i < action2.Inputs.Count; i++)
				{
					val6 = new ImU8String(10, 1);
					val6.AppendLiteral("##textItem");
					val6.AppendFormatted<int>(i);
					ImGui.PushID(val6);
					string current = action2.Inputs[i];
					if (ImGuiEx.IconButton((FontAwesomeIcon)61944, $"##removeText{i}"))
					{
						removeAction = delegate
						{
							action2.Inputs.Remove(current);
							Plugin.Config.Save();
						};
					}
					ImGuiEx.SetItemTooltip("Remove this entry.", (ImGuiHoveredFlags)0);
					ImGui.SameLine(0f, 0f);
					iO = ImGui.GetIO();
					ImGui.SetNextItemWidth(160f * iO.FontGlobalScale - ImGuiEx.GetIconButtonWidth((FontAwesomeIcon)61944));
					val7 = new ImU8String(11, 1);
					val7.AppendLiteral("##textInput");
					val7.AppendFormatted<int>(i);
					if (ImGui.InputText(val7, ref current, 128, (ImGuiInputTextFlags)0, (ImGui.ImGuiInputTextCallbackDelegate)null))
					{
						action2.Inputs[i] = current;
						Plugin.Config.Save();
					}
					ImGui.PopID();
				}
				removeAction?.Invoke();
				ImGui.Separator();
				ImGui.PushID(new ImU8String("##newItem"));
				string newEntry = "";
				ImGuiStylePtr style = ImGui.GetStyle();
				float num = 160f - style.FramePadding.X * 2f;
				iO = ImGui.GetIO();
				ImGui.SetNextItemWidth(num * iO.FontGlobalScale);
				if (ImGui.InputTextWithHint(new ImU8String("##newTextItem"), new ImU8String("New Input"), ref newEntry, 128, (ImGuiInputTextFlags)32, (ImGui.ImGuiInputTextCallbackDelegate)null) && !string.IsNullOrWhiteSpace(newEntry))
				{
					newEntry = newEntry.Trim();
					if (!action2.Inputs.Contains(newEntry))
					{
						action2.Inputs.Add(newEntry);
						Plugin.Config.Save();
					}
				}
				ImGuiEx.SetItemTooltip("Add a new input to match for.\nPress the Enter key to confirm entry.", (ImGuiHoveredFlags)0);
				ImGui.PopID();
				ImGui.EndCombo();
			}
			ImGuiEx.SetItemTooltip("One or more words/phrases to match for in received chat messages that will trigger counter/reactions.", (ImGuiHoveredFlags)0);
			if (action2.Inputs.Count > 1)
			{
				ImGui.SameLine();
				if (ImGuiEx.Checkbox("All Required##matchAllText", action2.MatchAll, delegate(bool x)
				{
					action2.MatchAll = x;
				}))
				{
					Plugin.Config.Save();
				}
				ImGuiEx.SetItemTooltip("Require all inputs be present in a message to trigger counter/reactions.", (ImGuiHoveredFlags)0);
			}
			ImGui.SameLine();
			if (ImGuiEx.Checkbox("Case Sensitive##caseSensitiveText", action2.CaseSensitive, delegate(bool x)
			{
				action2.CaseSensitive = x;
			}))
			{
				Plugin.Config.Save();
			}
			ImGuiEx.SetItemTooltip("Whether matched inputs are case sensitive.", (ImGuiHoveredFlags)0);
		}
		ImGui.TreePop();
	}

	private void DrawTriggerInstigator(Trigger trigger)
	{
		Instigator? instigator = trigger.Instigator;
		string text = ((instigator != null && instigator.Type == PlayerType.None) ? "Event Instigator - Instigator Type must be set." : "Event Instigator") + "##eventInstigator";
		Instigator? instigator2 = trigger.Instigator;
		if (!ImGuiEx.TreeNode(text, null, (instigator2 != null && instigator2.Type == PlayerType.None) ? ImGuiColors.DalamudRed : default(Vector4), (ImGuiTreeNodeFlags)0))
		{
			return;
		}
		if (trigger.Instigator == null)
		{
			trigger.Instigator = new Instigator();
			Plugin.Config.Save();
		}
		ImGuiIOPtr iO = ImGui.GetIO();
		ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
		ImU8String val = new ImU8String("##instigatorType");
		ImU8String val2 = default(ImU8String);
		val2 = new ImU8String(6, 1);
		val2.AppendLiteral("Type: ");
		val2.AppendFormatted<PlayerType>(trigger.Instigator.Type);
		if (ImGui.BeginCombo(val, val2, (ImGuiComboFlags)0))
		{
			foreach (PlayerType value in Enum.GetValues(typeof(PlayerType)))
			{
				if (value != PlayerType.Ignore)
				{
					bool selected = trigger.Instigator.Type == value;
					if (ImGui.Selectable(new ImU8String(value.ToString()), selected, (ImGuiSelectableFlags)0, default(Vector2)))
					{
						trigger.Instigator.Type = value;
						Plugin.Config.Save();
					}
				}
			}
			ImGui.EndCombo();
		}
		ImGuiEx.SetItemTooltip("Which type of instigating player can trigger this event.\n\nNone: Nobody can trigger this event.\nAll: Any player including yourself.\nOthers: Other players excluding yourself.\nSelf: Only you.\nPlayer: Only specific named player(s).\nTarget: Only your target.\nTargeter: Only players targeting you.", (ImGuiHoveredFlags)0);
		if (trigger.Type == TriggerType.Text)
		{
			ImGui.SameLine();
			if (trigger.Instigator.Type != PlayerType.Self && trigger.Instigator.Type != PlayerType.None && trigger.Instigator.Type != PlayerType.Target && trigger.Instigator.Type != PlayerType.Targeter)
			{
				if (ImGuiEx.Checkbox("Nearby##instigatorNearby", trigger.Instigator.RequireNearby, delegate(bool x)
				{
					trigger.Instigator.RequireNearby = x;
				}))
				{
					Plugin.Config.Save();
				}
				ImGuiEx.SetItemTooltip("Whether the player triggering this text event must be nearby (within object drawing distance).", (ImGuiHoveredFlags)0);
			}
			else
			{
				bool isTrue = true;
				ImGui.BeginDisabled(true);
				ImU8String val3 = default(ImU8String);
				val3 = new ImU8String(24, 0);
				val3.AppendLiteral("Nearby##instigatorNearby");
				ImGui.Checkbox(val3, ref isTrue);
				ImGui.EndDisabled();
				ImGuiEx.SetItemTooltip("Whether the player triggering this text event must be nearby (within object drawing distance).", (ImGuiHoveredFlags)128);
			}
		}
		ImGuiStylePtr style;
		if (trigger.Instigator.Type == PlayerType.All || trigger.Instigator.Type == PlayerType.Others || trigger.Instigator.Type == PlayerType.Target || trigger.Instigator.Type == PlayerType.Targeter || trigger.Instigator.Type == PlayerType.Player)
		{
			if (trigger.Instigator.Type == PlayerType.Player)
			{
				iO = ImGui.GetIO();
				ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
				ImU8String val4 = new ImU8String("##instigatorNames");
				ImU8String val5 = default(ImU8String);
				val5 = new ImU8String(15, 1);
				val5.AppendFormatted<int>(trigger.Instigator.Names.Count);
				val5.AppendLiteral(" Names to Match");
				if (ImGui.BeginCombo(val4, val5, (ImGuiComboFlags)0))
				{
					Action removeAction = null;
					ImU8String val6 = default(ImU8String);
					ImU8String val7 = default(ImU8String);
					for (int i = 0; i < trigger.Instigator.Names.Count; i++)
					{
						val6 = new ImU8String(10, 1);
						val6.AppendLiteral("##nameItem");
						val6.AppendFormatted<int>(i);
						ImGui.PushID(val6);
						string current = trigger.Instigator.Names[i];
						if (ImGuiEx.IconButton((FontAwesomeIcon)61944, $"##removeName{i}"))
						{
							removeAction = delegate
							{
								trigger.Instigator.Names.Remove(current);
								Plugin.Config.Save();
							};
						}
						ImGui.SameLine(0f, 0f);
						iO = ImGui.GetIO();
						ImGui.SetNextItemWidth(160f * iO.FontGlobalScale - ImGuiEx.GetIconButtonWidth((FontAwesomeIcon)61944));
						val7 = new ImU8String(11, 1);
						val7.AppendLiteral("##nameInput");
						val7.AppendFormatted<int>(i);
						if (ImGui.InputText(val7, ref current, 40, (ImGuiInputTextFlags)0, (ImGui.ImGuiInputTextCallbackDelegate)null))
						{
							trigger.Instigator.Names[i] = current;
							Plugin.Config.Save();
						}
						ImGui.PopID();
					}
					removeAction?.Invoke();
					ImGui.Separator();
					ImGui.PushID(new ImU8String("##newName"));
					string newEntry = "";
					style = ImGui.GetStyle();
					float num = 160f - style.FramePadding.X * 2f;
					iO = ImGui.GetIO();
					ImGui.SetNextItemWidth(num * iO.FontGlobalScale);
					if (ImGui.InputTextWithHint(new ImU8String("##newNameItem"), new ImU8String("Player Name"), ref newEntry, 40, (ImGuiInputTextFlags)32, (ImGui.ImGuiInputTextCallbackDelegate)null) && !string.IsNullOrWhiteSpace(newEntry))
					{
						newEntry = newEntry.Trim();
						if (!trigger.Instigator.Names.Contains(newEntry))
						{
							trigger.Instigator.Names.Add(newEntry);
							Plugin.Config.Save();
						}
					}
					ImGuiEx.SetItemTooltip("Add a new player name to match for.\nPress the Enter key to confirm entry.", (ImGuiHoveredFlags)0);
					ImGui.PopID();
					ImGui.EndCombo();
				}
				ImGuiEx.SetItemTooltip("One or more player names that can trigger this event.\nNames are not case-sensitive and should not include @World.", (ImGuiHoveredFlags)0);
			}
			iO = ImGui.GetIO();
			ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
			ImU8String val8 = new ImU8String("##instigatorCondition");
			ImU8String val9 = default(ImU8String);
			val9 = new ImU8String(10, 1);
			val9.AppendLiteral("Relation: ");
			val9.AppendFormatted<string>(ImGuiEx.EnumToSelectedCountString(trigger.Instigator.Condition, "None", ""));
			if (ImGui.BeginCombo(val8, val9, (ImGuiComboFlags)0))
			{
				foreach (PlayerCondition value2 in Enum.GetValues(typeof(PlayerCondition)))
				{
					if (value2 == PlayerCondition.None)
					{
						continue;
					}
					bool selected2 = trigger.Instigator.Condition.HasFlag(value2);
					ImGuiEx.IconCheckbox(selected2);
					ImGui.SameLine();
					ImU8String val10 = new ImU8String(0, 1);
					val10.AppendFormatted<PlayerCondition>(value2);
					if (ImGui.Selectable(val10, selected2, (ImGuiSelectableFlags)1, default(Vector2)))
					{
						if (selected2)
						{
							trigger.Instigator.Condition &= ~value2;
						}
						else
						{
							trigger.Instigator.Condition |= value2;
						}
						Plugin.Config.Save();
					}
				}
				ImGui.EndCombo();
			}
			ImGuiEx.SetItemTooltip("Restrict triggering of this event to players known to you.\nNone: No relation required.", (ImGuiHoveredFlags)0);
			if (!trigger.Instigator.RequireNearby && trigger.Instigator.Condition != PlayerCondition.None)
			{
				ImGui.SameLine();
				ImGuiEx.IconWarningTooltip("Relation condition can only be determined if the player is nearby.\nWith the above 'Nearby' option disabled, this event can still trigger without this condition being met when the player is not nearby.");
			}
			ImGui.SameLine();
			if (ImGuiEx.Checkbox("All Selected##instigatorAllConditions", trigger.Instigator.RequireAllConditions, delegate(bool x)
			{
				trigger.Instigator.RequireAllConditions = x;
			}))
			{
				Plugin.Config.Save();
			}
			ImGuiEx.SetItemTooltip("Whether a player must have all selected relations.", (ImGuiHoveredFlags)0);
			iO = ImGui.GetIO();
			ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
			ImU8String val11 = new ImU8String("##instigatorGender");
			ImU8String val12 = default(ImU8String);
			val12 = new ImU8String(8, 1);
			val12.AppendLiteral("Gender: ");
			val12.AppendFormatted<string>(ImGuiEx.EnumToSelectedCountString(trigger.Instigator.Gender, "Any", "Any"));
			if (ImGui.BeginCombo(val11, val12, (ImGuiComboFlags)0))
			{
				foreach (GenderCondition value3 in Enum.GetValues(typeof(GenderCondition)))
				{
					if (value3 == GenderCondition.Any)
					{
						continue;
					}
					bool selected3 = trigger.Instigator.Gender.HasFlag(value3);
					ImGuiEx.IconCheckbox(selected3);
					ImGui.SameLine();
					ImU8String val13 = new ImU8String(0, 1);
					val13.AppendFormatted<GenderCondition>(value3);
					if (ImGui.Selectable(val13, selected3, (ImGuiSelectableFlags)1, default(Vector2)))
					{
						if (selected3)
						{
							trigger.Instigator.Gender &= ~value3;
						}
						else
						{
							trigger.Instigator.Gender |= value3;
						}
						Plugin.Config.Save();
					}
				}
				ImGui.EndCombo();
			}
			ImGuiEx.SetItemTooltip("Restrict triggering of this event to players of specific gender.", (ImGuiHoveredFlags)0);
			iO = ImGui.GetIO();
			ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
			ImU8String val14 = new ImU8String("##instigatorRace");
			ImU8String val15 = default(ImU8String);
			val15 = new ImU8String(6, 1);
			val15.AppendLiteral("Race: ");
			val15.AppendFormatted<string>(ImGuiEx.EnumToSelectedCountString(trigger.Instigator.Race, "Any", "Any"));
			if (ImGui.BeginCombo(val14, val15, (ImGuiComboFlags)0))
			{
				foreach (RaceCondition value4 in Enum.GetValues(typeof(RaceCondition)))
				{
					if (value4 == RaceCondition.Any)
					{
						continue;
					}
					bool selected4 = trigger.Instigator.Race.HasFlag(value4);
					ImGuiEx.IconCheckbox(selected4);
					ImGui.SameLine();
					ImU8String val16 = new ImU8String(0, 1);
					val16.AppendFormatted<RaceCondition>(value4);
					if (ImGui.Selectable(val16, selected4, (ImGuiSelectableFlags)1, default(Vector2)))
					{
						if (selected4)
						{
							trigger.Instigator.Race &= ~value4;
						}
						else
						{
							trigger.Instigator.Race |= value4;
						}
						Plugin.Config.Save();
					}
				}
				ImGui.EndCombo();
			}
			ImGuiEx.SetItemTooltip("Restrict triggering of this event to players of specific race.", (ImGuiHoveredFlags)0);
		}
		if (trigger.Instigator.Type != PlayerType.None)
		{
			iO = ImGui.GetIO();
			ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
			if (ImGui.BeginCombo(new ImU8String("##instigatorStatus"), new ImU8String("Status"), (ImGuiComboFlags)0))
			{
				foreach (StatusType value5 in StatusTypes)
				{
					TriState state = TriState.Ignored;
					if (trigger.Instigator.Status.TryGetValue(value5, out var existing))
					{
						state = existing;
					}
					ImGuiEx.IconTriState(state);
					ImGui.SameLine();
					ImU8String val17 = new ImU8String(0, 1);
					val17.AppendFormatted<StatusType>(value5);
					if (ImGui.Selectable(val17, state != TriState.Ignored, (ImGuiSelectableFlags)1, default(Vector2)))
					{
						TriState next = ImGuiEx.NextTriState(state);
						if (next == TriState.Ignored)
						{
							trigger.Instigator.Status.Remove(value5);
						}
						else
						{
							trigger.Instigator.Status[value5] = next;
						}
						Plugin.Config.Save();
					}
				}
				ImGui.EndCombo();
			}
			ImGuiEx.SetItemTooltip("Restrict triggering of this event depending on instigator's status.\nCheckmark: A status they must have. (If multiple are checked, they only need 1 of them)\nCross: A status they must not have.", (ImGuiHoveredFlags)0);
			if (!trigger.Instigator.RequireNearby && trigger.Instigator.Type != PlayerType.Self && trigger.Instigator.Status.Count != 0)
			{
				ImGui.SameLine();
				ImGuiEx.IconWarningTooltip("Status condition can only be determined if the player is nearby.\nWith the above 'Nearby' option disabled, this event can still trigger without this condition being met when the player is not nearby.");
			}
		}
		if (trigger.Instigator.Type != PlayerType.Self && trigger.Instigator.Type != PlayerType.Ignore && trigger.Instigator.Type != PlayerType.None)
		{
			iO = ImGui.GetIO();
			ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
			ImU8String val18 = new ImU8String("##blacklistNames");
			ImU8String val19 = default(ImU8String);
			val19 = new ImU8String(18, 1);
			val19.AppendFormatted<int>(trigger.Instigator.BlacklistNames.Count);
			val19.AppendLiteral(" Blacklisted Names");
			if (ImGui.BeginCombo(val18, val19, (ImGuiComboFlags)0))
			{
				Action removeAction2 = null;
				ImU8String val20 = default(ImU8String);
				ImU8String val21 = default(ImU8String);
				for (int i2 = 0; i2 < trigger.Instigator.BlacklistNames.Count; i2++)
				{
					val20 = new ImU8String(8, 1);
					val20.AppendLiteral("##blItem");
					val20.AppendFormatted<int>(i2);
					ImGui.PushID(val20);
					string current2 = trigger.Instigator.BlacklistNames[i2];
					if (ImGuiEx.IconButton((FontAwesomeIcon)61944, $"##removeblName{i2}"))
					{
						removeAction2 = delegate
						{
							trigger.Instigator.BlacklistNames.Remove(current2);
							Plugin.Config.Save();
						};
					}
					ImGui.SameLine(0f, 0f);
					iO = ImGui.GetIO();
					ImGui.SetNextItemWidth(160f * iO.FontGlobalScale - ImGuiEx.GetIconButtonWidth((FontAwesomeIcon)61944));
					val21 = new ImU8String(13, 1);
					val21.AppendLiteral("##blnameInput");
					val21.AppendFormatted<int>(i2);
					if (ImGui.InputText(val21, ref current2, 40, (ImGuiInputTextFlags)0, (ImGui.ImGuiInputTextCallbackDelegate)null))
					{
						trigger.Instigator.BlacklistNames[i2] = current2;
						Plugin.Config.Save();
					}
					ImGui.PopID();
				}
				removeAction2?.Invoke();
				ImGui.Separator();
				ImGui.PushID(new ImU8String("##newBlName"));
				string newEntry2 = "";
				style = ImGui.GetStyle();
				float num2 = 160f - style.FramePadding.X * 2f;
				iO = ImGui.GetIO();
				ImGui.SetNextItemWidth(num2 * iO.FontGlobalScale);
				if (ImGui.InputTextWithHint(new ImU8String("##newBlNameItem"), new ImU8String("Player Name"), ref newEntry2, 40, (ImGuiInputTextFlags)32, (ImGui.ImGuiInputTextCallbackDelegate)null) && !string.IsNullOrWhiteSpace(newEntry2))
				{
					newEntry2 = newEntry2.Trim();
					if (!trigger.Instigator.BlacklistNames.Contains(newEntry2))
					{
						trigger.Instigator.BlacklistNames.Add(newEntry2);
						Plugin.Config.Save();
					}
				}
				ImGuiEx.SetItemTooltip("Add a new player name to blacklist.\nPress the Enter key to confirm entry.", (ImGuiHoveredFlags)0);
				ImGui.PopID();
				ImGui.EndCombo();
			}
			ImGuiEx.SetItemTooltip("A list of player names to forbid from triggering this event.\nNames are not case-sensitive and should not include @World.", (ImGuiHoveredFlags)0);
		}
		ImGui.TreePop();
	}

	private void DrawTriggerReceiver(Trigger trigger)
	{
		if (!ImGuiEx.TreeNode("Event Receiver##eventReceiver", null, default(Vector4), (ImGuiTreeNodeFlags)0))
		{
			return;
		}
		ImGuiIOPtr iO;
		if (trigger.Type == TriggerType.Emote)
		{
			if (!(trigger.Receiver is EmoteTargetReceiver))
			{
				trigger.Receiver = new EmoteTargetReceiver();
				Plugin.Config.Save();
			}
			EmoteTargetReceiver receiver = (EmoteTargetReceiver)trigger.Receiver;
			iO = ImGui.GetIO();
			ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
			ImU8String val = new ImU8String("##receiverType");
			ImU8String val2 = default(ImU8String);
			val2 = new ImU8String(6, 1);
			val2.AppendLiteral("Type: ");
			val2.AppendFormatted<PlayerType>(receiver.Type);
			if (ImGui.BeginCombo(val, val2, (ImGuiComboFlags)0))
			{
				foreach (PlayerType value in Enum.GetValues(typeof(PlayerType)))
				{
					if (value != PlayerType.Targeter)
					{
						bool selected = receiver.Type == value;
						if (ImGui.Selectable(new ImU8String(value.ToString()), selected, (ImGuiSelectableFlags)0, default(Vector2)))
						{
							receiver.Type = value;
							Plugin.Config.Save();
						}
					}
				}
				ImGui.EndCombo();
			}
			ImGuiEx.SetItemTooltip("Which type of receiving player triggers this event.\n\nIgnore: No conditions for receiver of this event will be set.\nNone: Instigator must have no target.\nAll: Instigator must target any player.\nOthers: Instigator must target other players.\nSelf: Instigator must target you.\nPlayer: Instigator must target specific named player(s).\nTarget: Instigator must target your target.", (ImGuiHoveredFlags)0);
			if (receiver.Type == PlayerType.All || receiver.Type == PlayerType.Others || receiver.Type == PlayerType.Target || receiver.Type == PlayerType.Player)
			{
				if (receiver.Type == PlayerType.Player)
				{
					iO = ImGui.GetIO();
					ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
					ImU8String val3 = new ImU8String("##receiverNames");
					ImU8String val4 = default(ImU8String);
					val4 = new ImU8String(15, 1);
					val4.AppendFormatted<int>(receiver.Names.Count);
					val4.AppendLiteral(" Names to Match");
					if (ImGui.BeginCombo(val3, val4, (ImGuiComboFlags)0))
					{
						Action removeAction = null;
						ImU8String val5 = default(ImU8String);
						ImU8String val6 = default(ImU8String);
						for (int i = 0; i < receiver.Names.Count; i++)
						{
							val5 = new ImU8String(10, 1);
							val5.AppendLiteral("##nameItem");
							val5.AppendFormatted<int>(i);
							ImGui.PushID(val5);
							string current = receiver.Names[i];
							if (ImGuiEx.IconButton((FontAwesomeIcon)61944, $"##removeName{i}"))
							{
								removeAction = delegate
								{
									receiver.Names.Remove(current);
									Plugin.Config.Save();
								};
							}
							ImGui.SameLine(0f, 0f);
							iO = ImGui.GetIO();
							ImGui.SetNextItemWidth(160f * iO.FontGlobalScale - ImGuiEx.GetIconButtonWidth((FontAwesomeIcon)61944));
							val6 = new ImU8String(11, 1);
							val6.AppendLiteral("##nameInput");
							val6.AppendFormatted<int>(i);
							if (ImGui.InputText(val6, ref current, 40, (ImGuiInputTextFlags)0, (ImGui.ImGuiInputTextCallbackDelegate)null))
							{
								receiver.Names[i] = current;
								Plugin.Config.Save();
							}
							ImGui.PopID();
						}
						removeAction?.Invoke();
						ImGui.Separator();
						ImGui.PushID(new ImU8String("##newName"));
						string newEntry = "";
						ImGuiStylePtr style = ImGui.GetStyle();
						float num = 160f - style.FramePadding.X * 2f;
						iO = ImGui.GetIO();
						ImGui.SetNextItemWidth(num * iO.FontGlobalScale);
						if (ImGui.InputTextWithHint(new ImU8String("##newNameItem"), new ImU8String("Player Name"), ref newEntry, 40, (ImGuiInputTextFlags)32, (ImGui.ImGuiInputTextCallbackDelegate)null) && !string.IsNullOrWhiteSpace(newEntry))
						{
							newEntry = newEntry.Trim();
							if (!receiver.Names.Contains(newEntry))
							{
								receiver.Names.Add(newEntry);
								Plugin.Config.Save();
							}
						}
						ImGuiEx.SetItemTooltip("Add a new player name to match for.\nPress the Enter key to confirm entry.", (ImGuiHoveredFlags)0);
						ImGui.PopID();
						ImGui.EndCombo();
					}
					ImGuiEx.SetItemTooltip("One or more player names that can trigger this event (as instigator target).\nNames are not case-sensitive and should not include @World.", (ImGuiHoveredFlags)0);
				}
				iO = ImGui.GetIO();
				ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
				ImU8String val7 = new ImU8String("##receiverCondition");
				ImU8String val8 = default(ImU8String);
				val8 = new ImU8String(10, 1);
				val8.AppendLiteral("Relation: ");
				val8.AppendFormatted<string>(ImGuiEx.EnumToSelectedCountString(receiver.Condition, "None", ""));
				if (ImGui.BeginCombo(val7, val8, (ImGuiComboFlags)0))
				{
					foreach (PlayerCondition value2 in Enum.GetValues(typeof(PlayerCondition)))
					{
						if (value2 == PlayerCondition.None)
						{
							continue;
						}
						bool selected2 = receiver.Condition.HasFlag(value2);
						ImGuiEx.IconCheckbox(selected2);
						ImGui.SameLine();
						ImU8String val9 = new ImU8String(0, 1);
						val9.AppendFormatted<PlayerCondition>(value2);
						if (ImGui.Selectable(val9, selected2, (ImGuiSelectableFlags)1, default(Vector2)))
						{
							if (selected2)
							{
								receiver.Condition &= ~value2;
							}
							else
							{
								receiver.Condition |= value2;
							}
							Plugin.Config.Save();
						}
					}
					ImGui.EndCombo();
				}
				ImGuiEx.SetItemTooltip("Restrict triggering of this event to players (instigator target) known to you.\nNone: No relation required.", (ImGuiHoveredFlags)0);
				ImGui.SameLine();
				if (ImGuiEx.Checkbox("All Selected##receiverAllConditions", receiver.RequireAllConditions, delegate(bool x)
				{
					receiver.RequireAllConditions = x;
				}))
				{
					Plugin.Config.Save();
				}
				ImGuiEx.SetItemTooltip("Whether a player must have all selected relations.", (ImGuiHoveredFlags)0);
				iO = ImGui.GetIO();
				ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
				ImU8String val10 = new ImU8String("##receiverGender");
				ImU8String val11 = default(ImU8String);
				val11 = new ImU8String(8, 1);
				val11.AppendLiteral("Gender: ");
				val11.AppendFormatted<string>(ImGuiEx.EnumToSelectedCountString(receiver.Gender, "Any", "Any"));
				if (ImGui.BeginCombo(val10, val11, (ImGuiComboFlags)0))
				{
					foreach (GenderCondition value3 in Enum.GetValues(typeof(GenderCondition)))
					{
						if (value3 == GenderCondition.Any)
						{
							continue;
						}
						bool selected3 = receiver.Gender.HasFlag(value3);
						ImGuiEx.IconCheckbox(selected3);
						ImGui.SameLine();
						ImU8String val12 = new ImU8String(0, 1);
						val12.AppendFormatted<GenderCondition>(value3);
						if (ImGui.Selectable(val12, selected3, (ImGuiSelectableFlags)1, default(Vector2)))
						{
							if (selected3)
							{
								receiver.Gender &= ~value3;
							}
							else
							{
								receiver.Gender |= value3;
							}
							Plugin.Config.Save();
						}
					}
					ImGui.EndCombo();
				}
				ImGuiEx.SetItemTooltip("Restrict triggering of this event to players (instigator target) of specific gender.", (ImGuiHoveredFlags)0);
				iO = ImGui.GetIO();
				ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
				ImU8String val13 = new ImU8String("##receiverRace");
				ImU8String val14 = default(ImU8String);
				val14 = new ImU8String(6, 1);
				val14.AppendLiteral("Race: ");
				val14.AppendFormatted<string>(ImGuiEx.EnumToSelectedCountString(receiver.Race, "Any", "Any"));
				if (ImGui.BeginCombo(val13, val14, (ImGuiComboFlags)0))
				{
					foreach (RaceCondition value4 in Enum.GetValues(typeof(RaceCondition)))
					{
						if (value4 == RaceCondition.Any)
						{
							continue;
						}
						bool selected4 = receiver.Race.HasFlag(value4);
						ImGuiEx.IconCheckbox(selected4);
						ImGui.SameLine();
						ImU8String val15 = new ImU8String(0, 1);
						val15.AppendFormatted<RaceCondition>(value4);
						if (ImGui.Selectable(val15, selected4, (ImGuiSelectableFlags)1, default(Vector2)))
						{
							if (selected4)
							{
								receiver.Race &= ~value4;
							}
							else
							{
								receiver.Race |= value4;
							}
							Plugin.Config.Save();
						}
					}
					ImGui.EndCombo();
				}
				ImGuiEx.SetItemTooltip("Restrict triggering of this event to players (instigator target) of specific race.", (ImGuiHoveredFlags)0);
			}
			if (receiver.Type != PlayerType.None && receiver.Type != PlayerType.Ignore)
			{
				iO = ImGui.GetIO();
				ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
				if (ImGui.BeginCombo(new ImU8String("##receiverStatus"), new ImU8String("Status"), (ImGuiComboFlags)0))
				{
					foreach (StatusType value5 in StatusTypes)
					{
						TriState state = TriState.Ignored;
						if (receiver.Status.TryGetValue(value5, out var existing))
						{
							state = existing;
						}
						ImGuiEx.IconTriState(state);
						ImGui.SameLine();
						ImU8String val16 = new ImU8String(0, 1);
						val16.AppendFormatted<StatusType>(value5);
						if (ImGui.Selectable(val16, state != TriState.Ignored, (ImGuiSelectableFlags)1, default(Vector2)))
						{
							TriState next = ImGuiEx.NextTriState(state);
							if (next == TriState.Ignored)
							{
								receiver.Status.Remove(value5);
							}
							else
							{
								receiver.Status[value5] = next;
							}
							Plugin.Config.Save();
						}
					}
					ImGui.EndCombo();
				}
				ImGuiEx.SetItemTooltip("Restrict triggering of this event depending on receiver's status.\nCheckmark: A status they must have. (If multiple are checked, they only need 1 of them)\nCross: A status they must not have.", (ImGuiHoveredFlags)0);
			}
		}
		else if (trigger.Type == TriggerType.Text)
		{
			if (!(trigger.Receiver is ChannelTextReceiver))
			{
				trigger.Receiver = new ChannelTextReceiver();
				Plugin.Config.Save();
			}
			ChannelTextReceiver receiver2 = (ChannelTextReceiver)trigger.Receiver;
			ImGui.BeginDisabled(receiver2.MatchAny);
			iO = ImGui.GetIO();
			ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
			ImU8String val17 = new ImU8String("##textReceiverChannels");
			ImU8String val18 = default(ImU8String);
			val18 = new ImU8String(9, 1);
			val18.AppendLiteral("Channel: ");
			val18.AppendFormatted<string>(receiver2.MatchAny ? "Any" : $"{receiver2.Channel}");
			if (ImGui.BeginCombo(val17, val18, (ImGuiComboFlags)0))
			{
				foreach (ChatType value6 in Enum.GetValues(typeof(ChatType)))
				{
					if (value6 == ChatType.None || value6 == ChatType.Command || value6 == ChatType.Echo)
					{
						continue;
					}
					bool selected5 = receiver2.Channel.HasFlag(value6);
					ImGuiEx.IconCheckbox(selected5);
					ImGui.SameLine();
					ImU8String val19 = new ImU8String(0, 1);
					val19.AppendFormatted<ChatType>(value6);
					if (ImGui.Selectable(val19, selected5, (ImGuiSelectableFlags)1, default(Vector2)))
					{
						if (selected5)
						{
							receiver2.Channel &= ~value6;
						}
						else
						{
							receiver2.Channel |= value6;
						}
						Plugin.Config.Save();
					}
				}
				ImGui.EndCombo();
			}
			ImGuiEx.SetItemTooltip("Restrict triggering of this event to specific chat channels.", (ImGuiHoveredFlags)0);
			ImGui.EndDisabled();
			ImGui.SameLine();
			if (ImGuiEx.Checkbox("Any##matchAnyChannels", receiver2.MatchAny, delegate(bool x)
			{
				receiver2.MatchAny = x;
			}))
			{
				Plugin.Config.Save();
			}
			ImGuiEx.SetItemTooltip("Matched inputs received in any channel can trigger counter/reactions.", (ImGuiHoveredFlags)0);
			iO = ImGui.GetIO();
			ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
			if (ImGui.BeginCombo(new ImU8String("##receiverStatus"), new ImU8String("Status"), (ImGuiComboFlags)0))
			{
				foreach (StatusType value7 in StatusTypes)
				{
					TriState state2 = TriState.Ignored;
					if (receiver2.Status.TryGetValue(value7, out var existing2))
					{
						state2 = existing2;
					}
					ImGuiEx.IconTriState(state2);
					ImGui.SameLine();
					ImU8String val20 = new ImU8String(0, 1);
					val20.AppendFormatted<StatusType>(value7);
					if (ImGui.Selectable(val20, state2 != TriState.Ignored, (ImGuiSelectableFlags)1, default(Vector2)))
					{
						TriState next2 = ImGuiEx.NextTriState(state2);
						if (next2 == TriState.Ignored)
						{
							receiver2.Status.Remove(value7);
						}
						else
						{
							receiver2.Status[value7] = next2;
						}
						Plugin.Config.Save();
					}
				}
				ImGui.EndCombo();
			}
			ImGuiEx.SetItemTooltip("Restrict triggering of this event depending on your status.\nCheckmark: A status you must have. (If multiple are checked, you only need 1 of them)\nCross: A status you must not have.", (ImGuiHoveredFlags)0);
		}
		ImGui.TreePop();
	}

	private void DrawTriggerCounter(Trigger trigger)
	{
		if (!ImGuiEx.TreeNode("Counter Reaction##counterReaction", null, default(Vector4), (ImGuiTreeNodeFlags)0))
		{
			return;
		}
		if (ImGuiEx.Checkbox("Use Shared Counter##useSharedCounter", trigger.UseSharedCounter, delegate(bool x)
		{
			trigger.UseSharedCounter = x;
		}))
		{
			Plugin.Config.Save();
		}
		ImGuiEx.SetItemTooltip("Whether this trigger should share a counter owned by another trigger.", (ImGuiHoveredFlags)0);
		if (!trigger.UseSharedCounter && !(trigger.Counter is Counter))
		{
			trigger.Counter = new Counter();
			Plugin.Config.Save();
		}
		else if (trigger.UseSharedCounter && !(trigger.Counter is SharedCounter))
		{
			trigger.Counter = new SharedCounter();
			Plugin.Config.Save();
		}
		Counter resolvedCounter = null;
		ImGuiIOPtr iO;
		if (trigger.UseSharedCounter)
		{
			SharedCounter shared = (trigger.Counter as SharedCounter) ?? new SharedCounter();
			List<Trigger> availableTriggers = Plugin.Config.Triggers.Where((Trigger t) => t.Guid != trigger.Guid && t.Counter is Counter).ToList();
			if (availableTriggers != null && availableTriggers.Count > 0)
			{
				int currentIndex = availableTriggers.FindIndex(delegate(Trigger t)
				{
					Guid guid = t.Guid;
					Guid? obj = shared?.TriggerGuid;
					return guid == obj;
				});
				ImGui.SameLine();
				iO = ImGui.GetIO();
				ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
				if (ImGui.BeginCombo(new ImU8String("##sharedCounterTriggers"), new ImU8String((currentIndex >= 0) ? availableTriggers[currentIndex].Name : "None"), (ImGuiComboFlags)0))
				{
					for (int i = 0; i < availableTriggers.Count; i++)
					{
						if (ImGui.Selectable(new ImU8String(availableTriggers[i].Name), i == currentIndex, (ImGuiSelectableFlags)0, default(Vector2)))
						{
							shared.TriggerGuid = availableTriggers[i].Guid;
							Plugin.Config.Save();
						}
					}
					ImGui.EndCombo();
				}
				ImGuiEx.SetItemTooltip("Select the trigger whose counter should be shared by this one.", (ImGuiHoveredFlags)0);
				resolvedCounter = Plugin.Config.Triggers.FirstOrDefault(delegate(Trigger t)
				{
					Guid guid = t.Guid;
					Guid? triggerGuid = shared.TriggerGuid;
					return guid == triggerGuid;
				})?.Counter as Counter;
			}
			else
			{
				Vector4 dalamudRed = ImGuiColors.DalamudRed;
			ImGui.TextColored(in dalamudRed, new ImU8String("No available triggers with counters to share from."));
			}
		}
		else
		{
			resolvedCounter = trigger.Counter as Counter;
		}
		if (resolvedCounter != null)
		{
			iO = ImGui.GetIO();
			ImGui.SetNextItemWidth(60f * iO.FontGlobalScale);
			if (ImGuiEx.DragInt("Current Count##counterAmount", resolvedCounter.Amount, delegate(int x)
			{
				resolvedCounter.Amount = x;
			}))
			{
				Plugin.Config.Save();
			}
			ImGuiEx.SetItemTooltip("Number of times this counter has been triggered.", (ImGuiHoveredFlags)0);
			if (ImGuiEx.TreeNode("Honorific Title##counterTitle", null, default(Vector4), (ImGuiTreeNodeFlags)0))
			{
				if (ImGuiEx.Checkbox("Display Honorific Title##counterDisplayTitle", resolvedCounter.DisplayTitle, delegate(bool x)
				{
					resolvedCounter.DisplayTitle = x;
				}))
				{
					Plugin.Config.Save();
				}
				ImGuiEx.SetItemTooltip("Whether to display a title using Honorific plugin.", (ImGuiHoveredFlags)0);
				if (resolvedCounter.DisplayTitle)
				{
					ImGui.SameLine();
					if (ImGui.Button(new ImU8String("Preview##previewTitle"), default(Vector2)))
					{
						plugin.TriggerManager.PreviewTitle(trigger, resolvedCounter);
					}
					ImGuiEx.SetItemTooltip("Show a preview of the title.", (ImGuiHoveredFlags)0);
					iO = ImGui.GetIO();
					ImGui.SetNextItemWidth(40f * iO.FontGlobalScale);
					if (ImGuiEx.DragInt("##counterTitleMinFreq", resolvedCounter.TitleMinFreq, delegate(int x)
					{
						resolvedCounter.TitleMinFreq = x;
					}, 1f, 1))
					{
						Plugin.Config.Save();
					}
					ImGuiEx.SetItemTooltip("The minimum amount of times the event is triggered before displaying title.", (ImGuiHoveredFlags)0);
					ImGui.SameLine();
					iO = ImGui.GetIO();
					ImGui.SetNextItemWidth(40f * iO.FontGlobalScale);
					if (ImGuiEx.DragInt("##counterTitleMaxFreq", resolvedCounter.TitleMaxFreq, delegate(int x)
					{
						resolvedCounter.TitleMaxFreq = x;
					}, 1f, 1))
					{
						Plugin.Config.Save();
					}
					ImGuiEx.SetItemTooltip("The maximum amount of times the event is triggered before displaying title.", (ImGuiHoveredFlags)0);
					ImGui.SameLine();
					iO = ImGui.GetIO();
					ImGui.SetNextItemWidth(60f * iO.FontGlobalScale);
					if (ImGuiEx.DragInt("##counterTitleFreqThreshold", resolvedCounter.TitleFreqThreshold, delegate(int x)
					{
						resolvedCounter.TitleFreqThreshold = x;
					}, 1f, 1))
					{
						Plugin.Config.Save();
					}
					ImGuiEx.SetItemTooltip("The count threshold to be reached before the maximum is absolute.\nWhile the count is under this threshold, the title will display with a relative frequency between min/max rounded to nearest 5.", (ImGuiHoveredFlags)0);
					ImGui.SameLine();
					ImGuiEx.IconTextUnformatted((FontAwesomeIcon)61529);
					ImGuiEx.SetItemTooltip(resolvedCounter.GetTitleFreqText(), (ImGuiHoveredFlags)0);
					ImGui.Spacing();
					iO = ImGui.GetIO();
					ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
					if (ImGuiEx.DrawHonorificTitle(resolvedCounter))
					{
						Plugin.Config.Save();
					}
					ImGuiEx.SetItemTooltip("Honorific Title template with the below formatting:\n%n% - Counter\n%ifn%/%isn% - Instigator Forename/Surname\n%rfn%/%rsn% - Receiver Forename/Surname", (ImGuiHoveredFlags)0);
					if (resolvedCounter.TitleTemplate.Length > 24)
					{
						ImGui.SameLine();
						ImGuiEx.IconWarningTooltip($"Current raw title length is {resolvedCounter.TitleTemplate.Length} characters (before template replacements).\nHonorific will not display title if it's over 32 characters in length.");
					}
					ImGui.SameLine();
					if (ImGuiEx.Checkbox("##counterPrefix", resolvedCounter.TitlePrefix, delegate(bool x)
					{
						resolvedCounter.TitlePrefix = x;
					}))
					{
						Plugin.Config.Save();
					}
					ImGuiEx.SetItemTooltip("Prefix this title above your player name.", (ImGuiHoveredFlags)0);
					ImGui.SameLine();
					if (ImGuiEx.ColorPicker3("", "counterColour", resolvedCounter.TitleColour, delegate(Vector3 x)
					{
						resolvedCounter.TitleColour = x;
					}))
					{
						Plugin.Config.Save();
					}
					ImGuiEx.SetItemTooltip("Title text colour.", (ImGuiHoveredFlags)0);
					ImGui.SameLine();
					if (ImGuiEx.HonorificGlowPicker("", "counterGlow", resolvedCounter.TitleGlow, resolvedCounter.TitleGradientColorSet, resolvedCounter.TitleGradientAnimationStyle, delegate(Vector3 glow, int? set, GradientAnimationStyle? style)
					{
						resolvedCounter.TitleGlow = glow;
						resolvedCounter.TitleGradientColorSet = set;
						resolvedCounter.TitleGradientAnimationStyle = style;
					}))
					{
						Plugin.Config.Save();
					}
					ImGuiEx.SetItemTooltip("Title text glow.", (ImGuiHoveredFlags)0);
					ImGui.SameLine();
					iO = ImGui.GetIO();
					ImGui.SetNextItemWidth(60f * iO.FontGlobalScale);
					if (ImGuiEx.DragInt("##counterDuration", resolvedCounter.TitleDuration, delegate(int x)
					{
						resolvedCounter.TitleDuration = x;
					}, 100f))
					{
						Plugin.Config.Save();
					}
					ImGuiEx.SetItemTooltip("Duration in milliseconds that the title will be displayed for.\n" + $"A value of '0' will use the global counter duration of {Plugin.Config.CounterDuration}ms.", (ImGuiHoveredFlags)0);
				}
				ImGui.TreePop();
			}
			if (ImGuiEx.TreeNode("Toast Message##counterToast", null, default(Vector4), (ImGuiTreeNodeFlags)0))
			{
				if (ImGuiEx.Checkbox("Display Toast##counterDisplayToast", resolvedCounter.DisplayToast, delegate(bool x)
				{
					resolvedCounter.DisplayToast = x;
				}))
				{
					Plugin.Config.Save();
				}
				ImGuiEx.SetItemTooltip("Whether to display a toast message.", (ImGuiHoveredFlags)0);
				if (resolvedCounter.DisplayToast)
				{
					ImGui.SameLine();
					if (ImGui.Button(new ImU8String("Preview##previewToast"), default(Vector2)))
					{
						plugin.TriggerManager.PreviewToast(resolvedCounter);
					}
					ImGuiEx.SetItemTooltip("Show a preview of the toast message.", (ImGuiHoveredFlags)0);
					iO = ImGui.GetIO();
					ImGui.SetNextItemWidth(40f * iO.FontGlobalScale);
					if (ImGuiEx.DragInt("##counterToastMinFreq", resolvedCounter.ToastMinFreq, delegate(int x)
					{
						resolvedCounter.ToastMinFreq = x;
					}, 1f, 1))
					{
						Plugin.Config.Save();
					}
					ImGuiEx.SetItemTooltip("The minimum amount of times the event is triggered before displaying toast.", (ImGuiHoveredFlags)0);
					ImGui.SameLine();
					iO = ImGui.GetIO();
					ImGui.SetNextItemWidth(40f * iO.FontGlobalScale);
					if (ImGuiEx.DragInt("##counterToastMaxFreq", resolvedCounter.ToastMaxFreq, delegate(int x)
					{
						resolvedCounter.ToastMaxFreq = x;
					}, 1f, 1))
					{
						Plugin.Config.Save();
					}
					ImGuiEx.SetItemTooltip("The maximum amount of times the event is triggered before displaying toast.", (ImGuiHoveredFlags)0);
					ImGui.SameLine();
					iO = ImGui.GetIO();
					ImGui.SetNextItemWidth(60f * iO.FontGlobalScale);
					if (ImGuiEx.DragInt("##counterToastFreqThreshold", resolvedCounter.ToastFreqThreshold, delegate(int x)
					{
						resolvedCounter.ToastFreqThreshold = x;
					}, 1f, 1))
					{
						Plugin.Config.Save();
					}
					ImGuiEx.SetItemTooltip("The count threshold to be reached before the maximum is absolute.\nWhile the count is under this threshold, the toast will display with a relative frequency between min/max rounded to nearest 5.", (ImGuiHoveredFlags)0);
					ImGui.SameLine();
					ImGuiEx.IconTextUnformatted((FontAwesomeIcon)61529);
					ImGuiEx.SetItemTooltip(resolvedCounter.GetToastFreqText(), (ImGuiHoveredFlags)0);
					ImGui.Spacing();
					iO = ImGui.GetIO();
					ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
					if (ImGuiEx.InputText("##counterToastTemplate", resolvedCounter.ToastTemplate, delegate(string x)
					{
						resolvedCounter.ToastTemplate = x;
					}))
					{
						Plugin.Config.Save();
					}
					ImGuiEx.SetItemTooltip("Toast message template with the below formatting:\n%n% - Counter\n%ifn%/%isn% - Instigator Forename/Surname\n%rfn%/%rsn% - Receiver Forename/Surname", (ImGuiHoveredFlags)0);
					iO = ImGui.GetIO();
					ImGui.SetNextItemWidth(80f * iO.FontGlobalScale);
					ImU8String val = new ImU8String("##toastType");
					ImU8String val2 = default(ImU8String);
					val2 = new ImU8String(0, 1);
					val2.AppendFormatted<ToastDisplayType>(resolvedCounter.ToastDisplayType);
					if (ImGui.BeginCombo(val, val2, (ImGuiComboFlags)0))
					{
						foreach (ToastDisplayType value in Enum.GetValues(typeof(ToastDisplayType)))
						{
							bool selected = resolvedCounter.ToastDisplayType == value;
							if (ImGui.Selectable(new ImU8String(value.ToString()), selected, (ImGuiSelectableFlags)0, default(Vector2)))
							{
								resolvedCounter.ToastDisplayType = value;
								Plugin.Config.Save();
							}
						}
						ImGui.EndCombo();
					}
					ImGuiEx.SetItemTooltip("Toast Display Type", (ImGuiHoveredFlags)0);
					if (resolvedCounter.ToastDisplayType == ToastDisplayType.Normal)
					{
						ImGui.SameLine();
						iO = ImGui.GetIO();
						ImGui.SetNextItemWidth(80f * iO.FontGlobalScale);
						ImU8String val3 = new ImU8String("##toastSpeed");
						ImU8String val4 = default(ImU8String);
						val4 = new ImU8String(0, 1);
						val4.AppendFormatted<ToastDisplaySpeed>(resolvedCounter.ToastDisplaySpeed);
						if (ImGui.BeginCombo(val3, val4, (ImGuiComboFlags)0))
						{
							foreach (ToastDisplaySpeed value2 in Enum.GetValues(typeof(ToastDisplaySpeed)))
							{
								bool selected2 = resolvedCounter.ToastDisplaySpeed == value2;
								if (ImGui.Selectable(new ImU8String(value2.ToString()), selected2, (ImGuiSelectableFlags)0, default(Vector2)))
								{
									resolvedCounter.ToastDisplaySpeed = value2;
									Plugin.Config.Save();
								}
							}
							ImGui.EndCombo();
						}
						ImGuiEx.SetItemTooltip("Toast Display Speed\n(Only available for Normal toasts)", (ImGuiHoveredFlags)0);
						ImGui.SameLine();
						iO = ImGui.GetIO();
						ImGui.SetNextItemWidth(80f * iO.FontGlobalScale);
						ImU8String val5 = new ImU8String("##toastPosition");
						ImU8String val6 = default(ImU8String);
						val6 = new ImU8String(0, 1);
						val6.AppendFormatted<ToastDisplayPosition>(resolvedCounter.ToastDisplayPosition);
						if (ImGui.BeginCombo(val5, val6, (ImGuiComboFlags)0))
						{
							foreach (ToastDisplayPosition value3 in Enum.GetValues(typeof(ToastDisplayPosition)))
							{
								bool selected3 = resolvedCounter.ToastDisplayPosition == value3;
								if (ImGui.Selectable(new ImU8String(value3.ToString()), selected3, (ImGuiSelectableFlags)0, default(Vector2)))
								{
									resolvedCounter.ToastDisplayPosition = value3;
									Plugin.Config.Save();
								}
							}
							ImGui.EndCombo();
						}
						ImGuiEx.SetItemTooltip("Toast Display Position\n(Only available for Normal toasts)", (ImGuiHoveredFlags)0);
					}
				}
				ImGui.TreePop();
			}
			if (ImGuiEx.TreeNode("Echo Chat##counterEcho", null, default(Vector4), (ImGuiTreeNodeFlags)0))
			{
				if (ImGuiEx.Checkbox("Output Echo##counterDisplayEcho", resolvedCounter.DisplayEcho, delegate(bool x)
				{
					resolvedCounter.DisplayEcho = x;
				}))
				{
					Plugin.Config.Save();
				}
				ImGuiEx.SetItemTooltip("Whether to output an echo message.", (ImGuiHoveredFlags)0);
				if (resolvedCounter.DisplayEcho)
				{
					ImGui.SameLine();
					if (ImGui.Button(new ImU8String("Preview##previewEcho"), default(Vector2)))
					{
						plugin.TriggerManager.PreviewEcho(resolvedCounter);
					}
					ImGuiEx.SetItemTooltip("Show a preview of the echo message.", (ImGuiHoveredFlags)0);
					iO = ImGui.GetIO();
					ImGui.SetNextItemWidth(40f * iO.FontGlobalScale);
					if (ImGuiEx.DragInt("##counterEchoMinFreq", resolvedCounter.EchoMinFreq, delegate(int x)
					{
						resolvedCounter.EchoMinFreq = x;
					}, 1f, 1))
					{
						Plugin.Config.Save();
					}
					ImGuiEx.SetItemTooltip("The minimum amount of times the event is triggered before outputting echo message.", (ImGuiHoveredFlags)0);
					ImGui.SameLine();
					iO = ImGui.GetIO();
					ImGui.SetNextItemWidth(40f * iO.FontGlobalScale);
					if (ImGuiEx.DragInt("##counterEchoMaxFreq", resolvedCounter.EchoMaxFreq, delegate(int x)
					{
						resolvedCounter.EchoMaxFreq = x;
					}, 1f, 1))
					{
						Plugin.Config.Save();
					}
					ImGuiEx.SetItemTooltip("The maximum amount of times the event is triggered before outputting echo message.", (ImGuiHoveredFlags)0);
					ImGui.SameLine();
					iO = ImGui.GetIO();
					ImGui.SetNextItemWidth(60f * iO.FontGlobalScale);
					if (ImGuiEx.DragInt("##counterEchoFreqThreshold", resolvedCounter.EchoFreqThreshold, delegate(int x)
					{
						resolvedCounter.EchoFreqThreshold = x;
					}, 1f, 1))
					{
						Plugin.Config.Save();
					}
					ImGuiEx.SetItemTooltip("The count threshold to be reached before the maximum is absolute.\nWhile the count is under this threshold, the echo message will output with a relative frequency between min/max rounded to nearest 5.", (ImGuiHoveredFlags)0);
					ImGui.SameLine();
					ImGuiEx.IconTextUnformatted((FontAwesomeIcon)61529);
					ImGuiEx.SetItemTooltip(resolvedCounter.GetEchoFreqText(), (ImGuiHoveredFlags)0);
					ImGui.Spacing();
					iO = ImGui.GetIO();
					ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
					if (ImGuiEx.InputText("##counterEchoTemplate", resolvedCounter.EchoTemplate, delegate(string x)
					{
						resolvedCounter.EchoTemplate = x;
					}))
					{
						Plugin.Config.Save();
					}
					ImGuiEx.SetItemTooltip("Echo message template with the below formatting:\n%n% - Counter\n%ifn%/%isn% - Instigator Forename/Surname\n%rfn%/%rsn% - Receiver Forename/Surname", (ImGuiHoveredFlags)0);
				}
				ImGui.TreePop();
			}
		}
		ImGui.TreePop();
	}

	private void DrawReactionOptions(Trigger trigger)
	{
		if (!ImGuiEx.TreeNode("Reaction Options##reactionOptions", null, default(Vector4), (ImGuiTreeNodeFlags)0))
		{
			return;
		}
		if (trigger.ReactionOptions == null)
		{
			trigger.ReactionOptions = new ReactionOptions();
			Plugin.Config.Save();
		}
		else
		{
			if (ImGuiEx.Checkbox("Passthrough Restrictions##passthroughRestrictions", trigger.ReactionOptions.PassthroughRestrictions, delegate(bool x)
			{
				trigger.ReactionOptions.PassthroughRestrictions = x;
			}))
			{
				Plugin.Config.Save();
			}
			ImGuiEx.SetItemTooltip("If the below state/range restrictions prevent the reaction queue from performing,\n this option will abort triggering of this event, allowing any similar lower priority event to trigger instead.", (ImGuiHoveredFlags)0);
			ImGui.SameLine();
			if (ImGuiEx.Checkbox("Count Failed Conditions##countFailedConditions", trigger.ReactionOptions.CountFailedConditions, delegate(bool x)
			{
				trigger.ReactionOptions.CountFailedConditions = x;
			}))
			{
				Plugin.Config.Save();
			}
			ImGuiEx.SetItemTooltip("If any of the below conditions prevent the reaction queue from performing,\n this option will allow the counter to increment & display title (if any) regardless.", (ImGuiHoveredFlags)0);
			ImGui.Spacing();
			ImGui.Separator();
			ImGui.Spacing();
			ImGuiIOPtr iO = ImGui.GetIO();
			ImGui.SetNextItemWidth(60f * iO.FontGlobalScale);
			if (ImGuiEx.DragInt("Reaction Cooldown##reactionCooldown", trigger.ReactionOptions.ReactionCooldown, delegate(int x)
			{
				trigger.ReactionOptions.ReactionCooldown = x;
			}))
			{
				Plugin.Config.Save();
			}
			ImGuiEx.SetItemTooltip("Cooldown in milliseconds for how frequent the below reactions can be triggered by this event.\nIf the event is triggered while on cooldown, reactions will be skipped but any counter attached to this event will still increment.", (ImGuiHoveredFlags)0);
			iO = ImGui.GetIO();
			ImGui.SetNextItemWidth(60f * iO.FontGlobalScale);
			ImU8String val = new ImU8String("Interrupt Behaviour##interruptType");
			ImU8String val2 = default(ImU8String);
			val2 = new ImU8String(0, 1);
			val2.AppendFormatted<ReactionInterruptType>(trigger.ReactionOptions.InterruptType);
			if (ImGui.BeginCombo(val, val2, (ImGuiComboFlags)0))
			{
				foreach (ReactionInterruptType value in Enum.GetValues(typeof(ReactionInterruptType)))
				{
					bool selected = trigger.ReactionOptions.InterruptType == value;
					if (ImGui.Selectable(new ImU8String(value.ToString()), selected, (ImGuiSelectableFlags)0, default(Vector2)))
					{
						trigger.ReactionOptions.InterruptType = value;
						Plugin.Config.Save();
					}
				}
				ImGui.EndCombo();
			}
			ImGuiEx.SetItemTooltip("Determines the behaviour for interrupting reaction queue when another event is triggered.\n\nNone: No triggers can interrupt this reaction queue.\nAny: Any triggers can interrupt this reaction queue.\nSame: Only same trigger can interrupt this reaction queue.\nOther: Only other triggers can interrupt this reaction queue.", (ImGuiHoveredFlags)0);
			iO = ImGui.GetIO();
			ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
			ImU8String val3 = new ImU8String("Restricted States##stateConditions");
			ImU8String val4 = default(ImU8String);
			val4 = new ImU8String(7, 1);
			val4.AppendLiteral("State: ");
			val4.AppendFormatted<string>(ImGuiEx.EnumToSelectedCountString(trigger.ReactionOptions.StateConditions, "None", ""));
			if (ImGui.BeginCombo(val3, val4, (ImGuiComboFlags)0))
			{
				foreach (StateConditionType value2 in Enum.GetValues(typeof(StateConditionType)))
				{
					if (value2 == StateConditionType.None)
					{
						continue;
					}
					bool selected2 = trigger.ReactionOptions.StateConditions.HasFlag(value2);
					ImGuiEx.IconCheckbox(selected2);
					ImGui.SameLine();
					ImU8String val5 = new ImU8String(0, 1);
					val5.AppendFormatted<StateConditionType>(value2);
					if (ImGui.Selectable(val5, selected2, (ImGuiSelectableFlags)1, default(Vector2)))
					{
						if (selected2)
						{
							trigger.ReactionOptions.StateConditions &= ~value2;
						}
						else
						{
							trigger.ReactionOptions.StateConditions |= value2;
						}
						Plugin.Config.Save();
					}
				}
				ImGui.EndCombo();
			}
			ImGuiEx.SetItemTooltip("Prevent performing this reaction queue under specific player states:\n\nMoving: Prevent when you're moving.\nStanding: Prevent when you're standing idle.\nGroundSit: Prevent when you're sitting on ground.\nChairSit: Prevent when you're sitting on chair.\nSleeping: Prevent when you're sleeping.\nEmote: Prevent when you're performing a standard emote.\nLoopingEmote: Prevent when you're performing a looping emote (eg. dancing).", (ImGuiHoveredFlags)0);
			iO = ImGui.GetIO();
			ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
			ImU8String val6 = new ImU8String("State Restoration##restoreType");
			ImU8String val7 = default(ImU8String);
			val7 = new ImU8String(9, 1);
			val7.AppendLiteral("Restore: ");
			val7.AppendFormatted<string>(ImGuiEx.EnumToSelectedCountString(trigger.ReactionOptions.RestoreType, "None", ""));
			if (ImGui.BeginCombo(val6, val7, (ImGuiComboFlags)0))
			{
				foreach (RestoreType value3 in Enum.GetValues(typeof(RestoreType)))
				{
					if (value3 == RestoreType.None)
					{
						continue;
					}
					bool selected3 = trigger.ReactionOptions.RestoreType.HasFlag(value3);
					ImGuiEx.IconCheckbox(selected3);
					ImGui.SameLine();
					ImU8String val8 = new ImU8String(0, 1);
					val8.AppendFormatted<RestoreType>(value3);
					if (ImGui.Selectable(val8, selected3, (ImGuiSelectableFlags)1, default(Vector2)))
					{
						if (selected3)
						{
							trigger.ReactionOptions.RestoreType &= ~value3;
						}
						else
						{
							trigger.ReactionOptions.RestoreType |= value3;
						}
						Plugin.Config.Save();
					}
				}
				ImGui.EndCombo();
			}
			ImGuiEx.SetItemTooltip("Determines which properties to restore when reaction queue ends.\n\nEmote: Restore looping emote (like dances/sit/sleep) if you were performing any prior to this event.\nTarget: Restore target if any reactions caused changes to it.\nRotation/Position: Restore character rotation/position if any reactions caused changes to them.", (ImGuiHoveredFlags)0);
			ImGui.Spacing();
			ImGui.Separator();
			ImGui.Spacing();
			if (ImGuiEx.Checkbox("Restrict Range##restrictRange", trigger.ReactionOptions.RestrictRange, delegate(bool x)
			{
				trigger.ReactionOptions.RestrictRange = x;
			}))
			{
				Plugin.Config.Save();
				if (!trigger.ReactionOptions.RestrictRange)
				{
					DrawRangePreview = false;
				}
			}
			ImGuiEx.SetItemTooltip("Whether reactions will only be performed if the instigator is within a specified range relative to you.\nIf you are the instigator, this can be the receiver's position relative to you instead.\n\nIf the reaction queue is empty, this condition can still determine whether to trigger the counter if 'Count Failed Conditions' is disabled.", (ImGuiHoveredFlags)0);
			if (trigger.ReactionOptions.RestrictRange)
			{
				ImGui.SameLine();
				if (ImGui.Button(new ImU8String("Preview##previewRange"), default(Vector2)))
				{
					DrawRangePreview = !DrawRangePreview;
				}
				ImGuiEx.SetItemTooltip("Toggle previewing the reaction range around your character.", (ImGuiHoveredFlags)0);
				if (DrawRangePreview)
				{
					ImGui.SameLine();
					iO = ImGui.GetIO();
					ImGui.SetNextItemWidth(80f * iO.FontGlobalScale);
					ImGuiEx.DragFloat("Opacity##opacity", RangePreviewOpacity, delegate(float x)
					{
						RangePreviewOpacity = x;
					}, 0.01f, 0.05f, 1f);
					ImGuiEx.SetItemTooltip("Opacity of the drawn preview region.", (ImGuiHoveredFlags)0);
				}
				iO = ImGui.GetIO();
				ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
				if (ImGuiEx.DragFloat("Min Distance##minDistance", trigger.ReactionOptions.RestrictedDistanceMin, delegate(float x)
				{
					trigger.ReactionOptions.RestrictedDistanceMin = x;
				}, 0.01f, 0f, 99.99f))
				{
					Plugin.Config.Save();
				}
				iO = ImGui.GetIO();
				ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
				if (ImGuiEx.DragFloat("Max Distance##maxDistance", trigger.ReactionOptions.RestrictedDistanceMax, delegate(float x)
				{
					trigger.ReactionOptions.RestrictedDistanceMax = x;
				}, 0.01f, 0.01f, 100f))
				{
					Plugin.Config.Save();
				}
				if (trigger.ReactionOptions.RestrictedDistanceMin > trigger.ReactionOptions.RestrictedDistanceMax)
				{
					trigger.ReactionOptions.RestrictedDistanceMin = trigger.ReactionOptions.RestrictedDistanceMax - 0.01f;
					Plugin.Config.Save();
				}
				if (trigger.ReactionOptions.RestrictedDistanceMax < trigger.ReactionOptions.RestrictedDistanceMin)
				{
					trigger.ReactionOptions.RestrictedDistanceMax = trigger.ReactionOptions.RestrictedDistanceMin + 0.01f;
					Plugin.Config.Save();
				}
				if (DrawRangePreview && trigger.ReactionOptions.RestrictedDistanceMax > 4f)
				{
					ImGui.SameLine();
					ImGuiEx.IconWarningTooltip("Preview may not display correctly if max distance exceeds camera region.");
				}
				iO = ImGui.GetIO();
				ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
				if (ImGuiEx.DragInt("Angle Direction##angleDirection", trigger.ReactionOptions.RestrictedAngleDirection, delegate(int x)
				{
					trigger.ReactionOptions.RestrictedAngleDirection = x;
				}, 1f, 0, 360))
				{
					Plugin.Config.Save();
				}
				iO = ImGui.GetIO();
				ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
				if (ImGuiEx.DragFloat("Angle Area##angleArea", trigger.ReactionOptions.RestrictedAngleArea, delegate(float x)
				{
					trigger.ReactionOptions.RestrictedAngleArea = x;
				}, 0.01f, 0f, 1f))
				{
					Plugin.Config.Save();
				}
				if (DrawRangePreview)
				{
					DrawReactionRangePreview(trigger.ReactionOptions);
				}
			}
			ImGui.Spacing();
			ImGui.Separator();
			ImGui.Spacing();
			if (ImGuiEx.Checkbox("Restrict Territory##restrictTerritory", trigger.ReactionOptions.RestrictTerritory, delegate(bool x)
			{
				trigger.ReactionOptions.RestrictTerritory = x;
			}))
			{
				Plugin.Config.Save();
			}
			ImGuiEx.SetItemTooltip("Whether reactions will only be performed if you are in a specific territory.", (ImGuiHoveredFlags)0);
			if (trigger.ReactionOptions.RestrictTerritory)
			{
				List<Territory> allowedTerritories = trigger.ReactionOptions.AllowedTerritories ??= new List<Territory>();
				if (TerritoryUiList.Count == 0)
				{
					BuildTerritoryUiList();
				}
				ImGui.SameLine();
				if (ImGuiEx.IconButton((FontAwesomeIcon)61525, "newTerritory"))
				{
					allowedTerritories.Add(new Territory());
					Plugin.Config.Save();
				}
				ImGuiEx.SetItemTooltip("Add new territory.", (ImGuiHoveredFlags)0);
				if (plugin.TryGetCurrentTerritory(out var t))
				{
					ImGui.SameLine();
					ImGuiEx.IconTextUnformatted((FontAwesomeIcon)61530);
					DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(33, 5);
					defaultInterpolatedStringHandler.AppendLiteral("Current Territory\n");
					defaultInterpolatedStringHandler.AppendLiteral("Name (Id): ");
					PlaceName? valueNullable = t.PlaceName.ValueNullable;
					object value4;
					if (!valueNullable.HasValue)
					{
						value4 = null;
					}
					else
					{
						PlaceName valueOrDefault = valueNullable.GetValueOrDefault();
						ReadOnlySeString name = valueOrDefault.Name;
						value4 = name.ExtractText();
					}
					defaultInterpolatedStringHandler.AppendFormatted((string?)value4);
					defaultInterpolatedStringHandler.AppendLiteral(" (");
					defaultInterpolatedStringHandler.AppendFormatted(t.RowId);
					defaultInterpolatedStringHandler.AppendLiteral(")\n");
					defaultInterpolatedStringHandler.AppendFormatted(PlayerManager.IsInWard ? $"Ward: {PlayerManager.CurrentWard}\n" : "");
					defaultInterpolatedStringHandler.AppendFormatted(PlayerManager.IsInPlot ? $"Plot: {PlayerManager.CurrentPlot}\n" : "");
					defaultInterpolatedStringHandler.AppendFormatted(PlayerManager.IsInRoom ? $"Room: {PlayerManager.CurrentRoom}\n" : "");
					ImGuiEx.SetItemTooltip(defaultInterpolatedStringHandler.ToStringAndClear(), (ImGuiHoveredFlags)0);
				}
				for (int i = 0; i < allowedTerritories.Count; i++)
				{
					Territory entry = allowedTerritories[i];
					ImGui.PushID((IntPtr)i);
					bool isRemoved = false;
					ImGuiEx.IconButton((FontAwesomeIcon)62189, "removeTerritory");
					ImGuiEx.SetItemTooltip("Remove this territory.", (ImGuiHoveredFlags)0);
					if (ImGui.BeginPopupContextItem(new ImU8String("##removeTerritoryContext"), (ImGuiPopupFlags)0))
					{
						if (ImGuiEx.IconSelectable((FontAwesomeIcon)62189))
						{
							allowedTerritories.RemoveAt(i);
							Plugin.Config.Save();
							isRemoved = true;
						}
						ImGui.EndPopup();
					}
					if (isRemoved)
					{
						ImGui.PopID();
						break;
					}
					ImGui.SameLine();
					iO = ImGui.GetIO();
					ImGui.SetNextItemWidth(250f * iO.FontGlobalScale);
					string preview = ((entry.Id == 0) ? "Select Territory" : (TerritoryUiList.FirstOrDefault<(uint, string, bool)>(((uint Id, string Name, bool IsResidential) x) => x.Id == entry.Id).Item2 ?? "Unknown"));
					if (ImGui.BeginCombo(new ImU8String("##territory"), new ImU8String(preview), (ImGuiComboFlags)0))
					{
						if (ImGui.Checkbox(new ImU8String("Residential Only"), ref ResidentialOnly))
						{
							BuildTerritoryUiList();
						}
						ImGui.Separator();
						foreach (var territoryUi in TerritoryUiList)
						{
							uint id = territoryUi.Id;
							string name2 = territoryUi.Name;
							bool selected4 = entry.Id == id;
							ImU8String val9 = new ImU8String(3, 2);
							val9.AppendFormatted<string>(name2);
							val9.AppendLiteral(" (");
							val9.AppendFormatted<uint>(id);
							val9.AppendLiteral(")");
							if (ImGui.Selectable(val9, selected4, (ImGuiSelectableFlags)0, default(Vector2)))
							{
								entry.Id = id;
								entry.Ward = (entry.Plot = (entry.Room = 0u));
								Plugin.Config.Save();
							}
						}
						ImGui.EndCombo();
					}
					ResidentialTerritory resi = Plugin.ResidentialTerritories.FirstOrDefault((ResidentialTerritory x) => x.Id == entry.Id);
					if (resi == null || resi.ResidentialType == ResidentialType.Workshop)
					{
						ImGui.PopID();
						continue;
					}
					switch (resi.ResidentialType)
					{
					case ResidentialType.Ward:
						ImGui.SameLine();
						iO = ImGui.GetIO();
						ImGui.SetNextItemWidth(30f * iO.FontGlobalScale);
						if (ImGuiEx.DragUInt($"W##ward{i}", entry.Ward, delegate(uint x)
						{
							entry.Ward = x;
						}, 0.1f, 0u, 30u))
						{
							Plugin.Config.Save();
						}
						ImGuiEx.SetItemTooltip("The Ward to match when in " + resi.Name + "\nSet to '0' for any ward.", (ImGuiHoveredFlags)0);
						ImGui.SameLine();
						iO = ImGui.GetIO();
						ImGui.SetNextItemWidth(30f * iO.FontGlobalScale);
						if (ImGuiEx.DragUInt($"P##plot{i}", entry.Plot, delegate(uint x)
						{
							entry.Plot = x;
						}, 0.1f, 0u, 60u))
						{
							Plugin.Config.Save();
						}
						ImGuiEx.SetItemTooltip("The Plot to match when in " + resi.Name + "\nThis restricts match to being within a plot's garden area.\nSet to '0' to ignore plot.", (ImGuiHoveredFlags)0);
						break;
					case ResidentialType.House:
						ImGui.SameLine();
						iO = ImGui.GetIO();
						ImGui.SetNextItemWidth(30f * iO.FontGlobalScale);
						if (ImGuiEx.DragUInt($"W##ward{i}", entry.Ward, delegate(uint x)
						{
							entry.Ward = x;
						}, 0.1f, 0u, 30u))
						{
							Plugin.Config.Save();
						}
						ImGuiEx.SetItemTooltip("The Ward to match when in " + resi.Name + "\nSet to '0' for any ward.", (ImGuiHoveredFlags)0);
						ImGui.SameLine();
						iO = ImGui.GetIO();
						ImGui.SetNextItemWidth(30f * iO.FontGlobalScale);
						if (ImGuiEx.DragUInt($"P##plot{i}", entry.Plot, delegate(uint x)
						{
							entry.Plot = x;
						}, 0.1f, 0u, 60u))
						{
							Plugin.Config.Save();
						}
						ImGuiEx.SetItemTooltip("The Plot to match when in " + resi.Name + "\nThis restricts match to being inside a specific house.\nSet to '0' to ignore plot.", (ImGuiHoveredFlags)0);
						break;
					case ResidentialType.Chambers:
						ImGui.SameLine();
						iO = ImGui.GetIO();
						ImGui.SetNextItemWidth(30f * iO.FontGlobalScale);
						if (ImGuiEx.DragUInt($"W##ward{i}", entry.Ward, delegate(uint x)
						{
							entry.Ward = x;
						}, 0.1f, 0u, 30u))
						{
							Plugin.Config.Save();
						}
						ImGuiEx.SetItemTooltip("The Ward to match when in " + resi.Name + "\nSet to '0' for any ward.", (ImGuiHoveredFlags)0);
						ImGui.SameLine();
						iO = ImGui.GetIO();
						ImGui.SetNextItemWidth(30f * iO.FontGlobalScale);
						if (ImGuiEx.DragUInt($"P##plot{i}", entry.Plot, delegate(uint x)
						{
							entry.Plot = x;
						}, 0.1f, 0u, 60u))
						{
							Plugin.Config.Save();
						}
						ImGuiEx.SetItemTooltip("The Plot to match when in " + resi.Name + "\nThis restricts match to being inside a specific house.\nSet to '0' to ignore plot.", (ImGuiHoveredFlags)0);
						ImGui.SameLine();
						iO = ImGui.GetIO();
						ImGui.SetNextItemWidth(30f * iO.FontGlobalScale);
						if (ImGuiEx.DragUInt($"R##room{i}", entry.Room, delegate(uint x)
						{
							entry.Room = x;
						}, 0.1f, 0u, 200u))
						{
							Plugin.Config.Save();
						}
						ImGuiEx.SetItemTooltip("The FC Room to match when in " + resi.Name + "\nThis restricts match to being inside a specific FC room.\nSet to '0' to ignore room.", (ImGuiHoveredFlags)0);
						break;
					case ResidentialType.Apartment:
						ImGui.SameLine();
						iO = ImGui.GetIO();
						ImGui.SetNextItemWidth(30f * iO.FontGlobalScale);
						if (ImGuiEx.DragUInt($"W##ward{i}", entry.Ward, delegate(uint x)
						{
							entry.Ward = x;
						}, 0.1f, 0u, 30u))
						{
							Plugin.Config.Save();
						}
						ImGuiEx.SetItemTooltip("The Ward to match when in " + resi.Name + "\nSet to '0' for any ward.", (ImGuiHoveredFlags)0);
						ImGui.SameLine();
						iO = ImGui.GetIO();
						ImGui.SetNextItemWidth(30f * iO.FontGlobalScale);
						if (ImGuiEx.DragUInt($"R##room{i}", entry.Room, delegate(uint x)
						{
							entry.Room = x;
						}, 0.1f, 0u, 200u))
						{
							Plugin.Config.Save();
						}
						ImGuiEx.SetItemTooltip("The Apartment Room to match when in " + resi.Name + "\nThis restricts match to being inside a specific apartment room.\nSet to '0' to ignore room.", (ImGuiHoveredFlags)0);
						break;
					case ResidentialType.ApartmentLobby:
						ImGui.SameLine();
						iO = ImGui.GetIO();
						ImGui.SetNextItemWidth(30f * iO.FontGlobalScale);
						if (ImGuiEx.DragUInt($"W##ward{i}", entry.Ward, delegate(uint x)
						{
							entry.Ward = x;
						}, 0.1f, 0u, 30u))
						{
							Plugin.Config.Save();
						}
						ImGuiEx.SetItemTooltip("The Ward to match when in " + resi.Name + "\nSet to '0' for any ward.", (ImGuiHoveredFlags)0);
						break;
					}
					ImGui.PopID();
				}
			}
		}
		ImGui.TreePop();
	}

	private void DrawReactionRangePreview(ReactionOptions options)
	{
		if (!options.RestrictRange)
		{
			return;
		}
		EntityInfo localPlayer = PlayerManager.LocalPlayer;
		if (localPlayer == null)
		{
			return;
		}
		ImGui.Text(new ImU8String("Target Test:"));
		ImGui.SameLine();
		if (!localPlayer.IsTargetValid || localPlayer.Target == localPlayer.GameObject)
		{
			Vector4 dalamudRed = ImGuiColors.DalamudRed;
			ImGui.TextColored(in dalamudRed, new ImU8String("Target a player/npc to test with."));
		}
		else
		{
			EntityInfo targetPlayer = PlayerManager.GetTargetAsEntity();
			if (targetPlayer != null)
			{
				if (targetPlayer.IsWithinReactionAngleAndDistanceToLocalPlayer(options))
				{
					Vector4 dalamudRed = ImGuiColors.ParsedGreen;
			ImGui.TextColored(in dalamudRed, new ImU8String("In reaction area."));
				}
				else
				{
					Vector4 dalamudRed = ImGuiColors.DalamudRed;
			ImGui.TextColored(in dalamudRed, new ImU8String("Not in reaction area."));
				}
			}
			else
			{
				Vector4 dalamudRed = ImGuiColors.DalamudRed;
			ImGui.TextColored(in dalamudRed, new ImU8String("Invalid target."));
			}
		}
		Vector4 fillColor = new Vector4(0f, 1f, 0f, RangePreviewOpacity);
		int segments = 64;
		float screenMargin = 128f;
		ImDrawListPtr drawList = ImGui.GetWindowDrawList();
		ImGuiViewportPtr mainViewport = ImGuiHelpers.MainViewport;
		Vector2 pos = mainViewport.Pos;
		mainViewport = ImGuiHelpers.MainViewport;
		Vector2 vpSize = mainViewport.Size;
		Vector2 vpRectMin = pos;
		Vector2 vpRectMax = pos + vpSize;
		float minDist = Math.Max(0f, options.RestrictedDistanceMin);
		float maxDist = Math.Max(minDist + 0.001f, options.RestrictedDistanceMax);
		if (maxDist <= 0f || maxDist <= minDist)
		{
			return;
		}
		Vector3 playerPos = (Vector3)localPlayer.Position;
		float playerFacing = localPlayer.Angle;
		Vector2 forward = new Vector2(MathF.Sin(playerFacing), MathF.Cos(playerFacing));
		float dirDeg = (float)options.RestrictedAngleDirection % 360f;
		if (dirDeg < 0f)
		{
			dirDeg += 360f;
		}
		float x = (float)Math.PI * 2f * (dirDeg / 360f);
		float cosC = MathF.Cos(x);
		float sinC = MathF.Sin(x);
		Vector2 centerDir = new Vector2(forward.X * cosC - forward.Y * sinC, forward.X * sinC + forward.Y * cosC);
		float areaFrac = Math.Clamp(options.RestrictedAngleArea, 0f, 1f);
		float totalCone = (float)Math.PI * 2f * areaFrac;
		float halfCone = totalCone / 2f;
		List<(Vector2, Vector2)> pairs = new List<(Vector2, Vector2)>(segments + 1);
		Vector2 innerScreen = default(Vector2);
		Vector2 outerScreen = default(Vector2);
		for (int i = 0; i <= segments; i++)
		{
			float t = ((segments == 0) ? 0f : ((float)i / (float)segments));
			float x2 = 0f - halfCone + t * totalCone;
			float cosR = MathF.Cos(x2);
			float sinR = MathF.Sin(x2);
			Vector2 sampleDir = new Vector2(centerDir.X * cosR - centerDir.Y * sinR, centerDir.X * sinR + centerDir.Y * cosR);
			Vector3 innerWorld = playerPos + new Vector3(sampleDir.X * minDist, 0f, sampleDir.Y * minDist);
			Vector3 outerWorld = playerPos + new Vector3(sampleDir.X * maxDist, 0f, sampleDir.Y * maxDist);
			if (Plugin.GameGui.WorldToScreen(innerWorld, out innerScreen) && Plugin.GameGui.WorldToScreen(outerWorld, out outerScreen) && !(innerScreen.X < vpRectMin.X - screenMargin) && !(innerScreen.X > vpRectMax.X + screenMargin) && !(innerScreen.Y < vpRectMin.Y - screenMargin) && !(innerScreen.Y > vpRectMax.Y + screenMargin) && !(outerScreen.X < vpRectMin.X - screenMargin) && !(outerScreen.X > vpRectMax.X + screenMargin) && !(outerScreen.Y < vpRectMin.Y - screenMargin) && !(outerScreen.Y > vpRectMax.Y + screenMargin))
			{
				pairs.Add((innerScreen, outerScreen));
			}
		}
		if (pairs.Count >= 2)
		{
			uint col = ImGui.GetColorU32(fillColor);
			ImGui.PushClipRect(vpRectMin, vpRectMax, false);
			for (int j = 0; j < pairs.Count - 1; j++)
			{
				Vector2 aOuter = pairs[j].Item2;
				Vector2 bOuter = pairs[j + 1].Item2;
				Vector2 aInner = pairs[j].Item1;
				Vector2 bInner = pairs[j + 1].Item1;
				drawList.AddTriangleFilled(aOuter, bOuter, aInner, col);
				drawList.AddTriangleFilled(aInner, bOuter, bInner, col);
			}
			ImGui.PopClipRect();
		}
	}

	private static void BuildTerritoryUiList()
	{
		TerritoryUiList = new List<(uint, string, bool)>();
		foreach (ResidentialTerritory r in Plugin.ResidentialTerritories)
		{
			TerritoryUiList.Add((r.Id, r.Name, true));
		}
		if (ResidentialOnly)
		{
			return;
		}
		foreach (NonResidentialTerritory n in Plugin.NonResidentialTerritories.OrderBy<NonResidentialTerritory, string>((NonResidentialTerritory x) => x.Name, StringComparer.OrdinalIgnoreCase))
		{
			TerritoryUiList.Add((n.Id, n.Name, false));
		}
	}

	private void DrawTriggerReactionQueue(Trigger trigger)
	{
		if (ImGuiEx.TreeNode("Reaction Queues##reactionQueues", null, default(Vector4), (ImGuiTreeNodeFlags)0))
		{
			DrawTriggerEmoteReactions(trigger);
			DrawTriggerTextReactions(trigger);
			ImGui.TreePop();
		}
	}

	private void DrawTriggerEmoteReactions(Trigger trigger)
	{
		if (!ImGuiEx.TreeNode("Emote Reactions##emoteReactions", null, default(Vector4), (ImGuiTreeNodeFlags)0))
		{
			return;
		}
		if (ImGuiEx.IconButton((FontAwesomeIcon)61525, "newEmoteReaction"))
		{
			if (trigger.Reactions == null)
			{
				List<ReactionBase> list = (trigger.Reactions = new List<ReactionBase>());
			}
			trigger.Reactions.Add(new EmoteReaction());
			Plugin.Config.Save();
		}
		ImGuiEx.SetItemTooltip("Add new emote reaction.", (ImGuiHoveredFlags)0);
		List<ReactionBase> emoteReactions = ((trigger.Reactions == null) ? null : trigger.Reactions.Where((ReactionBase x) => x is EmoteReaction).ToList());
		if (trigger.Reactions == null || emoteReactions == null)
		{
			ImGui.LabelText(new ImU8String("##noEmoteReactions"), new ImU8String("No Emote Reactions Added"));
		}
		else
		{
			if (emoteReactions.Count > 0)
			{
				ImGui.SameLine();
				if (ImGui.Button(new ImU8String("Preview##previewEmotes"), default(Vector2)))
				{
					plugin.TriggerManager.PreviewQueue(trigger);
				}
				ImGuiEx.SetItemTooltip("Preview the current emote/text reactions.\n\n- Preview ignores territory, range, state, and interrupt restrictions.\n- Certain emote options are not able to be previewed such as copying instigator emote.\n- Preview of text reactions will be performed in the echo chat channel.\n- If you have a valid player target, they will be treated as the instigator/receiver depending on \n   event instigator/receiver options above.", (ImGuiHoveredFlags)0);
			}
			Action action = null;
			int i = 1;
			foreach (ReactionBase reaction in emoteReactions)
			{
				trigger.Reactions.IndexOf(reaction);
				if (ImGuiEx.TreeNode($"{i}. Emote Reaction##emoteReaction{i}", null, default(Vector4), (ImGuiTreeNodeFlags)0))
				{
					action = DrawEmoteReaction(trigger, emoteReactions, (EmoteReaction)reaction);
					ImGui.TreePop();
				}
				i++;
			}
			action?.Invoke();
		}
		ImGui.TreePop();
	}

	private Action? DrawEmoteReaction(Trigger trigger, List<ReactionBase> emoteReactions, EmoteReaction reaction)
	{
		Action action = null;
		if (trigger.Reactions != null && trigger.Reactions.Count != 0)
		{
			if (ImGuiEx.IconButton((FontAwesomeIcon)61610, "moveReactionUp"))
			{
				action = delegate
				{
					int num = trigger.Reactions.IndexOf(reaction);
					trigger.Reactions.RemoveAt(num);
					num = Math.Max(num - 1, 0);
					trigger.Reactions.Insert(num, reaction);
					Plugin.Config.Save();
				};
			}
			ImGuiEx.SetItemTooltip("Move this reaction up the chain.", (ImGuiHoveredFlags)0);
			ImGui.SameLine();
			if (ImGuiEx.IconButton((FontAwesomeIcon)61611, "moveReactionDown"))
			{
				action = delegate
				{
					int num = trigger.Reactions.IndexOf(reaction);
					trigger.Reactions.RemoveAt(num);
					num = Math.Min(num + 1, trigger.Reactions.Count);
					trigger.Reactions.Insert(num, reaction);
					Plugin.Config.Save();
				};
			}
			ImGuiEx.SetItemTooltip("Move this reaction down the chain.", (ImGuiHoveredFlags)0);
			ImGui.SameLine();
			ImGuiEx.IconButton((FontAwesomeIcon)62189, "removeReaction");
			ImGuiEx.SetItemTooltip("Remove this reaction.", (ImGuiHoveredFlags)0);
			if (ImGui.BeginPopupContextItem(new ImU8String("##removeContext"), (ImGuiPopupFlags)0))
			{
				if (ImGuiEx.IconSelectable((FontAwesomeIcon)62189))
				{
					action = delegate
					{
						int num = trigger.Reactions.IndexOf(reaction);
						trigger.Reactions.RemoveAt(num);
						num = Math.Min(num, trigger.Reactions.Count - 1);
						Plugin.Config.Save();
					};
				}
				ImGui.EndPopup();
			}
		}
		int emoteIndex = emoteReactions.IndexOf(reaction);
		if (ImGuiEx.Checkbox("Perform Emote##performEmote", reaction.PerformEmote, delegate(bool x)
		{
			reaction.PerformEmote = x;
		}))
		{
			Plugin.Config.Save();
		}
		ImGuiEx.SetItemTooltip("Whether this emote reaction should actually perform an emote.\nOtherwise this reaction will only be used as a pause duration or switching target/adjusting position.", (ImGuiHoveredFlags)0);
		ImGuiIOPtr iO;
		if (emoteIndex == 0)
		{
			iO = ImGui.GetIO();
			ImGui.SetNextItemWidth(60f * iO.FontGlobalScale);
			if (ImGuiEx.DragInt("Delay##reactionDelay", reaction.Delay, delegate(int x)
			{
				reaction.Delay = x;
			}))
			{
				Plugin.Config.Save();
			}
			ImGuiEx.SetItemTooltip("Delay in milliseconds before this reaction will be performed from when the event is triggered.", (ImGuiHoveredFlags)0);
			ImGui.SameLine();
		}
		iO = ImGui.GetIO();
		ImGui.SetNextItemWidth(60f * iO.FontGlobalScale);
		if (ImGuiEx.DragInt("Duration##reactionDuration", reaction.Duration, delegate(int x)
		{
			reaction.Duration = x;
		}))
		{
			Plugin.Config.Save();
		}
		ImGuiEx.SetItemTooltip("Duration in milliseconds for this reaction before any proceeding reaction will be performed.\nThis is also a duration for which the reaction is considered interruptable.", (ImGuiHoveredFlags)0);
		if (emoteReactions.Count > 1 && reaction.Duration < 500 && emoteIndex + 1 < emoteReactions.Count)
		{
			EmoteReaction nextReaction = (EmoteReaction)emoteReactions[emoteIndex + 1];
			if (nextReaction != null && nextReaction.PerformEmote)
			{
				ImGui.SameLine();
				ImGuiEx.IconWarningTooltip("This emote queue may not perform as expected due to the current duration being too short, the next emote may be skipped or may interrupt this emote sooner than desired.\nThe 'Preview' button can be used to test how this queue behaves so you can finetune the duration.");
			}
		}
		if (reaction.PerformEmote)
		{
			ImGui.BeginDisabled(reaction.CopyInstigator);
			iO = ImGui.GetIO();
			ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
			Emote selectedEmote = plugin.Emotes.FirstOrDefault((Emote e) => e.ID == reaction.ID && reaction.ID != ushort.MaxValue);
			if (ImGui.BeginCombo(new ImU8String("##reactionEmotes"), new ImU8String(reaction.CopyInstigator ? "Copy Instigator" : ((selectedEmote != null) ? ("Emote: " + selectedEmote.Name) : "No Emote Selected")), (ImGuiComboFlags)0))
			{
				if (!IsComboOpen_ReactionEmotes)
				{
					IsComboOpen_ReactionEmotes = true;
					plugin.Emotes = plugin.Emotes.OrderByDescending((Emote emote2) => reaction.ID == emote2.ID && emote2.ID != ushort.MaxValue).ThenBy<Emote, string>((Emote emote2) => emote2.Name, StringComparer.OrdinalIgnoreCase).ToList();
				}
				foreach (Emote emote in plugin.Emotes)
				{
					if (!string.IsNullOrWhiteSpace(emote.Command) || emote.IsPose)
					{
						bool selected = reaction.ID == emote.ID;
						ImGuiEx.IconCheckbox(selected);
						ImGui.SameLine();
						if (ImGui.Selectable(new ImU8String(emote.ToString()), selected, (ImGuiSelectableFlags)1, default(Vector2)))
						{
							reaction.ID = (selected ? ushort.MaxValue : emote.ID);
							Plugin.Config.Save();
						}
					}
				}
				ImGui.EndCombo();
			}
			else if (IsComboOpen_ReactionEmotes)
			{
				IsComboOpen_ReactionEmotes = false;
			}
			ImGui.EndDisabled();
			ImGuiEx.SetItemTooltip("Select an emote to react with when this event is triggered.", (ImGuiHoveredFlags)0);
			if (trigger.Type == TriggerType.Emote)
			{
				ImGui.SameLine();
				if (ImGuiEx.Checkbox("Copy Instigator##copyInstigator", reaction.CopyInstigator, delegate(bool x)
				{
					reaction.CopyInstigator = x;
				}))
				{
					Plugin.Config.Save();
				}
				ImGuiEx.SetItemTooltip("Copy the emote that the instigator performed.\nIf you have not unlocked the emote, this reaction will not be performed.", (ImGuiHoveredFlags)0);
			}
			else if (reaction.CopyInstigator)
			{
				reaction.CopyInstigator = false;
				Plugin.Config.Save();
			}
		}
		iO = ImGui.GetIO();
		ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
		ImU8String val = new ImU8String("##targetType");
		ImU8String val2 = default(ImU8String);
		val2 = new ImU8String(8, 1);
		val2.AppendLiteral("Target: ");
		val2.AppendFormatted<ReactionTargetType>(reaction.TargetType);
		if (ImGui.BeginCombo(val, val2, (ImGuiComboFlags)0))
		{
			foreach (ReactionTargetType value in Enum.GetValues(typeof(ReactionTargetType)))
			{
				bool selected2 = reaction.TargetType == value;
				if (ImGui.Selectable(new ImU8String(value.ToString()), selected2, (ImGuiSelectableFlags)0, default(Vector2)))
				{
					reaction.TargetType = value;
					Plugin.Config.Save();
				}
			}
			ImGui.EndCombo();
		}
		ImGuiEx.SetItemTooltip("Set target when performing reaction:\n\nNone: No target condition will be set, any current target will continue to be targeted.\nUntarget: Remove any current target.\nTarget Instigator/Receiver: Set target as instigator/receiver.\nTarget Self: Set target as yourself.", (ImGuiHoveredFlags)0);
		iO = ImGui.GetIO();
		ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
		ImU8String val3 = new ImU8String("##lookAtType");
		ImU8String val4 = default(ImU8String);
		val4 = new ImU8String(8, 1);
		val4.AppendLiteral("LookAt: ");
		val4.AppendFormatted<ReactionLookAtType>(reaction.LookAtType);
		if (ImGui.BeginCombo(val3, val4, (ImGuiComboFlags)0))
		{
			foreach (ReactionLookAtType value2 in Enum.GetValues(typeof(ReactionLookAtType)))
			{
				bool selected3 = reaction.LookAtType == value2;
				if (ImGui.Selectable(new ImU8String(value2.ToString()), selected3, (ImGuiSelectableFlags)0, default(Vector2)))
				{
					reaction.LookAtType = value2;
					Plugin.Config.Save();
				}
			}
			ImGui.EndCombo();
		}
		ImGuiEx.SetItemTooltip("Control which direction to face in when performing reaction:\n\nTarget: Normal behaviour, face target (if any).\nMaintain: Maintain your current facing direction.\nInstigator/Receiver: Face instigator/receiver.\nInstigator/Receiver Inverse: Face away from instigator/receiver.\nInstigator/Receiver Direction: Face in same direction as instigator/receiver.\nInstigator/Receiver Direction Inverse: Face in opposite direction of instigator/receiver.", (ImGuiHoveredFlags)0);
		return action;
	}

	private void DrawTriggerTextReactions(Trigger trigger)
	{
		if (!ImGuiEx.TreeNode("Text Reactions##textReactions", null, default(Vector4), (ImGuiTreeNodeFlags)0))
		{
			return;
		}
		if (ImGuiEx.IconButton((FontAwesomeIcon)61525, "newTextReaction"))
		{
			if (trigger.Reactions == null)
			{
				List<ReactionBase> list = (trigger.Reactions = new List<ReactionBase>());
			}
			trigger.Reactions.Add(new TextReaction());
			Plugin.Config.Save();
		}
		ImGuiEx.SetItemTooltip("Add new text reaction.", (ImGuiHoveredFlags)0);
		List<ReactionBase> textReaction = ((trigger.Reactions == null) ? null : trigger.Reactions.Where((ReactionBase x) => x is TextReaction).ToList());
		if (trigger.Reactions == null || textReaction == null)
		{
			ImGui.LabelText(new ImU8String("##noTextReactions"), new ImU8String("No Text Reactions Added"));
		}
		else
		{
			if (textReaction.Count > 0)
			{
				ImGui.SameLine();
				if (ImGui.Button(new ImU8String("Preview##previewTexts"), default(Vector2)))
				{
					plugin.TriggerManager.PreviewQueue(trigger);
				}
				ImGuiEx.SetItemTooltip("Preview the current emote/text reactions.\n\n- Preview ignores territory, range, state, and interrupt restrictions.\n- Certain emote options are not able to be previewed such as copying instigator emote.\n- Preview of text reactions will be performed in the echo chat channel.\n- If you have a valid player target, they will be treated as the instigator/receiver depending on \n   event instigator/receiver options above.", (ImGuiHoveredFlags)0);
			}
			Action action = null;
			int i = 1;
			foreach (ReactionBase reaction in textReaction)
			{
				trigger.Reactions.IndexOf(reaction);
				if (ImGuiEx.TreeNode($"{i}. Text Reaction##textReaction{i}", null, default(Vector4), (ImGuiTreeNodeFlags)0))
				{
					action = DrawTextReaction(trigger, textReaction, (TextReaction)reaction);
					ImGui.TreePop();
				}
				i++;
			}
			action?.Invoke();
		}
		ImGui.TreePop();
	}

	private Action? DrawTextReaction(Trigger trigger, List<ReactionBase> textReactions, TextReaction reaction)
	{
		Action action = null;
		if (trigger.Reactions != null && trigger.Reactions.Count != 0)
		{
			if (ImGuiEx.IconButton((FontAwesomeIcon)61610, "moveReactionUp"))
			{
				action = delegate
				{
					int num = trigger.Reactions.IndexOf(reaction);
					trigger.Reactions.RemoveAt(num);
					num = Math.Max(num - 1, 0);
					trigger.Reactions.Insert(num, reaction);
					Plugin.Config.Save();
				};
			}
			ImGuiEx.SetItemTooltip("Move this reaction up the chain.", (ImGuiHoveredFlags)0);
			ImGui.SameLine();
			if (ImGuiEx.IconButton((FontAwesomeIcon)61611, "moveReactionDown"))
			{
				action = delegate
				{
					int num = trigger.Reactions.IndexOf(reaction);
					trigger.Reactions.RemoveAt(num);
					num = Math.Min(num + 1, trigger.Reactions.Count);
					trigger.Reactions.Insert(num, reaction);
					Plugin.Config.Save();
				};
			}
			ImGuiEx.SetItemTooltip("Move this reaction down the chain.", (ImGuiHoveredFlags)0);
			ImGui.SameLine();
			ImGuiEx.IconButton((FontAwesomeIcon)62189, "removeReaction");
			ImGuiEx.SetItemTooltip("Remove this reaction.", (ImGuiHoveredFlags)0);
			if (ImGui.BeginPopupContextItem(new ImU8String("##removeContext"), (ImGuiPopupFlags)0))
			{
				if (ImGuiEx.IconSelectable((FontAwesomeIcon)62189))
				{
					action = delegate
					{
						int num = trigger.Reactions.IndexOf(reaction);
						trigger.Reactions.RemoveAt(num);
						num = Math.Min(num, trigger.Reactions.Count - 1);
						Plugin.Config.Save();
					};
				}
				ImGui.EndPopup();
			}
		}
		int textReactionIndex = textReactions.IndexOf(reaction);
		ImGuiIOPtr iO = ImGui.GetIO();
		ImGui.SetNextItemWidth(80f * iO.FontGlobalScale);
		if (ImGuiEx.DragInt("Delay before##reactionDelay", reaction.Delay, delegate(int x)
		{
			reaction.Delay = x;
		}))
		{
			Plugin.Config.Save();
		}
		ImGuiEx.SetItemTooltip((textReactionIndex == 0 ? "Delay before this message from when the event is triggered." : "Delay before this message after the previous reaction finishes.") + "\nValues are milliseconds: 1000 = 1 second, 60000 = 1 minute.", (ImGuiHoveredFlags)0);
		ImGui.SameLine();
		iO = ImGui.GetIO();
		ImGui.SetNextItemWidth(80f * iO.FontGlobalScale);
		if (ImGuiEx.DragInt("Wait after##reactionDuration", reaction.Duration, delegate(int x)
		{
			reaction.Duration = x;
		}, 1f, 500))
		{
			Plugin.Config.Save();
		}
		ImGuiEx.SetItemTooltip("Wait after this message before the next reaction may run.\nValues are milliseconds: 1000 = 1 second, 60000 = 1 minute.", (ImGuiHoveredFlags)0);
		ImGui.BeginDisabled(reaction.SameChannel);
		iO = ImGui.GetIO();
		ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
		_ = reaction.Channel;
		if (ImGui.BeginCombo(new ImU8String("##reactionChannels"), new ImU8String(reaction.SameChannel ? "Copy Instigator" : ((reaction.Channel != ChatType.None) ? $"Channel: {reaction.Channel}" : "No Channel Selected")), (ImGuiComboFlags)0))
		{
			foreach (ChatType value in Enum.GetValues(typeof(ChatType)))
			{
				if (value == ChatType.None || value == ChatType.Emote)
				{
					continue;
				}
				bool selected = reaction.Channel == value;
				ImGuiEx.IconCheckbox(selected);
				ImGui.SameLine();
				ImU8String val = new ImU8String(0, 1);
				val.AppendFormatted<ChatType>(value);
				if (ImGui.Selectable(val, selected, (ImGuiSelectableFlags)1, default(Vector2)))
				{
					if (selected)
					{
						reaction.Channel &= ~value;
					}
					else
					{
						reaction.Channel = value;
					}
					Plugin.Config.Save();
				}
			}
			ImGui.EndCombo();
		}
		ImGui.EndDisabled();
		ImGuiEx.SetItemTooltip("Select a chat channel to send this reaction to when this event is triggered.\nThe 'Command' channel can be used for performing vanilla/plugin commands.", (ImGuiHoveredFlags)0);
		if (trigger.Type == TriggerType.Text)
		{
			Instigator? instigator = trigger.Instigator;
			if (instigator != null && instigator.Type == PlayerType.Self)
			{
				ChannelTextReceiver receiver = trigger.Receiver as ChannelTextReceiver;
				if (reaction.SameChannel)
				{
					ImGui.SameLine();
					ImGuiEx.IconAlertTooltip("This reaction will be ignored with the current properties to prevent crashing.\nUnable to send message to same chat channel when instigator is self.");
				}
				else if (reaction.Channel != ChatType.Echo && reaction.Channel != ChatType.Command && ((receiver != null && receiver.MatchAny) || (receiver != null && receiver.Channel.HasFlag(reaction.Channel))))
				{
					ImGui.SameLine();
					ImGuiEx.IconWarningTooltip("This reaction may be ignored with the current properties to prevent crashing.\nUnable to send message to same chat channel when instigator is self.\nWill only trigger if the reaction channel is not the same as the receiving channel.");
				}
			}
			ImGui.SameLine();
			if (ImGuiEx.Checkbox("Copy Instigator##copyInstigator", reaction.SameChannel, delegate(bool x)
			{
				reaction.SameChannel = x;
			}))
			{
				Plugin.Config.Save();
			}
			ImGuiEx.SetItemTooltip("Copy the chat channel used by the instigator.", (ImGuiHoveredFlags)0);
		}
		else if (reaction.SameChannel)
		{
			reaction.SameChannel = false;
			Plugin.Config.Save();
		}
		ImGui.BeginDisabled(reaction.CopyInstigator);
		iO = ImGui.GetIO();
		ImGui.SetNextItemWidth(160f * iO.FontGlobalScale);
		if (reaction.CopyInstigator)
		{
			ImGuiEx.InputText("##messageTemplate", "Copy Instigator", delegate(string _)
			{
			}, 450);
		}
		else if (ImGuiEx.InputText("##messageTemplate", reaction.Template, delegate(string x)
		{
			reaction.Template = x;
		}, 450))
		{
			Plugin.Config.Save();
		}
		ImGui.EndDisabled();
		ImGuiEx.SetItemTooltip("The message to send to the selected channel, with the below formatting:\n%ifn%/%isn% - Instigator Forename/Surname", (ImGuiHoveredFlags)0);
		if (trigger.Type == TriggerType.Text)
		{
			ImGui.SameLine();
			if (ImGuiEx.Checkbox("Copy Instigator##copyInstigatorMessage", reaction.CopyInstigator, delegate(bool x)
			{
				reaction.CopyInstigator = x;
			}))
			{
				Plugin.Config.Save();
			}
			ImGuiEx.SetItemTooltip("Copy the message sent by the instigator.", (ImGuiHoveredFlags)0);
		}
		else if (reaction.CopyInstigator)
		{
			reaction.CopyInstigator = false;
			Plugin.Config.Save();
		}
		return action;
	}

	public override void OnClose()
	{
		DrawRangePreview = false;
		base.OnClose();
	}
}
