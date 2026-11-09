using System.Globalization;
using System.Text;

namespace D3B.Scripts.Preflight;

internal sealed class PreflightBlockedDetail
{
    public string Category { get; }

    public string RelativePath { get; }

    public ulong ExpectedMountId { get; }

    public ulong ObservedMountId { get; }

    public uint ExpectedDeviceMajor { get; }

    public uint ExpectedDeviceMinor { get; }

    public uint ObservedDeviceMajor { get; }

    public uint ObservedDeviceMinor { get; }

    private PreflightBlockedDetail(
        string category,
        string relativePath,
        ulong expectedMountId,
        ulong observedMountId,
        uint expectedDeviceMajor,
        uint expectedDeviceMinor,
        uint observedDeviceMajor,
        uint observedDeviceMinor)
    {
        Category = category;
        RelativePath = relativePath;
        ExpectedMountId = expectedMountId;
        ObservedMountId = observedMountId;
        ExpectedDeviceMajor = expectedDeviceMajor;
        ExpectedDeviceMinor = expectedDeviceMinor;
        ObservedDeviceMajor = observedDeviceMajor;
        ObservedDeviceMinor = observedDeviceMinor;
    }

    public static PreflightBlockedDetail MountBoundary(
        ReadOnlySpan<byte> relativePath,
        PreflightFileStatus expected,
        PreflightFileStatus observed)
    {
        return new PreflightBlockedDetail(
            "mount_boundary",
            EscapePath(relativePath),
            expected.MountId,
            observed.MountId,
            expected.DeviceMajor,
            expected.DeviceMinor,
            observed.DeviceMajor,
            observed.DeviceMinor);
    }

    public string FormatSafeLine()
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "DETAIL: category={0} relative_path=\"{1}\" expected_mount_id={2} observed_mount_id={3} expected_device_major={4} expected_device_minor={5} observed_device_major={6} observed_device_minor={7}",
            Category,
            RelativePath,
            ExpectedMountId,
            ObservedMountId,
            ExpectedDeviceMajor,
            ExpectedDeviceMinor,
            ObservedDeviceMajor,
            ObservedDeviceMinor);
    }

    private static string EscapePath(ReadOnlySpan<byte> relativePath)
    {
        var escaped = new StringBuilder(relativePath.Length);

        foreach (byte value in relativePath)
        {
            if (value is >= 0x20 and <= 0x7e && value != (byte)'\\' && value != (byte)'"')
            {
                escaped.Append((char)value);
                continue;
            }

            escaped.Append("\\x");
            escaped.Append(value.ToString("X2", CultureInfo.InvariantCulture));
        }

        return escaped.ToString();
    }
}
