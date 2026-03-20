namespace QB;

public static class Program
{
    public static DefinitionPage1.qPage Page1 { get; } = new();
    public static DefinitionPage2.qPage Page2 { get; } = new();

    public static void Initialize()
    {
        Page1.Initialize();
        Page2.Initialize();
    }

    public static void Run()
    {
        Page1.Run();
        Page2.Run();
    }

    public static void Destroy()
    {
        Page2.Destroy();
        Page1.Destroy();
    }
}
