using System.Threading.Tasks;

namespace Chatterbox;

public class QueuedEmoteEvent : QueuedEvent
{
	public EntityInfo Instigator { get; }

	public EntityInfo? Receiver { get; }

	public ushort EmoteId { get; }

	public QueuedEmoteEvent(EntityInfo instigator, EntityInfo? receiver, ushort emoteId, Trigger trigger, ReactionBase reaction, int delay, int duration, bool isPreview)
		: base(trigger, reaction, delay, duration, isPreview)
	{
		Instigator = instigator;
		Receiver = receiver;
		EmoteId = emoteId;
	}

	public override Task ExecuteAsync(TriggerManager manager)
	{
		if (base.Reaction is EmoteReaction er)
		{
			return manager.PerformEmoteReaction(this, er);
		}
		if (base.Reaction is TextReaction tr)
		{
			return manager.PerformTextReaction(this, tr);
		}
		return Task.CompletedTask;
	}
}
