using System;

namespace DailyGoal;

internal sealed class TaskTextEditEventArgs : EventArgs
{
	public string Text { get; }

	public bool Cancel { get; set; }

	public TaskTextEditEventArgs(string text)
	{
		Text = text;
	}
}
