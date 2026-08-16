using System.Threading.Tasks;

namespace Chatterbox;

public abstract class QueuedEvent
{
	public Trigger Trigger { get; }

	public ReactionBase Reaction { get; }

	public int Delay { get; }

	public int Duration { get; }

	public bool IsPreview { get; }

	protected QueuedEvent(Trigger trigger, ReactionBase reaction, int delay, int duration, bool isPreview)
	{
		Trigger = trigger;
		Reaction = reaction;
		Delay = delay;
		Duration = duration;
		IsPreview = isPreview;
	}

	public abstract Task ExecuteAsync(TriggerManager manager);
}
