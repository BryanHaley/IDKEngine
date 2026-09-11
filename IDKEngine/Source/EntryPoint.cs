using System;
using System.Diagnostics;
using BBLogger;
using IDKEngine;

namespace Program;

class EntryPoint
{
    private static void Main(string[] args)
    {
        Benchmark benchmark = new Benchmark(args);

        using Application application = new Application(benchmark.Width, benchmark.Height, "IDKEngine", benchmark);

        // If application is run inside a debugger then we don't catch Exceptions here
        // as this worsens the debugging experience.
        // Otherwise, if the app is run standalone, then we catch and log the Exception.
        if (Debugger.IsAttached)
        {
            application.Run();
        }
        else
        {
            try
            {
                application.Run();
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.LogLevel.Fatal, ex.Message);
                Logger.Log(Logger.LogLevel.Fatal, ex.StackTrace);
            }
        }
    }
}
