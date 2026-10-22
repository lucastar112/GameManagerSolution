
using gameManager.src.Ipc;
using System.Diagnostics;

namespace gameManager.src.Children
{
    public sealed class ChildProcessManager
    {
        private readonly GameManagerIpcServer _ipcServer;
        private readonly Dictionary<int, Process> _processes = new();

        private static readonly TimeSpan RegistrationTimeout =
            TimeSpan.FromSeconds(10);

        public ChildProcessManager(GameManagerIpcServer ipcServer)
        {
            _ipcServer = ipcServer;
        }

        /// <summary>
        /// Starts child processes
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="dolphinProcess"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task StartChildAsync(
            ChildConfiguration configuration,
            Process dolphinProcess,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(configuration.Path))
            {
                HandleStartupFailure(
                    configuration,
                    $"Child executable was not found: {configuration.Path}");
                return;
            }

            Console.WriteLine(
                $"Starting child '{configuration.Name}'...");

            string arguments = configuration.Arguments.Replace(
                "{DolphinPID}",
                dolphinProcess.Id.ToString());

            Console.WriteLine($"Arguments: {arguments}");

            ProcessStartInfo startInfo = new()
            {
                FileName = configuration.Path,
                Arguments = arguments,
                UseShellExecute = true,
                CreateNoWindow = true
            };

            Process? childProcess;

            try
            {
                childProcess = Process.Start(startInfo);
            }
            catch (Exception exception)
            {
                HandleStartupFailure(
                    configuration,
                    $"Failed to start child '{configuration.Name}': " +
                    exception.Message,
                    exception);

                return;
            }

            if (childProcess is null)
            {
                HandleStartupFailure(
                    configuration,
                    $"Process.Start returned null for '{configuration.Name}'.");

                return;
            }

            Console.WriteLine(
                $"Child '{configuration.Name}' started with PID {childProcess.Id}.");

            if (!configuration.UseIpc)
            {
                Console.WriteLine(
                    $"Child '{configuration.Name}' started without IPC.");

                // Keep track of non-IPC children too.
                _processes[-childProcess.Id] = childProcess;
                return;
            }

            try
            {
                int clientId = await _ipcServer.WaitForClientAsync(
                    configuration.Name,
                    childProcess,
                    RegistrationTimeout,
                    cancellationToken);

                _processes[clientId] = childProcess;

                Console.WriteLine(
                    $"Child '{configuration.Name}' registered as IPC client {clientId}.");
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or TimeoutException)
            {
                if (!childProcess.HasExited)
                {
                    try
                    {
                        childProcess.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException)
                    {
                        // The process may have exited between the check and Kill.
                    }
                }

                childProcess.Dispose();

                HandleStartupFailure(
                    configuration,
                    $"Child '{configuration.Name}' failed to register: " +
                    exception.Message,
                    exception);
            }
        }

        private static void HandleStartupFailure(
            ChildConfiguration configuration,
            string message,
            Exception? innerException = null)
        {
            if (configuration.Required)
            {
                throw new InvalidOperationException(
                    $"Required child startup failed. {message}",
                    innerException);
            }

            Console.WriteLine(
                $"Optional child skipped. {message}");
        }
    }
}
