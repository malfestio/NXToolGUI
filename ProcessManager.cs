using NXToolGUI;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

public static class ProcessManager
{
    private const int PROCESS_VM_READ = 0x0010;
    private const int PROCESS_VM_WRITE = 0x0020;
    private const int PROCESS_VM_OPERATION = 0x0008;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll")]
    public static extern uint GetLastError();

    public static void HookToProcess(string processName, ConsoleControl console)
    {
        Process process = Process.GetProcessesByName(processName).FirstOrDefault();
        if (process == null)
        {
            console.WriteLine($"Process '{processName}' not found.");
            return;
        }

        IntPtr processHandle = OpenProcess(PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION, false, process.Id);

        if (processHandle == IntPtr.Zero)
        {
            console.WriteLine($"Failed to open process. Error code: {Marshal.GetLastWin32Error()}");
            return;
        }

        console.WriteLine($"Successfully hooked to process '{processName}' with ID {process.Id}");

        CloseHandle(processHandle);
    }

    public static void ReadStringFromMemory(string processName, IntPtr baseAddress, int[] offsets, ConsoleControl console)
    {
        Process process = Process.GetProcessesByName(processName).FirstOrDefault();
        if (process == null)
        {
            console.WriteLine($"Process '{processName}' not found.");
            return;
        }

        IntPtr processHandle = OpenProcess(PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION, false, process.Id);

        if (processHandle == IntPtr.Zero)
        {
            console.WriteLine($"Failed to open process. Error code: {Marshal.GetLastWin32Error()}");
            return;
        }

        try
        {
            IntPtr address = baseAddress;
            console.WriteLine($"Starting base address: 0x{address.ToInt32():X8}");

            // Read the pointer chain
            foreach (int offset in offsets)
            {
                byte[] buffer = new byte[4]; // Use 4 bytes for 32-bit process
                int bytesRead;

                if (!ReadProcessMemory(processHandle, address, buffer, buffer.Length, out bytesRead))
                {
                    uint error = GetLastError();
                    console.WriteLine($"Failed to read memory at address 0x{address.ToInt32():X8}. Error code: {error}");
                    return;
                }

                address = (IntPtr)BitConverter.ToInt32(buffer, 0);
                console.WriteLine($"Read address: 0x{address.ToInt32():X8}");
                address += offset;
                console.WriteLine($"Address after applying offset 0x{offset:X}: 0x{address.ToInt32():X8}");
            }

            // Read the final value
            byte[] valueBuffer = new byte[256]; // Adjust size as needed
            int valueBytesRead;
            if (!ReadProcessMemory(processHandle, address, valueBuffer, valueBuffer.Length, out valueBytesRead))
            {
                uint error = GetLastError();
                console.WriteLine($"Failed to read final value at address 0x{address.ToInt32():X8}. Error code: {error}");
            }
            else
            {
                string value = Encoding.UTF8.GetString(valueBuffer, 0, valueBytesRead).TrimEnd('\0');
                console.WriteLine($"Read value: {value}");
            }
        }
        finally
        {
            CloseHandle(processHandle);
        }
    }

    public static string ReadStringFromMemoryAsString(string processName, IntPtr baseAddress, int[] offsets)
    {
        Process process = Process.GetProcessesByName(processName).FirstOrDefault();
        if (process == null)
        {
            return $"Process '{processName}' not found.";
        }

        IntPtr processHandle = OpenProcess(PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION, false, process.Id);

        if (processHandle == IntPtr.Zero)
        {
            return $"Failed to open process. Error code: {Marshal.GetLastWin32Error()}";
        }

        try
        {
            IntPtr address = baseAddress;

            // Read the pointer chain
            foreach (int offset in offsets)
            {
                byte[] buffer = new byte[4]; // Use 4 bytes for 32-bit process
                int bytesRead;

                if (!ReadProcessMemory(processHandle, address, buffer, buffer.Length, out bytesRead))
                {
                    uint error = GetLastError();
                    return $"Failed to read memory at address 0x{address.ToInt32():X}. Error code: {error}";
                }

                address = (IntPtr)BitConverter.ToInt32(buffer, 0);
                address += offset;
            }

            Console.WriteLine($"Final address to read string from: 0x{address.ToInt32():X}");

            // Read the final value
            byte[] valueBuffer = new byte[14]; // Use 14 bytes as specified in the XML
            int valueBytesRead;
            if (!ReadProcessMemory(processHandle, address, valueBuffer, valueBuffer.Length, out valueBytesRead))
            {
                uint error = GetLastError();
                return $"Failed to read final value at address 0x{address.ToInt32():X}. Error code: {error}";
            }
            else
            {
                return Encoding.ASCII.GetString(valueBuffer, 0, valueBytesRead).TrimEnd('\0');
            }
        }
        finally
        {
            CloseHandle(processHandle);
        }
    }

    public static int ReadInt32FromMemory(IntPtr processHandle, IntPtr address)
    {
        byte[] buffer = new byte[4];
        int bytesRead;
        if (ReadProcessMemory(processHandle, address, buffer, buffer.Length, out bytesRead))
        {
            return BitConverter.ToInt32(buffer, 0);
        }
        throw new Exception("Failed to read memory.");
    }

    public static string ReadStringFromMemory(IntPtr processHandle, IntPtr address, int maxLength)
    {
        byte[] buffer = new byte[maxLength];
        int bytesRead;
        if (ReadProcessMemory(processHandle, address, buffer, buffer.Length, out bytesRead))
        {
            int nullTerminator = Array.IndexOf(buffer, (byte)0);
            if (nullTerminator != -1)
            {
                return Encoding.ASCII.GetString(buffer, 0, nullTerminator);
            }
            return Encoding.ASCII.GetString(buffer);
        }
        throw new Exception("Failed to read memory.");
    }
}