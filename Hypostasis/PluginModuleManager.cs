using System;
using System.Collections.Generic;
using System.Linq;
using Hypostasis.Dalamud;

namespace Hypostasis;

public static class PluginModuleManager
{
	private static readonly Dictionary<Type, PluginModule> pluginModules = new Dictionary<Type, PluginModule>();

	public static IEnumerable<PluginModule> PluginModules => pluginModules.Values;

	public static bool Initialize()
	{
		bool succeeded = true;
		foreach (Type t in Util.Assembly.GetTypes<PluginModule>())
		{
			PluginModule pluginModule = (PluginModule)Activator.CreateInstance(t);
			if (pluginModule == null)
			{
				continue;
			}
			if (pluginModule.IsValid)
			{
				if (pluginModule.ShouldEnable)
				{
					ToggleOrInvalidateModule(pluginModule, Hypostasis.IsDebug);
				}
			}
			else
			{
				DalamudApi.LogWarning($"{t} failed to load!");
				succeeded = false;
			}
			pluginModules.Add(t, pluginModule);
		}
		return succeeded;
	}

	public static T GetModule<T>() where T : PluginModule
	{
		return (T)pluginModules[typeof(T)];
	}

	public static void CheckModules()
	{
		foreach (PluginModule item in pluginModules.Values.Where((PluginModule pluginModule) => pluginModule.IsValid && pluginModule.ShouldEnable != pluginModule.IsEnabled))
		{
			ToggleOrInvalidateModule(item, logInfo: true);
		}
	}

	public static void ToggleOrInvalidateModule(PluginModule pluginModule, bool logInfo)
	{
		try
		{
			pluginModule.Toggle();
			if (logInfo)
			{
				DalamudApi.LogInfo(pluginModule.IsEnabled ? $"Enabled plugin module: {pluginModule}" : $"Disabled plugin module: {pluginModule}");
			}
		}
		catch (Exception exception)
		{
			DalamudApi.LogError($"Error in plugin module: {pluginModule}", exception);
			pluginModule.IsValid = false;
		}
	}

	public static void Dispose()
	{
		foreach (PluginModule pluginModule in pluginModules.Values.Where((PluginModule pluginModule2) => pluginModule2.IsValid))
		{
			if (pluginModule.IsEnabled)
			{
				pluginModule.Toggle();
			}
			pluginModule.Dispose();
		}
	}
}
