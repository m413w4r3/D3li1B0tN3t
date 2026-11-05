using D3B.Diagnostic.Contract;
using D3B.Diagnostic.Replay;

namespace D3B.Scripts.Diagnose;

internal static class DiagnoseIO
{
    public static int Run(Action action)
    {
        try { action(); return 0; }
        catch (DiagnoseFailure failure) { Console.Error.WriteLine($"{failure.Category}; details are redacted."); return 1; }
        catch { Console.Error.WriteLine("Diagnostic command failed; details are redacted."); return 1; }
    }

    public static async Task<int> RunAsync(Func<Task> action)
    {
        try { await action(); return 0; }
        catch (DiagnoseFailure failure) { Console.Error.WriteLine($"{failure.Category}; details are redacted."); return 1; }
        catch { Console.Error.WriteLine("Diagnostic command failed; details are redacted."); return 1; }
    }

    public static T Stage<T>(string category, Func<T> operation)
    {
        try { return operation(); }
        catch (DiagnoseFailure) { throw; }
        catch { throw new DiagnoseFailure(category); }
    }

    public static async Task<T> StageAsync<T>(string category, Func<Task<T>> operation)
    {
        try { return await operation(); }
        catch (DiagnoseFailure) { throw; }
        catch (DiagnosticEngineException exception)
        {
            throw new DiagnoseFailure($"{category} (exit {exception.ExitCode}; {exception.StderrTail})");
        }
        catch (DiagnosticProtocolException exception)
        {
            throw new DiagnoseFailure($"{category} ({exception.Message})");
        }
        catch { throw new DiagnoseFailure(category); }
    }

    public static void Write(FileInfo? output, string json)
    {
        try
        {
            if (output is null) Console.WriteLine(json);
            else File.WriteAllText(output.FullName, json);
        }
        catch { throw new DiagnoseFailure("output write failed"); }
    }

    internal sealed class DiagnoseFailure : Exception
    {
        public string Category { get; }

        public DiagnoseFailure(string category)
        {
            Category = category;
        }
    }
}
