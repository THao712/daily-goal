using System;

namespace DailyGoal;

internal sealed class TaskReorderEventArgs : EventArgs
{
	public int Direction { get; }

	public TaskReorderEventArgs(int direction)
	{
		Direction = direction;
	}
}
