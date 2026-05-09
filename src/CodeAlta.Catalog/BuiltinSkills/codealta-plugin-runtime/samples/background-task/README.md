# Background task sample

Starts a tracked background loop that is cancelled during plugin deactivation. The `/background-task-status` command returns a visible running-task count. Long-running plugins must use the runtime task service rather than untracked `Task.Run` work.
