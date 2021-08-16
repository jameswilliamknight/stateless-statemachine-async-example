namespace StatefulWorker
{
    public enum State
    {
        Startup,
        WaitingForReset,
        WaitingToRun,
        Running,
    }
}