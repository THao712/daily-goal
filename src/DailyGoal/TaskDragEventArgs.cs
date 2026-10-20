using System;
using System.Drawing;

namespace DailyGoal;

internal sealed class TaskDragEventArgs : EventArgs
{
	public Point ScreenLocation { get; }

	public bool Canceled { get; }

	public TaskDragEventArgs(Point screenLocation, bool canceled = false)
	{
		ScreenLocation = screenLocation;
		Canceled = canceled;
	}
}
