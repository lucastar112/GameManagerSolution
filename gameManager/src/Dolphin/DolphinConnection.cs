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

        public DolphinConnection(Process process)
        {
            Connect(process);
        }

        private void Connect(Process process)
        {
            if (process is null)
            {
                throw new InvalidOperationException("Dolphin is not running.");
            }
            
            // Connect to the Dolphin process by name
            _process = process;

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

            if (_process is null || _process.HasExited)
                return false;

            if (_dolphin is null)
                return false;

            // Convert the emulated game RAM address
            // into the corresponding address inside Dolphin.
            if (!_dolphin.TryGetAddress(address, out IntPtr hostAddress))
            {
                Console.WriteLine(
                    $"Failed to translate Dolphin RAM address 0x{address.ToInt64():X8}.");

                return false;
            }

            return ReadProcessMemory(
                _readHandle,
                hostAddress,
                buffer,
                buffer.Length,
                ref bytesRead);
        }


        public bool TryWrite(IntPtr address, byte[] data, out int bytesWritten)
        {
            bytesWritten = 0;

            if (_process is null || _process.HasExited)
                return false;

            if (_dolphin is null)
                return false;

            if (!_dolphin.TryGetAddress(address, out IntPtr hostAddress))
                return false;

            return WriteProcessMemory(
                _writeHandle,
                hostAddress,
                data,
                data.Length,
                ref bytesWritten);
        }

    }
}
