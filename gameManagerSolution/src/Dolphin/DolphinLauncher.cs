using System.Diagnostics;

namespace gameManager.src.Dolphin
{
    public sealed class DolphinLauncher
    {
        public Process Start(string dolphinPath, string gamePath)
        {
            StopExistingDolphinProcesses();

            ProcessStartInfo startInfo = new()
            {
                FileName = dolphinPath,
                Arguments = $"--exec=\"{gamePath}\"",
                UseShellExecute = false
            };

            Process? process = Process.Start(startInfo);

            if (process is null)
            {
                throw new InvalidOperationException(
                    "Failed to start Dolphin.");
            }

            Console.WriteLine(
                $"Dolphin started (PID {process.Id}).");

            return process;
        }

        private static void StopExistingDolphinProcesses()
        {
            Process[] processes =
                Process.GetProcessesByName("dolphin");

            foreach (Process process in processes)
            {
                try
                {
                    Console.WriteLine(
                        $"Stopping existing Dolphin (PID {process.Id})...");

                    process.Kill();
                    process.WaitForExit();
                }
                catch (Exception exception)
                {
                    Console.WriteLine(
                        $"Failed to stop Dolphin (PID {process.Id}): " +
                        exception.Message);
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
    }
}