namespace QB;

public static class Program
{
    public static DefinitionMain.qPage Main { get; } = new();
    public static DefinitionPage1.qPage Page1 { get; } = new();

    public static void Initialize()
    {
        Main.Initialize();
        Page1.Initialize();
    }

    public static void Run()
    {
        Main.Run();
        Page1.Run();
    }

    public static void Destroy()
    {
        Page1.Destroy();
        Main.Destroy();
    }
}
