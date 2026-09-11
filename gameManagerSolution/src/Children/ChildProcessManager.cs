using gameManager.src.Ipc;
using System.Diagnostics;

namespace gameManager.src.Children
{
    public sealed class ChildProcessManager
    {
        private readonly GameManagerIpcServer _ipcServer;
        private readonly Dictionary<int, Process> _processes = new();

        public ChildProcessManager(GameManagerIpcServer ipcServer)
        {
            _ipcServer = ipcServer;
        }

        /// <summary>
        /// Starts child process and connect to it via IPC
        /// </summary>
        public async Task StartChildAsync(ChildConfiguration configuration, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(configuration.Path)) 
                throw new FileNotFoundException($"Child executable was not found: " + $"{configuration.Path}");
            
            Console.WriteLine($"Starting child '{configuration.Name}'...");

            ProcessStartInfo startInfo = new() {FileName = configuration.Path, Arguments = configuration.Arguments, UseShellExecute = false, CreateNoWindow = false};
            Process process = Process.Start(startInfo) 
                ?? throw new InvalidOperationException($"Failed to start child '{configuration.Name}'.");

            Console.WriteLine($"Child '{configuration.Name}' started " + $"with PID {process.Id}.");

            int clientId = await _ipcServer.WaitForClientAsync(cancellationToken);
            _processes[clientId] = process;
            Console.WriteLine($"Child '{configuration.Name}' registered " + $"as IPC client {clientId}.");
        }

        public bool TryGetProcess(int clientId, out Process? process)
        {
            return _processes.TryGetValue(clientId, out process);
        }
    }
}