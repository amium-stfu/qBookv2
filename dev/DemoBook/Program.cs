namespace QB;

public static class Program
{
    public static DefinitionAllControls.qPage AllControls { get; } = new();
    public static DefinitionSimulation.qPage Simulation { get; } = new();
    public static DefinitionUdlClient.qPage UdlClient { get; } = new();

    public static void Initialize()
    {
        AllControls.Initialize();
        Simulation.Initialize();
        UdlClient.Initialize();
    }

    public static void Run()
    {
        AllControls.Run();
        Simulation.Run();
        UdlClient.Run();
    }

    public static void Destroy()
    {
        UdlClient.Destroy();
        Simulation.Destroy();
        AllControls.Destroy();
    }
}
