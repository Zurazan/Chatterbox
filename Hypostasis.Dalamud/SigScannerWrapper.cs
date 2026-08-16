using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Game;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using HookBackend = Dalamud.Plugin.Services.IGameInteropProvider.HookBackend;

namespace Hypostasis.Dalamud;

public class SigScannerWrapper(ISigScanner s) : IDisposable
{
	private readonly Dictionary<string, nint> sigCache = new Dictionary<string, nint>();

	private readonly Dictionary<string, nint> staticSigCache = new Dictionary<string, nint>();

	private readonly List<IDisposable> disposableHooks = new List<IDisposable>();

	public ISigScanner DalamudSigScanner { get; init; } = s;

	public ProcessModule Module => DalamudSigScanner.Module;

	public nint BaseAddress => Module.BaseAddress;

	public nint BaseTextAddress => (nint)(BaseAddress + DalamudSigScanner.TextSectionOffset);

	public nint BaseDataAddress => (nint)(BaseAddress + DalamudSigScanner.DataSectionOffset);

	public nint BaseRDataAddress => (nint)(BaseAddress + DalamudSigScanner.RDataSectionOffset);

	public nint Scan(nint address, int size, string signature)
	{
		int num;
		if (address >= BaseAddress)
		{
			num = ((address < BaseRDataAddress) ? 1 : 0);
			if (num != 0)
			{
				address = (nint)DalamudSigScanner.SearchBase + (address - BaseAddress);
			}
		}
		else
		{
			num = 0;
		}
		nint ret = SigScanner.Scan((IntPtr)address, size, signature);
		if (num != 0 && ret >= (nint)DalamudSigScanner.SearchBase)
		{
			ret = BaseAddress + (ret - (nint)DalamudSigScanner.SearchBase);
		}
		return ret;
	}

	public nint Scan(nint address, nint endAddress, string signature)
	{
		return Scan(address, (int)(endAddress - address), signature);
	}

	public bool TryScan(nint address, int size, string signature, out nint result)
	{
		bool scanCopy = address >= BaseAddress && address < BaseRDataAddress;
		if (scanCopy)
		{
			address = (nint)DalamudSigScanner.SearchBase + (address - BaseAddress);
		}
		bool result2 = SigScanner.TryScan((IntPtr)address, size, signature, out result);
		if (scanCopy && result >= (nint)DalamudSigScanner.SearchBase)
		{
			result = BaseAddress + (result - (nint)DalamudSigScanner.SearchBase);
		}
		return result2;
	}

	public bool TryScan(nint address, nint endAddress, string signature, out nint result)
	{
		return TryScan(address, (int)(endAddress - address), signature, out result);
	}

	public nint ScanText(string signature)
	{
		if (sigCache.TryGetValue(signature, out var ptr))
		{
			return ptr;
		}
		ptr = DalamudSigScanner.ScanText(signature);
		AddSignatureInfo(signature, ptr, 0, stc: false);
		return ptr;
	}

	public bool TryScanText(string signature, out nint result)
	{
		if (sigCache.TryGetValue(signature, out result))
		{
			return true;
		}
		bool result2 = DalamudSigScanner.TryScanText(signature, out result);
		AddSignatureInfo(signature, result, 0, stc: false);
		return result2;
	}

	public nint ScanData(string signature)
	{
		if (sigCache.TryGetValue(signature, out var ptr))
		{
			return ptr;
		}
		ptr = DalamudSigScanner.ScanData(signature);
		AddSignatureInfo(signature, ptr, 0, stc: false);
		return ptr;
	}

	public bool TryScanData(string signature, out nint result)
	{
		if (sigCache.TryGetValue(signature, out result))
		{
			return true;
		}
		bool result2 = DalamudSigScanner.TryScanData(signature, out result);
		AddSignatureInfo(signature, result, 0, stc: false);
		return result2;
	}

	public nint ScanModule(string signature)
	{
		if (sigCache.TryGetValue(signature, out var ptr))
		{
			return ptr;
		}
		ptr = DalamudSigScanner.ScanModule(signature);
		AddSignatureInfo(signature, ptr, 0, stc: false);
		return ptr;
	}

	public bool TryScanModule(string signature, out nint result)
	{
		if (sigCache.TryGetValue(signature, out result))
		{
			return true;
		}
		bool result2 = DalamudSigScanner.TryScanModule(signature, out result);
		AddSignatureInfo(signature, result, 0, stc: false);
		return result2;
	}

	public nint ScanStaticAddress(string signature, int offset = 0)
	{
		if (offset == 0 && staticSigCache.TryGetValue(signature, out var ptr))
		{
			return ptr;
		}
		ptr = DalamudSigScanner.GetStaticAddressFromSig(signature, offset);
		AddSignatureInfo(signature, ptr, offset, stc: true);
		return ptr;
	}

	public bool TryScanStaticAddress(string signature, out nint result, int offset = 0)
	{
		if (offset == 0 && staticSigCache.TryGetValue(signature, out result))
		{
			return true;
		}
		bool result2 = DalamudSigScanner.TryGetStaticAddressFromSig(signature, out result, offset);
		AddSignatureInfo(signature, result, offset, stc: true);
		return result2;
	}

	private Hook<T> HookAddress<T>(nint address, T detour, bool startEnabled = true, bool autoDispose = true, HookBackend backend = (HookBackend)0) where T : Delegate
	{
		Hook<T> hook = DalamudApi.GameInteropProvider.HookFromAddress<T>((IntPtr)address, detour, backend);
		AddHook<T>(hook, startEnabled, autoDispose);
		return hook;
	}

	private Hook<T> HookSignature<T>(string signature, T detour, bool scanModule = false, bool startEnabled = true, bool autoDispose = true, HookBackend backend = (HookBackend)0) where T : Delegate
	{
		nint address = ((!scanModule) ? DalamudSigScanner.ScanText(signature) : DalamudSigScanner.ScanModule(signature));
		Hook<T> hook = DalamudApi.GameInteropProvider.HookFromAddress<T>((IntPtr)address, detour, backend);
		AddSignatureInfo(signature, address, 0, stc: false);
		AddHook<T>(hook, startEnabled, autoDispose);
		return hook;
	}

	private void AddSignatureInfo(string signature, nint ptr, int offset, bool stc)
	{
		if (!stc)
		{
			sigCache[signature] = ptr;
		}
		else
		{
			staticSigCache[signature] = ptr;
		}
	}

	public void InjectSignatures()
	{
		foreach (var item in Util.Assembly.GetTypesWithAttribute<HypostasisInjectionAttribute>())
		{
			Type t = item.Item1;
			Inject(t);
		}
	}

	public void Inject(Type type, object o = null)
	{
		foreach (MemberInfo memberInfo in type.GetAllMembers().Where(delegate(MemberInfo memberInfo2)
		{
			MemberTypes memberType = memberInfo2.MemberType;
			return (memberType == MemberTypes.Field || memberType == MemberTypes.Property) ? true : false;
		}))
		{
			InjectMember(o, memberInfo);
		}
	}

	public void Inject(object o)
	{
		Inject(o.GetType(), o);
	}

	public void InjectMember(object o, MemberInfo memberInfo)
	{
		HypostasisMemberInjectionAttribute attribute = memberInfo.GetCustomAttribute<HypostasisMemberInjectionAttribute>();
		if (attribute == null)
		{
			return;
		}
		if (!(attribute is HypostasisSignatureInjectionAttribute sigAttribute))
		{
			if (attribute is HypostasisClientStructsInjectionAttribute csAttribute)
			{
				InjectClientStructs(o, memberInfo, csAttribute);
			}
		}
		else
		{
			InjectSignature(o, memberInfo, sigAttribute);
		}
	}

	private void InjectSignature(object o, MemberInfo memberInfo, HypostasisSignatureInjectionAttribute sigAttribute)
	{
		Util.AssignableInfo assignableInfo = new Util.AssignableInfo(o, memberInfo);
		string signature = sigAttribute.Signature;
		bool stc = sigAttribute.Static;
		nint address = default(nint);
		if ((!stc) ? (!DalamudSigScanner.TryScanText(signature, out address)) : (!DalamudSigScanner.TryGetStaticAddressFromSig(signature, out address, 0)))
		{
			LogInjectError(memberInfo, $"Failed to find signature: \"{signature}\" (Static: {stc})", sigAttribute.Required);
		}
		else
		{
			InjectAddress(assignableInfo, address, sigAttribute);
		}
	}

	private void InjectClientStructs(object o, MemberInfo memberInfo, HypostasisClientStructsInjectionAttribute csAttribute)
	{
		string memberName = (memberInfo.Name.EndsWith("Hook") ? memberInfo.Name.Replace("Hook", string.Empty) : csAttribute.MemberName);
		MemberInfo csMember = csAttribute.ClientStructsType.GetMember(memberName)[0];
		Util.AssignableInfo assignableInfo = new Util.AssignableInfo(o, memberInfo);
		object obj;
		if (!(csMember is FieldInfo f))
		{
			if (!(csMember is PropertyInfo p))
			{
				if (!(csMember is MethodInfo m))
				{
					throw new ApplicationException("Member type is unsupported");
				}
				obj = m.Invoke(null, Array.Empty<object>());
			}
			else
			{
				obj = p.GetValue(null);
			}
		}
		else
		{
			obj = f.GetValue(null);
		}
		object retrievedValue = obj;
		InjectAddress(assignableInfo, Util.ConvertObjectToIntPtr(retrievedValue), csAttribute);
	}

	private void InjectAddress(Util.AssignableInfo assignableInfo, nint address, HypostasisMemberInjectionAttribute attribute)
	{
		address += attribute.Offset;
		Type type = assignableInfo.Type;
		if (type == typeof(nint) || type.IsPointer || type.IsFunctionPointer)
		{
			assignableInfo.SetValue(address);
		}
		else if (type.IsAssignableTo(typeof(Delegate)))
		{
			assignableInfo.SetValue(Marshal.GetDelegateForFunctionPointer(address, type));
		}
		else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Hook<>))
		{
			InjectHook(assignableInfo, address, attribute);
		}
		else if (type.IsPrimitive)
		{
			assignableInfo.SetValue(Marshal.PtrToStructure(address, type));
		}
		else
		{
			LogInjectError(assignableInfo.MemberInfo, "Failed to determine how to inject member", attribute.Required);
		}
	}

	private void InjectHook(Util.AssignableInfo assignableInfo, nint address, HypostasisMemberInjectionAttribute attribute)
	{
		Type ownerType = assignableInfo.MemberInfo.ReflectedType;
		object o = assignableInfo.Object;
		Type type = assignableInfo.Type;
		Type hookDelegateType = type.GenericTypeArguments[0];
		if (!IsValidHookAddress(address))
		{
			LogInjectError(assignableInfo.MemberInfo, $"Attempted to place hook on invalid location {address:X}", attribute.Required);
			return;
		}
		Delegate detour = GetMethodDelegate(ownerType, hookDelegateType, o, assignableInfo.Name.Replace("Hook", "Detour"));
		if ((object)detour == null)
		{
			string detourName = attribute.DetourName;
			if (detourName != null)
			{
				detour = GetMethodDelegate(ownerType, hookDelegateType, o, detourName);
				if ((object)detour == null)
				{
					LogInjectError(assignableInfo.MemberInfo, "Detour not found or was incompatible with delegate \"" + detourName + "\" " + hookDelegateType.Name, attribute.Required);
					return;
				}
			}
			else
			{
				Delegate[] matches = GetMethodDelegates(ownerType, hookDelegateType, o);
				if (matches.Length != 1)
				{
					LogInjectError(assignableInfo.MemberInfo, $"Found {matches.Length} matching detours: specify a detour name", attribute.Required);
					return;
				}
				detour = matches[0];
			}
		}
		object hook = type.GetMethod("FromAddress", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, new object[3] { address, detour, false });
		assignableInfo.SetValue(hook);
		if (attribute.EnableHook)
		{
			type.GetMethod("Enable")?.Invoke(hook, null);
		}
		if (attribute.DisposeHook)
		{
			disposableHooks.Add(hook as IDisposable);
		}
	}

	private static Delegate GetMethodDelegate(Type ownerType, Type delegateType, object o, string methodName)
	{
		MethodInfo detourMethod = ownerType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		return CreateDelegate(delegateType, o, detourMethod);
	}

	private static Delegate[] GetMethodDelegates(IReflect ownerType, Type delegateType, object o)
	{
		return (from methodInfo in ownerType.GetAllMethods()
			select CreateDelegate(delegateType, o, methodInfo) into del
			where (object)del != null
			select del).ToArray();
	}

	private static Delegate CreateDelegate(Type delegateType, object o, MethodInfo delegateMethod)
	{
		if (delegateType == null)
		{
			return null;
		}
		if (!delegateMethod.IsStatic)
		{
			return Delegate.CreateDelegate(delegateType, o, delegateMethod, throwOnBindFailure: false);
		}
		return Delegate.CreateDelegate(delegateType, delegateMethod, throwOnBindFailure: false);
	}

	public void AddHook<T>(Hook<T> hook, bool enable = true, bool dispose = true) where T : Delegate
	{
		if (enable)
		{
			hook.Enable();
		}
		if (dispose)
		{
			disposableHooks.Add((IDisposable)hook);
		}
	}

	public void InjectMember(Type type, object o, string member)
	{
		InjectMember(o, type.GetMember(member, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)[0]);
	}

	private static void LogInjectError(MemberInfo memberInfo, string message, bool required)
	{
		string errorMsg = $"Error injecting {memberInfo.ReflectedType?.FullName}.{memberInfo.Name}:\n{message}";
		if (required)
		{
			throw new ApplicationException(errorMsg);
		}
		DalamudApi.LogWarning(errorMsg);
	}

	public unsafe bool IsValidHookAddress(nint address)
	{
		if (address != BaseTextAddress)
		{
			if (address > BaseTextAddress && address < BaseRDataAddress && *(byte*)address != 204)
			{
				return *(byte*)(address - 1) == 204;
			}
			return false;
		}
		return true;
	}

	public void Dispose()
	{
		foreach (IDisposable disposableHook in disposableHooks)
		{
			disposableHook?.Dispose();
		}
		GC.SuppressFinalize(this);
	}
}
