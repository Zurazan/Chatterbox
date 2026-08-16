using System.Threading.Tasks;

namespace Chatterbox;

public class QueuedTextEvent : QueuedEvent
{
	public string InstigatorName { get; }

	public string InstigatorMessage { get; }

	public EntityInfo? Instigator { get; }

	public ChatType Channel { get; }

	public QueuedTextEvent(string instigatorName, string instigatorMessage, EntityInfo? instigator, ChatType channel, Trigger trigger, ReactionBase reaction, int delay, int duration, bool isPreview)
		: base(trigger, reaction, delay, duration, isPreview)
	{
		InstigatorName = instigatorName;
		InstigatorMessage = instigatorMessage;
		Instigator = instigator;
		Channel = channel;
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
