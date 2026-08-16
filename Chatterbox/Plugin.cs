using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Hypostasis.Dalamud;
using Hypostasis.Game;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Extensions;
using Lumina.Text.ReadOnly;
using Newtonsoft.Json;
using LuminaEmote = Lumina.Excel.Sheets.Emote;
using OnUpdateDelegate = Dalamud.Plugin.Services.IFramework.OnUpdateDelegate;
using HandlerDelegate = Dalamud.Game.Command.IReadOnlyCommandInfo.HandlerDelegate;
using OnChatMessageDelegate = Dalamud.Plugin.Services.IChatGui.OnChatMessageDelegate;

namespace Chatterbox;

public class Plugin : IDalamudPlugin, IDisposable
{
	private const string CommandName = "/chatterbox";

	private const string AltCommandName = "/trigger";

	public List<Emote> Emotes;

	public static List<SpecialEmote> SpecialEmotes = new List<SpecialEmote>();

	public static List<string> Worlds = new List<string>();

	public static HashSet<ResidentialTerritory> ResidentialTerritories = new HashSet<ResidentialTerritory>();

	public static HashSet<NonResidentialTerritory> NonResidentialTerritories = new HashSet<NonResidentialTerritory>();

	public static IList<JsonConverter> Converters = new List<JsonConverter>
	{
		(JsonConverter)(object)new ActionBaseConverter(),
		(JsonConverter)(object)new CounterBaseConverter(),
		(JsonConverter)(object)new ReceiverBaseConverter(),
		(JsonConverter)(object)new ReactionBaseConverter()
	};

	private WindowSystem Windows;

	public MainWindow MainWindow;

	private bool disposed;

	internal bool IsDisposed => disposed;

	public string Name => "Chatterbox";

	[PluginService]
	internal static IPluginLog Log { get; private set; } = null;

	[PluginService]
	internal static IClientState ClientState { get; private set; } = null;

	[PluginService]
	internal static IDalamudPluginInterface PluginInterface { get; private set; } = null;

	[PluginService]
	internal ICommandManager CommandManager { get; init; } = null!;

	[PluginService]
	internal static IGameInteropProvider GameInteropProvider { get; private set; } = null;

	[PluginService]
	internal ICondition Condition { get; init; }

	[PluginService]
	internal static IFramework Framework { get; private set; } = null;

	[PluginService]
	internal static IObjectTable Objects { get; private set; } = null;

	[PluginService]
	internal static ITargetManager Targets { get; private set; } = null;

	[PluginService]
	internal static IDataManager DataManager { get; private set; } = null;

	[PluginService]
	internal static IGameGui GameGui { get; private set; } = null;

	[PluginService]
	internal static IChatGui ChatGui { get; private set; } = null;

	[PluginService]
	internal static IToastGui ToastGui { get; private set; } = null;

	[PluginService]
	internal ISigScanner ISigScanner { get; init; }

	public SigScannerWrapper SigScanner { get; private set; }

	public ExcelSheet<TerritoryType> TerritorySheet { get; init; }

	public ExcelSheet<LuminaEmote> EmoteSheet { get; init; }

	public Honorific? Honorific { get; private set; }

	public EmoteHook EmoteHook { get; private set; }

	public Chat Chat { get; init; }

	public TriggerManager TriggerManager { get; private set; }

	public static Config Config { get; private set; } = null;

	public Plugin()
	{
		Emotes = new List<Emote>();
		CommandManager.AddHandler(CommandName, new CommandInfo(new HandlerDelegate(OnCommand))
		{
			HelpMessage = "Open Plugin Interface."
		});
		CommandManager.AddHandler(AltCommandName, new CommandInfo(new HandlerDelegate(OnCommand))
		{
			HelpMessage = "Open Plugin Interface."
		});
		SigScanner = new SigScannerWrapper(ISigScanner);
		SigScanner.InjectSignatures();
		Common.Initialize();
		Config = LoadConfig();
		int removedLegacyTriggers = Config.Triggers.RemoveAll((Trigger trigger) => (uint)trigger.Type == 3u);
		if (removedLegacyTriggers > 0)
		{
			Log.Info($"Removed {removedLegacyTriggers} unsupported legacy activity trigger(s).", Array.Empty<object>());
			Config.Save();
		}
		Windows = new WindowSystem(Name);
		MainWindow mainWindow = new MainWindow(this);
		((Window)mainWindow).IsOpen = false;
		MainWindow = mainWindow;
		Windows.AddWindow((IWindow)(object)MainWindow);
		TerritorySheet = DataManager.GetExcelSheet<TerritoryType>((ClientLanguage?)null, (string)null);
		EmoteSheet = DataManager.GetExcelSheet<LuminaEmote>((ClientLanguage?)null, (string)null);
		InitializeEmotes();
		InitializeWorlds();
		InitializeTerritories();
		Mare.Initialize();
		Honorific = new Honorific();
		EmoteHook = new EmoteHook(this);
		Chat = new Chat(this);
		ChatGui.ChatMessageUnhandled += new OnChatMessageDelegate(Chat.OnChatMessage);
		TriggerManager = new TriggerManager(this);
		PluginInterface.UiBuilder.DisableGposeUiHide = true;
		PluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
		PluginInterface.UiBuilder.Draw += Windows.Draw;
		Framework.Update += new OnUpdateDelegate(Framework_Update);
	}

	private void Framework_Update(IFramework framework)
	{
		if (disposed)
		{
			return;
		}
		PlayerManager.UpdatePlayerList();
		TriggerManager.Update();
		Honorific?.Update();
	}

	private Config LoadConfig()
	{
		Config config = new Config();
		FileInfo configFile = PluginInterface.ConfigFile;
		if (configFile != null && configFile.Exists)
		{
			config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(configFile.FullName), new JsonSerializerSettings
			{
				Converters = Converters
			}) ?? new Config();
		}
		config.Triggers ??= new List<Trigger>();
		config.Triggers.RemoveAll((Trigger trigger) => trigger == null);
		return config;
	}

	private void InitializeEmotes()
	{
		SpecialEmotes = new List<SpecialEmote>
		{
			new SpecialEmote(0, "[P] Idle 0", triggersEmoteHook: false),
			new SpecialEmote(91, "[P] Idle 1", triggersEmoteHook: false),
			new SpecialEmote(92, "[P] Idle 2", triggersEmoteHook: false),
			new SpecialEmote(107, "[P] Idle 3", triggersEmoteHook: false),
			new SpecialEmote(108, "[P] Idle 4", triggersEmoteHook: false),
			new SpecialEmote(218, "[P] Idle 5", triggersEmoteHook: false),
			new SpecialEmote(219, "[P] Idle 6", triggersEmoteHook: false),
			new SpecialEmote(52, "[P] Sit Ground 0", triggersEmoteHook: true),
			new SpecialEmote(97, "[P] Sit Ground 1", triggersEmoteHook: false),
			new SpecialEmote(98, "[P] Sit Ground 2", triggersEmoteHook: false),
			new SpecialEmote(117, "[P] Sit Ground 3", triggersEmoteHook: false),
			new SpecialEmote(50, "[P] Sit Chair 0", triggersEmoteHook: true),
			new SpecialEmote(95, "[P] Sit Chair 1 (Anywhere)", triggersEmoteHook: true),
			new SpecialEmote(96, "[P] Sit Chair 2", triggersEmoteHook: false),
			new SpecialEmote(254, "[P] Sit Chair 3", triggersEmoteHook: false),
			new SpecialEmote(255, "[P] Sit Chair 4", triggersEmoteHook: false),
			new SpecialEmote(88, "[P] Sleep 0", triggersEmoteHook: true),
			new SpecialEmote(99, "[P] Sleep 1", triggersEmoteHook: false),
			new SpecialEmote(100, "[P] Sleep 2", triggersEmoteHook: false),
			new SpecialEmote(51, "[P] Stand (Chair)", triggersEmoteHook: true),
			new SpecialEmote(53, "[P] Stand (Ground)", triggersEmoteHook: true),
			new SpecialEmote(89, "[P] Stand (Sleep)", triggersEmoteHook: true)
		};
		var enumerator = EmoteSheet.GetEnumerator();
		try
		{
			while (enumerator.MoveNext())
			{
				LuminaEmote exEmote = enumerator.Current;
				string name = ((object)exEmote.Name/*cast due to constrained. prefix*/).ToString();
				SpecialEmote sEmote = SpecialEmotes.FirstOrDefault((SpecialEmote x) => x.ID == exEmote.RowId);
				List<Emote> emotes = Emotes;
				ushort id = (ushort)exEmote.RowId;
				string name2 = ((exEmote.RowId == 146) ? "Dote (Targeted)" : ((exEmote.RowId == 147) ? "Dote (Untargeted)" : ((sEmote != null && exEmote.RowId == sEmote.ID) ? sEmote.Name : (string.IsNullOrWhiteSpace(name) ? $"Unknown-{exEmote.RowId}" : name))));
				TextCommand? valueNullable = exEmote.TextCommand.ValueNullable;
				object command;
				if (!valueNullable.HasValue)
				{
					command = null;
				}
				else
				{
					TextCommand valueOrDefault = valueNullable.GetValueOrDefault();
					command = ((object)valueOrDefault.Command/*cast due to constrained. prefix*/).ToString();
				}
				emotes.Add(new Emote(id, name2, (string?)command, sEmote != null, sEmote?.TriggersEmoteHook ?? true));
			}
		}
		finally
		{
			((IDisposable)enumerator/*cast due to constrained. prefix*/).Dispose();
		}
		Emotes.Sort((Emote a, Emote b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
	}

	private void InitializeWorlds()
	{
		Worlds.Clear();
		var enumerator = DataManager.GetExcelSheet<World>((ClientLanguage?)null, (string)null).GetEnumerator();
		try
		{
			while (enumerator.MoveNext())
			{
				World world = enumerator.Current;
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
				uint? region = num;
				if (region < 1 || region > 4)
				{
					continue;
				}
				string worldName = world.Name.ToString() ?? string.Empty;
				if (!string.IsNullOrEmpty(worldName) && !worldName.Contains('-'))
				{
					Worlds.Add(worldName);
				}
			}
		}
		finally
		{
			((IDisposable)enumerator/*cast due to constrained. prefix*/).Dispose();
		}
	}

	private void InitializeTerritories()
	{
		ResidentialTerritories = new HashSet<ResidentialTerritory>
		{
			new ResidentialTerritory(136u, "Mist", ResidentialType.Ward),
			new ResidentialTerritory(282u, "Private Cottage - Mist", ResidentialType.House),
			new ResidentialTerritory(283u, "Private House - Mist", ResidentialType.House),
			new ResidentialTerritory(284u, "Private Mansion - Mist", ResidentialType.House),
			new ResidentialTerritory(384u, "Private Chambers - Mist", ResidentialType.Chambers),
			new ResidentialTerritory(423u, "Company Workshop - Mist", ResidentialType.Workshop),
			new ResidentialTerritory(573u, "Topmast Apartment Lobby", ResidentialType.ApartmentLobby),
			new ResidentialTerritory(608u, "Topmast Apartment", ResidentialType.Apartment),
			new ResidentialTerritory(340u, "The Lavender Beds", ResidentialType.Ward),
			new ResidentialTerritory(342u, "Private Cottage - The Lavender Beds", ResidentialType.House),
			new ResidentialTerritory(343u, "Private House - The Lavender Beds", ResidentialType.House),
			new ResidentialTerritory(344u, "Private Mansion - The Lavender Beds", ResidentialType.House),
			new ResidentialTerritory(385u, "Private Chambers - The Lavender Beds", ResidentialType.Chambers),
			new ResidentialTerritory(425u, "Company Workshop - The Lavender Beds", ResidentialType.Workshop),
			new ResidentialTerritory(574u, "Lily Hills Apartment Lobby", ResidentialType.ApartmentLobby),
			new ResidentialTerritory(609u, "Lily Hills Apartment", ResidentialType.Apartment),
			new ResidentialTerritory(341u, "The Goblet", ResidentialType.Ward),
			new ResidentialTerritory(345u, "Private Cottage - The Goblet", ResidentialType.House),
			new ResidentialTerritory(346u, "Private House -  The Goblet", ResidentialType.House),
			new ResidentialTerritory(347u, "Private Mansion -  The Goblet", ResidentialType.House),
			new ResidentialTerritory(386u, "Private Chambers - The Goblet", ResidentialType.Chambers),
			new ResidentialTerritory(424u, "Company Workshop - The Goblet", ResidentialType.Workshop),
			new ResidentialTerritory(575u, "Sultana's Breath Apartment Lobby", ResidentialType.ApartmentLobby),
			new ResidentialTerritory(610u, "Sultana's Breath Apartment", ResidentialType.Apartment),
			new ResidentialTerritory(641u, "Shirogane", ResidentialType.Ward),
			new ResidentialTerritory(649u, "Private Cottage - Shirogane", ResidentialType.House),
			new ResidentialTerritory(650u, "Private House - Shirogane", ResidentialType.House),
			new ResidentialTerritory(651u, "Private Mansion - Shirogane", ResidentialType.House),
			new ResidentialTerritory(652u, "Private Chambers - Shirogane", ResidentialType.Chambers),
			new ResidentialTerritory(653u, "Company Workshop - Shirogane", ResidentialType.Workshop),
			new ResidentialTerritory(654u, "Kobai Goten Apartment Lobby", ResidentialType.ApartmentLobby),
			new ResidentialTerritory(655u, "Kobai Goten Apartment", ResidentialType.Apartment),
			new ResidentialTerritory(979u, "Empyreum", ResidentialType.Ward),
			new ResidentialTerritory(980u, "Private Cottage - Empyreum", ResidentialType.House),
			new ResidentialTerritory(981u, "Private House - Empyreum", ResidentialType.House),
			new ResidentialTerritory(982u, "Private Mansion - Empyreum", ResidentialType.House),
			new ResidentialTerritory(983u, "Private Chambers - Empyreum", ResidentialType.Chambers),
			new ResidentialTerritory(984u, "Company Workshop - Empyreum", ResidentialType.Workshop),
			new ResidentialTerritory(985u, "Ingleside Apartment Lobby", ResidentialType.ApartmentLobby),
			new ResidentialTerritory(999u, "Ingleside Apartment", ResidentialType.Apartment)
		};
		TerritoryType res = default(TerritoryType);
		PlaceName valueOrDefault;
		foreach (ResidentialTerritory rt in ResidentialTerritories)
		{
			if (LinqExtensions.TryGetFirst<TerritoryType>((IEnumerable<TerritoryType>)TerritorySheet, (Predicate<TerritoryType>)((TerritoryType x) => x.RowId == rt.Id), out res))
			{
				PlaceName? valueNullable = res.PlaceName.ValueNullable;
				object obj;
				if (!valueNullable.HasValue)
				{
					obj = null;
				}
				else
				{
					valueOrDefault = valueNullable.GetValueOrDefault();
					obj = SeStringExtensions.ToDalamudString(valueOrDefault.Name).TextValue;
				}
				string name = (string)obj;
				rt.Name = name ?? rt.Name;
			}
		}
		var enumerator2 = TerritorySheet.GetEnumerator();
		try
		{
			while (enumerator2.MoveNext())
			{
				TerritoryType ter = enumerator2.Current;
				PlaceName? valueNullable = ter.PlaceName.ValueNullable;
				object obj2;
				if (!valueNullable.HasValue)
				{
					obj2 = null;
				}
				else
				{
					valueOrDefault = valueNullable.GetValueOrDefault();
					ReadOnlySeString name2 = valueOrDefault.Name;
					obj2 = name2.ExtractText();
				}
				string placeName = (string)obj2;
				if (!string.IsNullOrWhiteSpace(placeName) && ResidentialTerritories.FirstOrDefault((ResidentialTerritory x) => x.Id == ter.RowId) == null)
				{
					NonResidentialTerritories.Add(new NonResidentialTerritory(ter.RowId, placeName));
				}
			}
		}
		finally
		{
			((IDisposable)enumerator2/*cast due to constrained. prefix*/).Dispose();
		}
	}

	public bool TryGetCurrentTerritory(out TerritoryType res)
	{
		res = default(TerritoryType);
		return LinqExtensions.TryGetFirst<TerritoryType>((IEnumerable<TerritoryType>)TerritorySheet, (Predicate<TerritoryType>)((TerritoryType x) => x.RowId == ClientState.TerritoryType), out res);
	}

	private void OnCommand(string command, string args)
	{
		((Window)MainWindow).IsOpen = !((Window)MainWindow).IsOpen;
	}

	private void OpenConfigUi()
	{
		((Window)MainWindow).IsOpen = true;
	}

	public bool HasInvalidConditionForTitle()
	{
		if (!Condition[(ConditionFlag)70] && !Condition[(ConditionFlag)45] && !Condition[(ConditionFlag)51] && !Condition[(ConditionFlag)86] && !Condition[(ConditionFlag)53] && !Condition[(ConditionFlag)35] && !Condition[(ConditionFlag)31] && !Condition[(ConditionFlag)32] && !Condition[(ConditionFlag)92] && !Condition[(ConditionFlag)93] && !Condition[(ConditionFlag)58])
		{
			return Condition[(ConditionFlag)78];
		}
		return true;
	}

	public void Dispose()
	{
		if (disposed)
		{
			return;
		}
		disposed = true;
		ChatGui.ChatMessageUnhandled -= new OnChatMessageDelegate(Chat.OnChatMessage);
		Framework.Update -= new OnUpdateDelegate(Framework_Update);
		PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
		PluginInterface.UiBuilder.Draw -= Windows.Draw;
		Windows.RemoveAllWindows();
		CommandManager.RemoveHandler(CommandName);
		CommandManager.RemoveHandler(AltCommandName);
		TriggerManager.Dispose();
		EmoteHook.Dispose();
		Honorific?.Dispose();
		Mare.Dispose();
		Common.Dispose();
		SigScanner.Dispose();
	}
}
