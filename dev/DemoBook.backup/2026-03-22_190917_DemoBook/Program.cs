namespace QB;

public static class Program
{
    public static DefinitionAllControls.qPage AllControls { get; } = new();
    public static DefinitionSimulation.qPage Simulation { get; } = new();

    public static void Initialize()
    {
        AllControls.Initialize();
        Simulation.Initialize();
    }

    public static void Run()
    {
        AllControls.Run();
        Simulation.Run();
    }

    public static void Destroy()
    {
        Simulation.Destroy();
        AllControls.Destroy();
    }
}
