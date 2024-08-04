using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace NXToolGUI
{
    public class InventoryScanner
    {
        private const int PROCESS_VM_READ = 0x0010;
        private const int PROCESS_VM_WRITE = 0x0020;
        private const int PROCESS_VM_OPERATION = 0x0008;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer,
            int dwSize, out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize,
            out int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private ConsoleControl console;
        private Dictionary<string, string> config;


        public InventoryScanner(ConsoleControl console, Dictionary<string, string> config)
        {
            this.console = console;
            this.config = config;
        }

        public void ScanAndModifyInventory(Process process, IntPtr basePointer, int[] offsets, int[] targetItemIDs)
        {
            IntPtr processHandle = OpenProcess(PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION, false, process.Id);
            if (processHandle == IntPtr.Zero)
            {
                console.WriteLine($"Failed to open process. Error code: {Marshal.GetLastWin32Error()}");
                return;
            }

            try
            {
                IntPtr inventoryAddress = ReadPointerChain(processHandle, basePointer, offsets);
                if (inventoryAddress == IntPtr.Zero)
                {
                    console.WriteLine("Failed to read inventory address.");
                    return;
                }

                console.WriteLine($"Final inventory address: 0x{inventoryAddress.ToInt64():X}");

                int structSize = Marshal.SizeOf<INV_SLOT>();
                int totalScanSize = int.Parse(config["Scan.TotalScanSize"]);
                int slotsToScan = totalScanSize / structSize;

                console.WriteLine($"Structure size: {structSize} bytes");
                console.WriteLine($"Total scan size: {totalScanSize} bytes");
                console.WriteLine($"Slots to scan: {slotsToScan}");

                byte[] buffer = new byte[totalScanSize];
                int bytesRead;

                if (!ReadProcessMemory(processHandle, inventoryAddress, buffer, totalScanSize, out bytesRead))
                {
                    console.WriteLine($"Failed to read memory at address 0x{inventoryAddress.ToInt64():X}. Error code: {Marshal.GetLastWin32Error()}");
                    return;
                }

                console.WriteLine($"Successfully read {bytesRead} bytes from memory");

                HashSet<int> targetItemIDSet = new HashSet<int>(targetItemIDs);
                int modifiedCount = 0;

                for (int i = 0; i < slotsToScan; i++)
                {
                    int offset = i * structSize;
                    if (offset + structSize > totalScanSize) break;

                    byte[] slotBuffer = new byte[structSize];
                    Array.Copy(buffer, offset, slotBuffer, 0, structSize);

                    INV_SLOT slot = ByteArrayToStructure<INV_SLOT>(slotBuffer);

                    IntPtr slotAddress = IntPtr.Add(inventoryAddress, offset);

                    if (targetItemIDSet.Contains(slot.ItemID))
                    {
                        console.WriteLine($"Found target ItemID {slot.ItemID} at slot {i}");
                        console.WriteLine($"Current ItemID: {slot.ItemID}, PosX: {slot.PosX}, PosY: {slot.PosY}");

                        slot.ItemID = 81408;
                        slot.PosX = 0;
                        slot.PosY = 7;

                        byte[] modifiedBuffer = StructureToByteArray(slot);
                        int bytesWritten;
                        if (!WriteProcessMemory(processHandle, slotAddress, modifiedBuffer, structSize, out bytesWritten))
                        {
                            console.WriteLine($"Failed to write memory at address 0x{slotAddress.ToInt64():X}. Error code: {Marshal.GetLastWin32Error()}");
                        }
                        else
                        {
                            console.WriteLine($"Modified slot {i}: ItemID set to 81408, PosX set to 0, PosY set to 6");
                            modifiedCount++;
                        }
                    }
                }

                console.WriteLine($"Scan complete. Modified {modifiedCount} slots.");
            }
            finally
            {
                CloseHandle(processHandle);
            }
        }

        private IntPtr ReadPointerChain(IntPtr processHandle, IntPtr baseAddress, int[] offsets)
        {
            IntPtr address = baseAddress;
            byte[] buffer = new byte[IntPtr.Size];
            int bytesRead;

            foreach (int offset in offsets)
            {
                if (!ReadProcessMemory(processHandle, address, buffer, buffer.Length, out bytesRead))
                {
                    console.WriteLine($"Failed to read memory at address 0x{address.ToInt64():X}. Error code: {Marshal.GetLastWin32Error()}");
                    return IntPtr.Zero;
                }

                address = IntPtr.Size == 4 ? (IntPtr)BitConverter.ToInt32(buffer, 0) : (IntPtr)BitConverter.ToInt64(buffer, 0);
                address = IntPtr.Add(address, offset);
            }

            return address;
        }

        private static T ByteArrayToStructure<T>(byte[] bytes) where T : struct
        {
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                return (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            }
            finally
            {
                handle.Free();
            }
        }

        private static byte[] StructureToByteArray(object obj)
        {
            int size = Marshal.SizeOf(obj);
            byte[] arr = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);

            Marshal.StructureToPtr(obj, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);

            return arr;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INV_SLOT
    {
        public int ItemID;
        public int PosX;
        public int PosY;
        public int Amount;
        public int Value;
        public int UniqueNumber;
    }
}