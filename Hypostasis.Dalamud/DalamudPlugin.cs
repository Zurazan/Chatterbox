using System;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using OnUpdateDelegate = Dalamud.Plugin.Services.IFramework.OnUpdateDelegate;

namespace Hypostasis.Dalamud;

public abstract class DalamudPlugin : IDisposable
{
	private readonly PluginCommandManager pluginCommandManager;

	protected DalamudPlugin(IDalamudPluginInterface pluginInterface)
	{
		try
		{
			Hypostasis.Initialize((IDalamudPlugin)(object)(((this is IDalamudPlugin) ? this : null) ?? throw new ApplicationException("A DalamudPlugin MUST implement IDalamudPlugin!")), pluginInterface);
			SetupConfig();
			pluginCommandManager = new PluginCommandManager(this);
		}
		catch (Exception exception)
		{
			DalamudApi.LogError("Failed loading Hypostasis for " + Hypostasis.PluginName, exception);
			Dispose();
			Hypostasis.State = Hypostasis.PluginState.Failed;
			return;
		}
		try
		{
			DalamudApi.SigScanner.InjectSignatures();
			Initialize();
			if (!PluginModuleManager.Initialize())
			{
				DalamudApi.ShowNotification("One or more modules failed to load,\nplease check /xllog for more info", (NotificationType)2, 10000u);
			}
			Type type = GetType();
			if (type.DeclaresMethod("Update"))
			{
				DalamudApi.Framework.Update += new OnUpdateDelegate(Update);
			}
			if (type.DeclaresMethod("Draw"))
			{
				DalamudApi.PluginInterface.UiBuilder.Draw += Draw;
			}
			if (type.DeclaresMethod("ToggleConfig"))
			{
				DalamudApi.PluginInterface.UiBuilder.OpenConfigUi += ToggleConfig;
			}
			Hypostasis.State = Hypostasis.PluginState.Loaded;
		}
		catch (Exception exception2)
		{
			string msg = "Failed loading " + Hypostasis.PluginName;
			DalamudApi.LogError(msg, exception2);
			DalamudApi.ShowNotification("\t\t\t" + msg + "\t\t\t\n\n", (NotificationType)3, 10000u);
			DalamudApi.ShowErrorToast(msg);
			DalamudApi.PrintError(msg);
			Dispose();
			Hypostasis.State = Hypostasis.PluginState.Failed;
		}
	}

	protected virtual void Initialize()
	{
	}

	protected virtual void ToggleConfig()
	{
	}

	protected virtual void Update()
	{
	}

	private void Update(IFramework framework)
	{
		Update();
	}

	protected virtual void Draw()
	{
	}

	protected virtual void SetupConfig()
	{
	}

	protected virtual void DisposeConfig()
	{
	}

	protected virtual void Dispose(bool disposing)
	{
	}

	public void Dispose()
	{
		bool failed = Hypostasis.State == Hypostasis.PluginState.Loading;
		Hypostasis.State = Hypostasis.PluginState.Unloading;
		DisposeConfig();
		DalamudApi.Framework.Update -= new OnUpdateDelegate(Update);
		DalamudApi.PluginInterface.UiBuilder.Draw -= Draw;
		DalamudApi.PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfig;
		try
		{
			Dispose(disposing: true);
		}
		finally
		{
			pluginCommandManager?.Dispose();
			Hypostasis.Dispose(failed);
			Hypostasis.State = Hypostasis.PluginState.Unloaded;
			GC.SuppressFinalize(this);
		}
	}
}
public abstract class DalamudPlugin<C> : DalamudPlugin where C : PluginConfiguration, new()
{
	public static C Config { get; private set; }

	protected DalamudPlugin(IDalamudPluginInterface pluginInterface)
		: base(pluginInterface)
	{
	}

	protected sealed override void SetupConfig()
	{
		Config = PluginConfiguration.LoadConfig<C>();
	}

	protected sealed override void DisposeConfig()
	{
		Config?.Save();
	}
}
public abstract class DalamudPlugin<P, C> : DalamudPlugin<C> where P : DalamudPlugin where C : PluginConfiguration, new()
{
	public static P Plugin { get; private set; }

	protected DalamudPlugin(IDalamudPluginInterface pluginInterface)
		: base(pluginInterface)
	{
		Plugin = this as P;
	}
}
