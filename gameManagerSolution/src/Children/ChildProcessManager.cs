using gameManager.src.Ipc;
using System.Diagnostics;

namespace gameManager.src.Children
{
    public sealed class ChildProcessManager
    {
        private readonly GameManagerIpcServer _ipcServer;
        private readonly Dictionary<int, Process> _processes = new();
        private Process process = null;

        public ChildProcessManager(GameManagerIpcServer ipcServer)
        {
            _ipcServer = ipcServer;
        }

        /// <summary>
        /// Starts child process and connect to it via IPC
        /// </summary>
        public async Task StartChildAsync(ChildConfiguration configuration, Process dolphinProcess, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(configuration.Path))
                throw new FileNotFoundException($"Child executable was not found: {configuration.Path}");

            Console.WriteLine($"Starting child '{configuration.Name}'...");
            string arguments = configuration.Arguments.Replace("{DolphinPID}", dolphinProcess.Id.ToString());

            Console.WriteLine($"Arguments: {arguments}");

            ProcessStartInfo startInfo = new()
            {
                FileName = configuration.Path,
                Arguments = arguments,
                UseShellExecute = true,
                CreateNoWindow = true
            };

            try
            {
                process = Process.Start(startInfo);
            }
            catch
            {
                if (configuration.Required)
                    throw new InvalidOperationException($"Failed to start a required child '{configuration.Name}'.");
                else
                {
                    Console.WriteLine($"None required child failed to start '{configuration.Name}', skipping the child.");
                    return;
                }
            }
            

            Console.WriteLine($"Child '{configuration.Name}' started with PID {process.Id}.");

            if (!configuration.UseIpc)
            {
                Console.WriteLine($"Child '{configuration.Name}' started without IPC.");
                return;
            }

            int clientId = await _ipcServer.WaitForClientAsync(cancellationToken);
            _processes[clientId] = process;
            Console.WriteLine($"Child '{configuration.Name}' registered as IPC client {clientId}.");
        }
    }
}