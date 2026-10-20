using System;
using System.Drawing;

namespace DailyGoal;

internal sealed class TaskDragStartedEventArgs : EventArgs
{
	public Point ScreenLocation { get; }

	public Point GrabOffset { get; }

	public TaskDragStartedEventArgs(Point screenLocation, Point grabOffset)
	{
		ScreenLocation = screenLocation;
		GrabOffset = grabOffset;
	}
}
