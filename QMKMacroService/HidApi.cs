using System.Collections;
using System.Transactions;
using HidSharp;
using HidSharp.Reports;
using HidSharp.Reports.Input;

namespace QMKMacroService;

public class MacroPadApi
{
    private readonly ReportDescriptor _programmableButtonsDescriptor;
    private readonly HidDeviceInputReceiver _programmableButtonsReceiver;
    private BitArray _previousButtonsBitArray;

    private readonly ReportDescriptor _rawHidDescriptor;
    private readonly HidStream _radHidStream;

    private const int VendorId = 12972; // Framework
    private const int ProductId = 19; // Macro-pad
    private static readonly uint[] ProgrammableButtonUsages = [0x000C0001, 0x000C0003];
    private static readonly uint[] RawHidUsages = [0xFF600061];

    public MacroPadApi(ILogger<Worker> _logger)
    {
        HidDevice macroPadDevice;
        HidDevice rawHidDevice;
        var devices = DeviceList.Local.GetHidDevices(VendorId, ProductId);

        var hidDevices = devices.ToList();
        macroPadDevice = GetHidDeviceWithUsageChain(hidDevices, ProgrammableButtonUsages);

        rawHidDevice = GetHidDeviceWithUsageChain(hidDevices, RawHidUsages);

        var macroPadStream = macroPadDevice.Open();

        _programmableButtonsDescriptor = macroPadDevice.GetReportDescriptor();
        _programmableButtonsReceiver = _programmableButtonsDescriptor.CreateHidDeviceInputReceiver();
        _previousButtonsBitArray = new BitArray(8 * (_programmableButtonsDescriptor.MaxInputReportLength - 1));

        _programmableButtonsReceiver.Start(macroPadStream);

        _rawHidDescriptor = rawHidDevice.GetReportDescriptor();
        _radHidStream = rawHidDevice.Open();
    }

    private static HidDevice GetHidDeviceWithUsageChain(List<HidDevice> hidDevices, uint[] usages)
    {
        return hidDevices.First(device =>
        {
            try
            {
                return device.GetReportDescriptor().DeviceItems.Any(deviceItem =>
                    DeviceItemHasUsageChain(deviceItem, usages));
            }
            catch (NotSupportedException e)
            {
                return false;
            }
        });
    }

    private static bool DeviceItemHasUsageChain(DescriptorItem deviceItem, uint[] usages)
    {
        if (usages.Length == 0)
        {
            return false;
        }

        if (!deviceItem.Usages.ContainsValue(usages.First()))
        {
            return false;
        }

        if (usages.Length > 1)
        {
            return deviceItem.ChildItems.Any(childItem => DeviceItemHasUsageChain(childItem, usages.Skip(1).ToArray()));
        }

        return true;
    }

    public IList<byte> GetCommandKeyPresses()
    {
        if (!_programmableButtonsReceiver.IsRunning)
        {
            throw new NullReferenceException("The HidReceiver for the MacroPad has stopped running.");
        }

        var pressedButtons = new List<byte>();

        var buffer = new byte[_programmableButtonsDescriptor.MaxInputReportLength];

        if (!_programmableButtonsReceiver.TryRead(buffer, 0, out var report)) return pressedButtons;

        // skip 1 to ignore the report id in byte 1
        var buttonsBitArray = new BitArray(buffer.Skip(1).ToArray());
        var pushedButtonsBitArray = new BitArray(buttonsBitArray).Xor(_previousButtonsBitArray).And(buttonsBitArray);
        _previousButtonsBitArray = buttonsBitArray;

        if (!pushedButtonsBitArray.HasAnySet()) return pressedButtons;

        for (byte buttonNum = 1; buttonNum <= pushedButtonsBitArray.Count; buttonNum++)
        {
            if (pushedButtonsBitArray[buttonNum - 1])
            {
                pressedButtons.Add(buttonNum);
            }
        }

        return pressedButtons;
    }

    public void WriteToRawHid(IList<byte> bytes)
    {
        while (bytes.Count > 0)
        {
            List<byte> buffer = [69];
            buffer.AddRange(bytes.Take(_rawHidDescriptor.MaxOutputReportLength - 1));
            bytes = bytes.Skip(_rawHidDescriptor.MaxOutputReportLength - 1).ToList();

            _radHidStream.Write(buffer.ToArray());
        }
    }
}