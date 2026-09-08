using System.Diagnostics;
using System.Runtime.InteropServices;
using DolphinMemory = Dolphin.Memory.Access;

namespace gameManager.src.Dolphin
{
    public sealed class DolphinConnection
    {
        private Process? _process;
        private DolphinMemory.Dolphin? _dolphin;

        private IntPtr _readHandle;
        private IntPtr _writeHandle;

        private const int ProcessVmRead = 0x0010;
        private const int ProcessVmWrite = 0x1F0FFF;

        // Prepare process dll
        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(
            int desiredAccess,
            bool inheritHandle,
            int processId);

        [DllImport("kernel32.dll")]
        private static extern bool ReadProcessMemory(
            IntPtr process,
            IntPtr baseAddress,
            byte[] buffer,
            int size,
            ref int bytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(
            IntPtr process,
            IntPtr baseAddress,
            byte[] buffer,
            int size,
            ref int bytesWritten);

        public DolphinConnection()
        {
            Connect();
        }

        private void Connect()
        {
            // Connect to the Dolphin process by name (needs to be changed)
            Process[] processes = Process.GetProcessesByName("dolphin");

            if (processes.Length == 0)
            {
                throw new InvalidOperationException("Dolphin is not running.");
            }

            _process = processes[0];

            Console.WriteLine($"Dolphin process found: {_process.ProcessName} " + $"(PID {_process.Id})");

            _dolphin =new DolphinMemory.Dolphin(_process);

            _readHandle = OpenProcess(ProcessVmRead, false, _process.Id);

            _writeHandle = OpenProcess(ProcessVmWrite, false, _process.Id);

            if (_readHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to obtain Dolphin read access.");
            }

            if (_writeHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to obtain Dolphin write access.");
            }
        }

        public bool IsGameRunning()
        {
            if (_process is null || _dolphin is null)
            {
                return false;
            }

            if (_process.HasExited)
            {
                return false;
            }

            return _dolphin.TryGetAddress(0x80000000, out _);
        }

        public bool TryRead(IntPtr address, byte[] buffer, out int bytesRead)
        {
            bytesRead = 0;

            return ReadProcessMemory(
                _readHandle,
                address,
                buffer,
                buffer.Length,
                ref bytesRead);
        }

        public bool TryWrite(IntPtr address, byte[] data, out int bytesWritten)
        {
            bytesWritten = 0;

            return WriteProcessMemory(
                _writeHandle,
                address,
                data,
                data.Length,
                ref bytesWritten);
        }
    }
}
