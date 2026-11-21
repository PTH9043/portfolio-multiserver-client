using ChatClient.Diagnostics;

namespace ChatClient;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        ClientRuntimeTrace.Start();
        ClientRuntimeTrace.Log("INFO", "ChatClient started");
        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
        finally
        {
            ClientRuntimeTrace.Log("INFO", "ChatClient exiting");
            ClientRuntimeTrace.Stop("Program.cs");
        }
    }
}