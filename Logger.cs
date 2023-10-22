using RoboMaster;

public class Logger : ILogger
{
    public void LogError(string message)
    {
        Console.WriteLine(message);
    }

    public void LogInfo(string message)
    {
        Console.WriteLine(message);
    }

    public void LogWarning(string message)
    {
        Console.WriteLine(message);
    }
}