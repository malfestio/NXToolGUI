using System;
using System.Windows.Forms;
using System.Diagnostics;
using System.Linq;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace NXToolGUI
{
    public partial class NXToolGUI : Form
    {
        private ConsoleControl console;
        private Button button1;
        private Button button2;
        private Button button10;
        private bool isHooked = false;
        private InventoryScanner scanner;
        private bool isScanning = false;
        private string configFilePath = "config.ini";
        private Dictionary<string, Dictionary<string, string>> config;

        public NXToolGUI()
        {
            InitializeComponent();
            console = new ConsoleControl();
            console.Dock = DockStyle.Left;
            console.Width = 300;
            this.Controls.Add(console);
            config = ReadConfigFile();
            scanner = new InventoryScanner(console, FlattenConfig(config));
            InitializeButtons();
        }

        private void InitializeButtons()
        {
            for (int i = 1; i <= 10; i++)
            {
                Button button = new Button
                {
                    Text = $"Button {i}",
                    Width = 100,
                    Height = 30,
                    Top = (i - 1) * 35,
                    Left = console.Right + 10,
                    Tag = i
                };
                button.Click += ButtonClick;
                this.Controls.Add(button);

                if (i == 1)
                {
                    button1 = button;
                    UpdateButtonColor();
                }
                
                else if (i == 2)
                {
                    button2 = button;
                    button2.Text = "Start Scanner";
                }
                else if (i == 10)
                {
                    button10 = button;
                    button10.Text = "Button 10";
                }
            }
        }

        private void ButtonClick(object sender, EventArgs e)
        {
            Button clickedButton = sender as Button;
            int buttonNumber = (int)clickedButton.Tag;

            console.WriteLine($"Button {buttonNumber} clicked");

            switch (buttonNumber)
            {
                case 1:
                    ToggleHookStatus();
                    break;
                case 2:
                    ToggleScanner();
                    break;
                case 10:
                    break;
                default:
                    console.WriteLine($"Unhandled button {buttonNumber}");
                    break;
            }
        }

        private void ToggleHookStatus()
        {
            if (!isHooked)
            {
                try
                {
                    ProcessManager.HookToProcess("TwelveSky2", console);
                    isHooked = true;
                    console.WriteLine("Successfully hooked to TwelveSky2 process.");
                }
                catch (Exception ex)
                {
                    console.WriteLine($"Failed to hook: {ex.Message}");
                    isHooked = false;
                }
            }
            else
            {
                isHooked = false;
                console.WriteLine("Unhooked from TwelveSky2 process.");
            }

            UpdateButtonColor();
            UpdateButton1Text();
        }

        private void UpdateButtonColor()
        {
            button1.BackColor = isHooked ? Color.Green : Color.Red;
            button1.ForeColor = Color.White;
        }

        private void UpdateButton1Text()

        {

            if (!isHooked)

            {

                button1.Text = "Hook";

                return;

            }


            Process process = Process.GetProcessesByName("TwelveSky2").FirstOrDefault();

            if (process == null)

            {

                console.WriteLine("TwelveSky2 process not found.");

                return;

            }

            IntPtr baseAddress = process.MainModule.BaseAddress;

            console.WriteLine($"TwelveSky2.exe base address: 0x{baseAddress.ToInt64():X}");


            // Read the base address offset from the config file

            if (!config["Addresses"].TryGetValue("BaseAddress", out string baseAddressOffsetStr))

            {

                console.WriteLine("BaseAddress not found in config file.");

                return;

            }


            // Convert the base address offset from hex string to long

            if (!long.TryParse(baseAddressOffsetStr, System.Globalization.NumberStyles.HexNumber, null, out long baseAddressOffset))

            {

                console.WriteLine($"Invalid BaseAddress in config: {baseAddressOffsetStr}");

                return;

            }


            // Calculate the initial pointer address

            IntPtr initialPointerAddress = IntPtr.Add(baseAddress, (int)baseAddressOffset);

            console.WriteLine($"Initial pointer address: 0x{initialPointerAddress.ToInt64():X}");


            // Read the pointer value at the initial address

            int pointerValue = ProcessManager.ReadInt32FromMemory(process.Handle, initialPointerAddress);

            console.WriteLine($"Pointer value: 0x{pointerValue:X}");


            // Calculate the final address by adding 0x14 (20 in decimal) to the pointer value

            IntPtr finalAddress = new IntPtr(pointerValue + 0x14);

            console.WriteLine($"Final address: 0x{finalAddress.ToInt64():X}");


            // Read the string from the final address

            string result = ProcessManager.ReadStringFromMemory(process.Handle, finalAddress, 100); // Assuming max length of 100 characters

            button1.Text = result;

            console.WriteLine($"Updated Button 1 text: {result}");

        }

        private void ToggleScanner()
        {
            if (!isHooked)
            {
                console.WriteLine("Please hook to the process first.");
                return;
            }

            if (!isScanning)
            {
                isScanning = true;
                button2.Text = "Stop Scanner";
                button2.BackColor = Color.Green;
                ScanInventory();
            }
            else
            {
                isScanning = false;
                button2.Text = "Start Scanner";
                button2.BackColor = SystemColors.Control;
            }
        }

        private void ScanInventory()
        {
            Process process = Process.GetProcessesByName("TwelveSky2").FirstOrDefault();
            if (process == null)
            {
                console.WriteLine("TwelveSky2 process not found.");
                return;
            }

            IntPtr baseAddress = process.MainModule.BaseAddress;
            console.WriteLine($"TwelveSky2.exe base address: 0x{baseAddress.ToInt64():X}");

            // Calculate the correct inventory address
            long baseAddressOffset = Convert.ToInt64(config["Addresses"]["BaseAddress"], 16);
            IntPtr inventoryPointer = IntPtr.Add(baseAddress, (int)baseAddressOffset);
            console.WriteLine($"Inventory pointer address: 0x{inventoryPointer.ToInt64():X}");

            int[] offsets = { Convert.ToInt32(config["Offsets"]["InventoryOffset"], 16) };
            int[] targetItemIDs = ReadTargetItemIDsFromFile(config["Files"]["TargetItemIDsFile"]);

            if (targetItemIDs.Length == 0)
            {
                console.WriteLine("No target item IDs found. Please check the file.");
                return;
            }

            scanner.ScanAndModifyInventory(process, inventoryPointer, offsets, targetItemIDs);
        }

        private int[] ReadTargetItemIDsFromFile(string filePath)
        {
            List<int> itemIDs = new List<int>();

            try
            {
                if (!File.Exists(filePath))
                {
                    console.WriteLine($"File not found: {filePath}");
                    return new int[0];
                }

                string[] lines = File.ReadAllLines(filePath);
                foreach (string line in lines)
                {
                    if (int.TryParse(line.Trim(), out int itemID))
                    {
                        itemIDs.Add(itemID);
                    }
                    else
                    {
                        console.WriteLine($"Invalid item ID in file: {line}");
                    }
                }

                console.WriteLine($"Read {itemIDs.Count} target item IDs from file.");
            }
            catch (Exception ex)
            {
                console.WriteLine($"Error reading target item IDs file: {ex.Message}");
            }

            return itemIDs.ToArray();
        }

        private Dictionary<string, Dictionary<string, string>> ReadConfigFile()
        {
            var configDict = new Dictionary<string, Dictionary<string, string>>();
            string currentSection = "";

            try
            {
                if (!File.Exists(configFilePath))
                {
                    console.WriteLine($"Config file not found: {configFilePath}");
                    return configDict;
                }

                string[] lines = File.ReadAllLines(configFilePath);
                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmedLine)) continue;

                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
                        configDict[currentSection] = new Dictionary<string, string>();
                    }
                    else
                    {
                        int equalIndex = trimmedLine.IndexOf('=');
                        if (equalIndex > 0)
                        {
                            string key = trimmedLine.Substring(0, equalIndex).Trim();
                            string value = trimmedLine.Substring(equalIndex + 1).Trim();
                            configDict[currentSection][key] = value;
                        }
                    }
                }

                console.WriteLine($"Read {configDict.Count} configuration sections.");
            }
            catch (Exception ex)
            {
                console.WriteLine($"Error reading config file: {ex.Message}");
            }

            return configDict;
        }

        private Dictionary<string, string> FlattenConfig(Dictionary<string, Dictionary<string, string>> nestedConfig)
        {
            var flatConfig = new Dictionary<string, string>();
            foreach (var section in nestedConfig)
            {
                foreach (var item in section.Value)
                {
                    flatConfig[$"{section.Key}.{item.Key}"] = item.Value;
                }
            }
            return flatConfig;
        }
    }
}