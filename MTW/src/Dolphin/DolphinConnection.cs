using DolphinMemory = Dolphin.Memory.Access;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Buffers.Binary;

namespace MTW.src.Dolphin
{
    public sealed class DolphinConnection
    {
        private readonly Process _process;
        private readonly DolphinMemory.Dolphin? _dolphin;

        private readonly IntPtr _readHandle;
        private readonly IntPtr _writeHandle;

        private const uint PROCESS_VM_READ = 0x0010;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_VM_WRITE = 0x0020;
        private const uint PROCESS_VM_OPERATION = 0x0008;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(
            uint dwDesiredAccess,
            bool bInheritHandle,
            int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            [Out] byte[] lpBuffer,
            int nSize,
            out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesWritten);

        public DolphinConnection(Process process)
        {
            _process = process;

            if (_process.HasExited)
            {
                throw new InvalidOperationException("Dolphin process has already exited.");
            }

            _dolphin = new DolphinMemory.Dolphin(_process);

            _readHandle = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, _process.Id);
            _writeHandle = OpenProcess(PROCESS_VM_WRITE | PROCESS_VM_OPERATION | PROCESS_QUERY_INFORMATION, false, _process.Id);

            if (_readHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException($"Failed to open Dolphin process. Win32 error: {Marshal.GetLastWin32Error()}");
            }
        }

        public bool TryReadByte(IntPtr address, out byte value)
        {
            value = 0;

            if (!_dolphin.TryGetAddress(address, out IntPtr hostAddress))
                return false;

            byte[] buffer = new byte[1];

            if (!ReadProcessMemory(_readHandle, hostAddress, buffer, 1, out int bytesRead))
                return false;

            if (bytesRead != 1)
                return false;

            value = buffer[0];

            return true;
        }

        public bool TryReadPointer(IntPtr address, int offset, out IntPtr pointer)
        {
            pointer = IntPtr.Zero;
            IntPtr targetAddress = new IntPtr(address.ToInt64() + offset);

            if (!_dolphin.TryGetAddress(targetAddress, out IntPtr hostAddress))
            {
                Console.WriteLine($"Failed to translate Dolphin address 0x{targetAddress.ToInt64():X8}.");
                return false;
            }

            byte[] buffer = new byte[4];

            if (!ReadProcessMemory(_readHandle, hostAddress, buffer, buffer.Length, out int bytesRead))
            {
                Console.WriteLine($"ReadProcessMemory failed. Win32 error = {Marshal.GetLastWin32Error()}");
                return false;
            }

            if (bytesRead != 4)
            {
                Console.WriteLine($"Pointer read returned {bytesRead} bytes.");
                return false;
            }

            uint value = BinaryPrimitives.ReadUInt32BigEndian(buffer);

            pointer = new IntPtr(unchecked((long)value));

            Console.WriteLine($"Read pointer 0x{targetAddress.ToInt64():X8} -> 0x{pointer.ToInt64():X8}");

            return pointer != IntPtr.Zero;
        }

        public bool TryWriteByte(IntPtr address, byte value)
        {
            if (!_dolphin.TryGetAddress(address, out IntPtr hostAddress))
                return false;

            byte[] buffer = {value};

            return WriteProcessMemory(_writeHandle, hostAddress, buffer, 1, out int bytesWritten) && bytesWritten == 1;
        }

        public void Dispose()
        {
            if (_readHandle != IntPtr.Zero)
            {
                CloseHandle(_readHandle);
            }
        }
    }
}