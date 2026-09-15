using System.ComponentModel;
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
        private const int ProcessVmWrite = 0x0020;
        private const int ProcessVmOperation = 0x0008;

        // Windows process memory APIs
        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(int desiredAccess, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr process, IntPtr baseAddress, byte[] buffer, int size, ref int bytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr process, IntPtr baseAddress, byte[] buffer, int size, ref int bytesWritten);

        /// <summary>
        /// Connects to Dolphin's process
        /// </summary>
        public DolphinConnection(Process process)
        {
            Connect(process);
        }

        /// <summary>
        /// Connects to Dolphin's process
        /// </summary>
        private void Connect(Process process)
        {
            const int maxAttempts = 20;
            const int retryDelayMs = 250;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    _process = process;
                    _dolphin = new DolphinMemory.Dolphin(process);
                    _readHandle = OpenProcess(ProcessVmRead | ProcessVmOperation, false, process.Id);
                    _writeHandle = OpenProcess(ProcessVmWrite | ProcessVmOperation, false, process.Id);

                    if (_readHandle == IntPtr.Zero)
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to open Dolphin for reading.");

                    if (_writeHandle == IntPtr.Zero)
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to open Dolphin for writing.");

                    Console.WriteLine($"Connected to Dolphin (PID {process.Id}).");
                    return;
                }
                catch (Win32Exception exception) when (exception.NativeErrorCode == 299)
                {
                    Console.WriteLine($"Dolphin process is still initializing " + $"(attempt {attempt}/{maxAttempts}).");
                    Thread.Sleep(retryDelayMs);
                }
            }
            throw new InvalidOperationException($"Could not connect to Dolphin after {maxAttempts} attempts.");
        }

        /// <summary>
        /// Check's if the game is running in Dolphin
        /// </summary>
        public bool IsGameRunning()
        {
            if (_process is null || _dolphin is null) 
                return false;

            if (_process.HasExited)
                return false;

            return _dolphin.TryGetAddress(0x80000000, out _);
        }

        /// <summary>
        /// Try to read the game's memory
        /// </summary>
        public bool TryRead(IntPtr address, byte[] buffer, out int bytesRead)
        {
            bytesRead = 0;

            if (_process is null)
            {
                Console.WriteLine("TryRead: process is null.");
                return false;
            }

            if (_process.HasExited)
            {
                Console.WriteLine("TryRead: process has exited.");
                return false;
            }

            if (_dolphin is null)
            {
                Console.WriteLine("TryRead: Dolphin object is null.");
                return false;
            }

            // Usefull for debugging, but can be noisy
            //Console.WriteLine($"TryRead: Dolphin RAM address = 0x{address.ToInt64():X8}");

            if (!_dolphin.TryGetAddress(address, out IntPtr hostAddress))
            {
                Console.WriteLine("TryRead: failed to translate address.");
                return false;
            }
            // Usefull for debugging, but can be noisy
            //Console.WriteLine($"TryRead: host address = 0x{hostAddress.ToInt64():X}");
            //Console.WriteLine($"TryRead: read handle = 0x{_readHandle.ToInt64():X}");

            if (!ReadProcessMemory(_readHandle, hostAddress, buffer, buffer.Length, ref bytesRead))
            {
                Console.WriteLine($"TryRead: ReadProcessMemory failed. " + $"Win32 error = {Marshal.GetLastWin32Error()}");
                return false;
            }
            // Usefull for debugging, but can be noisy
            //Console.WriteLine($"TryRead: successfully read {bytesRead} bytes.");

            return true;
        }

        /// <summary>
        /// Try to read the game's memory
        /// </summary>
        public bool TryReadPointerChain(IntPtr address, byte[] buffer, nint[] offsets, out int bytesRead, int playerId)
        {
            bytesRead = 0;

            if (_process is null)
            {
                Console.WriteLine("TryReadPointerChain: process is null.");
                return false;
            }

            if (_process.HasExited)
            {
                Console.WriteLine("TryReadPointerChain: process has exited.");
                return false;
            }

            if (_dolphin is null)
            {
                Console.WriteLine("TryReadPointerChain: Dolphin object is null.");
                return false;
            }

            // Get first pointer from the game
            if (!_dolphin.TryGetAddress(address, out IntPtr hostAddress))
            {
                Console.WriteLine("TryReadPointerChain: failed to translate address.");
                return false;
            }

            IntPtr currentHost = hostAddress;
            int ptrSize = IntPtr.Size;
            byte[] ptrBuf = new byte[ptrSize];

            // Run through the offset list
            foreach (int offset in offsets)
            {
                int br = 0;
                // Get next Pointer
                if (!ReadProcessMemory(_readHandle, currentHost, ptrBuf, ptrBuf.Length, ref br) || br != ptrBuf.Length)
                {
                    Console.WriteLine($"TryReadPointerChain: failed to read pointer at 0x{currentHost.ToInt64():X}. Win32 error = {Marshal.GetLastWin32Error()}");
                    return false;
                }

                long addrValue = (ptrSize == 8) ? BitConverter.ToInt64(ptrBuf, 0) : BitConverter.ToInt32(ptrBuf, 0);

                // Get next pointer value
                currentHost = new IntPtr(addrValue + offset);
            }

            // Get the targeted value
            if (!ReadProcessMemory(_readHandle, currentHost, buffer, buffer.Length, ref bytesRead))
            {
                Console.WriteLine($"TryReadPointerChain: ReadProcessMemory failed. Win32 error = {Marshal.GetLastWin32Error()}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Try to write in the game's memory
        /// </summary>
        public bool TryWrite(IntPtr address, byte[] data, out int bytesWritten)
        {
            bytesWritten = 0;

            if (_process is null || _process.HasExited)
                return false;

            if (_dolphin is null)
                return false;

            if (!_dolphin.TryGetAddress(address, out IntPtr hostAddress))
                return false;

            return WriteProcessMemory(_writeHandle, hostAddress, data, data.Length, ref bytesWritten);
        }
    }
}


/*
Need to build a dynamaic cache for each race as for example playerSub10 changes for every player, every race. So it needs to be updated once 
every race then used by the gameManager as the reference like config.json. So check first if the resource is available on this cache.json, 
if not, wait until it is, then I need to check if a new race has started to update the cache. Use the same system as to check if resource 
is available, would be easier to implement, also need a system to get player specific pointer chain value. The best would be to create a special
client specificly for this purpose, so that gameManager stays game neutral, this new client named {gameID}.exe, will check if a 
new race has loaded then creates the cache.json for the gameManager, then tells the gameManager that the resource is available, as mkwii is 
for now the only game I know that works this way.
 */