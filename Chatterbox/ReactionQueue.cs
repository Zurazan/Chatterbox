using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Chatterbox;

public class ReactionQueue : IDisposable
{
	private class QueueContext : IDisposable
	{
		public Trigger Trigger { get; }

		public RestoreContext? RestoreContext { get; }

		public Queue<QueuedEvent> EmoteQueue { get; } = new Queue<QueuedEvent>();

		public Queue<QueuedEvent> TextQueue { get; } = new Queue<QueuedEvent>();

		public CancellationTokenSource Cancellation { get; }

		public QueueContext(Trigger trigger, RestoreContext? restoreContext)
		{
			Trigger = trigger;
			RestoreContext = restoreContext;
			Cancellation = new CancellationTokenSource();
		}

		public void Cancel()
		{
			try
			{
				Cancellation.Cancel();
			}
			catch (ObjectDisposedException)
			{
			}
		}

		public void Dispose()
		{
			Cancellation.Dispose();
		}
	}

	private readonly Plugin plugin;

	private readonly TriggerManager TriggerManager;

	private readonly Stack<QueueContext> queueStack = new Stack<QueueContext>();

	private readonly object queueLock = new object();

	private bool processingEmote;

	private bool processingText;

	private volatile bool disposed;

	public ReactionQueue(Plugin plugin, TriggerManager triggerManager)
	{
		this.plugin = plugin;
		TriggerManager = triggerManager;
	}

	public bool EnqueueEmote(EntityInfo instigator, EntityInfo? receiver, ushort emoteId, Trigger trigger, CounterManager? counterManager = null, bool preview = false)
	{
		if (disposed)
		{
			return false;
		}
		EntityInfo? localPlayer = PlayerManager.LocalPlayer;
		if (localPlayer == null)
		{
			return false;
		}
		ReactionOptions options = trigger.ReactionOptions ?? new ReactionOptions();
		if (counterManager != null && options.CountFailedConditions)
		{
			counterManager.UpdateCounter(trigger, instigator, receiver);
		}
		if (!HandleInterrupt(trigger, out QueueContext ctxCurrent, preview))
		{
			Plugin.Log.Debug($"Trigger '{trigger.Name}' was blocked by its reaction interrupt settings.", Array.Empty<object>());
			return false;
		}
		List<EmoteReaction> emoteReactions = trigger.Reactions?.OfType<EmoteReaction>().ToList() ?? new List<EmoteReaction>();
		List<TextReaction> textReactions = trigger.Reactions?.OfType<TextReaction>().ToList() ?? new List<TextReaction>();
		if (emoteReactions.Count == 0 && textReactions.Count == 0)
		{
			if (counterManager == null || options.CountFailedConditions)
			{
				return false;
			}
			bool canUpdate = true;
			if (options.RestrictRange)
			{
				canUpdate = false;
				if (!instigator.IsLocalPlayer && instigator.IsWithinReactionAngleAndDistanceToLocalPlayer(options))
				{
					canUpdate = true;
				}
				else if (instigator.IsLocalPlayer && receiver != null && receiver.IsWithinReactionAngleAndDistanceToLocalPlayer(options))
				{
					canUpdate = true;
				}
			}
			if (canUpdate && options.RestrictTerritory)
			{
				canUpdate = options.MeetsTerritoryConditions();
			}
			if (canUpdate)
			{
				counterManager.UpdateCounter(trigger, instigator, receiver);
			}
			return false;
		}
		else
		{
			if (!preview && (!localPlayer.CanReactionInterruptCurrentState(options) || (options.RestrictRange && ((!instigator.IsLocalPlayer && !instigator.IsWithinReactionAngleAndDistanceToLocalPlayer(options)) || (instigator.IsLocalPlayer && receiver != null && !receiver.IsWithinReactionAngleAndDistanceToLocalPlayer(options)))) || (options.RestrictTerritory && !options.MeetsTerritoryConditions())))
			{
				Plugin.Log.Debug($"Trigger '{trigger.Name}' matched but was blocked by state, range, or territory restrictions.", Array.Empty<object>());
				return false;
			}
			if (counterManager != null && !options.CountFailedConditions)
			{
				counterManager.UpdateCounter(trigger, instigator, receiver);
			}
			QueueContext ctx = new QueueContext(trigger, (options.RestoreType != RestoreType.None) ? new RestoreContext(plugin, options.RestoreType) : null);
			int emoteDelay = ((emoteReactions.Count > 0) ? emoteReactions[0].Delay : 0);
			foreach (EmoteReaction r in emoteReactions)
			{
				ctx.EmoteQueue.Enqueue(new QueuedEmoteEvent(instigator, receiver, emoteId, trigger, r, emoteDelay, r.Duration, preview));
				emoteDelay = 0;
			}
			foreach (TextReaction r2 in textReactions)
			{
				ctx.TextQueue.Enqueue(new QueuedEmoteEvent(instigator, receiver, emoteId, trigger, r2, Math.Max(0, r2.Delay) + 1, r2.Duration, preview));
			}
			return EnqueueContext(ctxCurrent, ctx);
		}
	}

	public bool EnqueueText(string instigatorName, string instigatorMessage, EntityInfo? instigator, ChatType channel, Trigger trigger, CounterManager? counterManager = null, bool preview = false)
	{
		if (disposed)
		{
			return false;
		}
		EntityInfo? localPlayer = PlayerManager.LocalPlayer;
		if (localPlayer == null)
		{
			return false;
		}
		ReactionOptions options = trigger.ReactionOptions ?? new ReactionOptions();
		if (counterManager != null && options.CountFailedConditions)
		{
			counterManager.UpdateCounter(trigger, instigatorName);
		}
		if (!HandleInterrupt(trigger, out QueueContext ctxCurrent, preview))
		{
			Plugin.Log.Debug($"Trigger '{trigger.Name}' was blocked by its reaction interrupt settings.", Array.Empty<object>());
			return false;
		}
		List<EmoteReaction> emoteReactions = trigger.Reactions?.OfType<EmoteReaction>().ToList() ?? new List<EmoteReaction>();
		List<TextReaction> textReactions = trigger.Reactions?.OfType<TextReaction>().ToList() ?? new List<TextReaction>();
		if (emoteReactions.Count == 0 && textReactions.Count == 0)
		{
			if (counterManager != null && !options.CountFailedConditions)
			{
				bool canUpdate = true;
				if (options.RestrictRange && instigator != null && !instigator.IsLocalPlayer)
				{
					canUpdate = instigator.IsWithinReactionAngleAndDistanceToLocalPlayer(options);
				}
				if (canUpdate && options.RestrictTerritory)
				{
					canUpdate = options.MeetsTerritoryConditions();
				}
				if (canUpdate)
				{
					counterManager.UpdateCounter(trigger, instigatorName);
				}
			}
			return false;
		}
		else
		{
			bool stateAllowed = localPlayer.CanReactionInterruptCurrentState(options);
			bool rangeAllowed = !options.RestrictRange || instigator == null || instigator.IsLocalPlayer || instigator.IsWithinReactionAngleAndDistanceToLocalPlayer(options);
			bool territoryAllowed = !options.RestrictTerritory || options.MeetsTerritoryConditions();
			if (!preview && (!stateAllowed || !rangeAllowed || !territoryAllowed))
			{
				Plugin.Log.Info($"Trigger '{trigger.Name}' matched but was blocked by reaction restrictions. State: {stateAllowed}; range: {rangeAllowed}; territory: {territoryAllowed}; current territory: {Plugin.ClientState.TerritoryType}; ward: {PlayerManager.CurrentWard}; plot: {PlayerManager.CurrentPlot}; room: {PlayerManager.CurrentRoom}.", Array.Empty<object>());
				return false;
			}
			if (counterManager != null && !options.CountFailedConditions)
			{
				counterManager.UpdateCounter(trigger, instigatorName);
			}
			QueueContext ctx = new QueueContext(trigger, (options.RestoreType != RestoreType.None) ? new RestoreContext(plugin, options.RestoreType) : null);
			int emoteDelay = ((emoteReactions.Count > 0) ? emoteReactions[0].Delay : 0);
			foreach (EmoteReaction r in emoteReactions)
			{
				ctx.EmoteQueue.Enqueue(new QueuedTextEvent(instigatorName, instigatorMessage, instigator, channel, trigger, r, emoteDelay, r.Duration, preview));
				emoteDelay = 0;
			}
			foreach (TextReaction r2 in textReactions)
			{
				ctx.TextQueue.Enqueue(new QueuedTextEvent(instigatorName, instigatorMessage, instigator, channel, trigger, r2, Math.Max(0, r2.Delay) + 1, r2.Duration, preview));
			}
			return EnqueueContext(ctxCurrent, ctx);
		}
	}

	private bool EnqueueContext(QueueContext? interrupted, QueueContext context)
	{
		bool startEmote = false;
		bool startText = false;
		lock (queueLock)
		{
			if (disposed)
			{
				context.Dispose();
				return false;
			}
			interrupted?.Cancel();
			queueStack.Push(context);
			if (!processingEmote)
			{
				processingEmote = true;
				startEmote = true;
			}
			if (!processingText)
			{
				processingText = true;
				startText = true;
			}
		}
		if (startEmote)
		{
			_ = ProcessEmoteQueue();
		}
		if (startText)
		{
			_ = ProcessTextQueue();
		}
		return true;
	}

	private bool HandleInterrupt(Trigger trigger, out QueueContext? ctx, bool forceInterrupt = false)
	{
		lock (queueLock)
		{
			ctx = null;
			if (disposed)
			{
				return false;
			}
			if (queueStack.Count == 0)
			{
				return true;
			}
			ctx = queueStack.Peek();
			if (forceInterrupt)
			{
				return true;
			}
			ReactionInterruptType interruptType = trigger.ReactionOptions?.InterruptType ?? ReactionInterruptType.Any;
			if (interruptType == ReactionInterruptType.None)
			{
				return false;
			}
			if (interruptType == ReactionInterruptType.Same && trigger != ctx.Trigger)
			{
				return false;
			}
			if (interruptType == ReactionInterruptType.Other && trigger == ctx.Trigger)
			{
				return false;
			}
			return true;
		}
	}

	private async Task ProcessEmoteQueue()
	{
		bool isCanceled = false;
		try
		{
			while (true)
			{
				QueueContext ctx;
				QueuedEvent item;
				lock (queueLock)
				{
					if (disposed || queueStack.Count == 0 || queueStack.Peek().EmoteQueue.Count == 0)
					{
						break;
					}
					ctx = queueStack.Peek();
					item = ctx.EmoteQueue.Dequeue();
				}
				isCanceled = await ProcessQueueItemAsync(item, ctx);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.Error(ex, "Emote reaction queue exception", Array.Empty<object>());
		}
		finally
		{
			bool restart = false;
			lock (queueLock)
			{
				processingEmote = false;
				if (!disposed && queueStack.Count > 0 && queueStack.Peek().EmoteQueue.Count > 0)
				{
					processingEmote = true;
					restart = true;
				}
			}
			if (restart)
			{
				_ = ProcessEmoteQueue();
			}
			else if (!isCanceled)
			{
				HandleQueueCompletion();
			}
		}
	}

	private async Task ProcessTextQueue()
	{
		bool isCanceled = false;
		try
		{
			while (true)
			{
				QueueContext ctx;
				QueuedEvent item;
				lock (queueLock)
				{
					if (disposed || queueStack.Count == 0 || queueStack.Peek().TextQueue.Count == 0)
					{
						break;
					}
					ctx = queueStack.Peek();
					item = ctx.TextQueue.Dequeue();
				}
				isCanceled = await ProcessQueueItemAsync(item, ctx);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.Error(ex, "Text reaction queue exception", Array.Empty<object>());
		}
		finally
		{
			bool restart = false;
			lock (queueLock)
			{
				processingText = false;
				if (!disposed && queueStack.Count > 0 && queueStack.Peek().TextQueue.Count > 0)
				{
					processingText = true;
					restart = true;
				}
			}
			if (restart)
			{
				_ = ProcessTextQueue();
			}
			else if (!isCanceled)
			{
				HandleQueueCompletion();
			}
		}
	}

	private async Task<bool> ProcessQueueItemAsync(QueuedEvent item, QueueContext ctx)
	{
		try
		{
			if (item.Delay > 0)
			{
				await Task.Delay(item.Delay, ctx.Cancellation.Token);
			}
			if (disposed || ctx.Cancellation.IsCancellationRequested)
			{
				return true;
			}
			await item.ExecuteAsync(TriggerManager);
			if (item.Duration > 0)
			{
				await Task.Delay(item.Duration, ctx.Cancellation.Token);
			}
		}
		catch (OperationCanceledException)
		{
			return true;
		}
		catch (ObjectDisposedException)
		{
			return true;
		}
		return false;
	}

	private void HandleQueueCompletion()
	{
		QueueContext? finished = null;
		lock (queueLock)
		{
			if (disposed || processingEmote || processingText || queueStack.Count == 0)
			{
				return;
			}
			while (queueStack.Count > 1)
			{
				QueueContext interrupted = queueStack.Pop();
				interrupted.Cancel();
				interrupted.Dispose();
			}
			finished = queueStack.Pop();
		}
		try
		{
			finished.RestoreContext?.Restore();
		}
		finally
		{
			finished.Dispose();
		}
	}

	public void Dispose()
	{
		QueueContext[] contexts;
		lock (queueLock)
		{
			if (disposed)
			{
				return;
			}
			disposed = true;
			contexts = queueStack.ToArray();
			queueStack.Clear();
		}
		foreach (QueueContext context in contexts)
		{
			context.Cancel();
			context.Dispose();
		}
	}
}
