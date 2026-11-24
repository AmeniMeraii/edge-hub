using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace EdgeHub.SystemStats;

/// <summary>Reads CPU, memory, network and disks from Windows, without admin rights or drivers.</summary>
public sealed partial class WindowsSystemReadings : ISystemReadings
{
    public CpuTimes ReadCpuTimes() =>
        GetSystemTimes(out long idle, out long kernel, out long user) ? new CpuTimes(idle, kernel, user) : default;

    public MemoryReading ReadMemory()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref status)
            ? new MemoryReading((long)status.TotalPhys, (long)status.AvailPhys)
            : default;
    }

    public NetworkReading? ReadNetwork()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces();
        InterfaceInfo? active = ActiveInterface.Pick(interfaces.Select(Describe).ToList());
        if (active is null)
        {
            return null;
        }

        NetworkInterface chosen = interfaces.First(i => i.Id == active.Id);
        IPv4InterfaceStatistics statistics = chosen.GetIPv4Statistics();
        return new NetworkReading(chosen.Name, statistics.BytesReceived, statistics.BytesSent);
    }

    public IReadOnlyList<DiskUsage> ReadDisks() =>
        DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
            .Select(d => new DiskUsage(d.Name.TrimEnd('\\'), d.TotalSize, d.AvailableFreeSpace))
            .ToList();

    private static InterfaceInfo Describe(NetworkInterface nic)
    {
        bool isUp = nic.OperationalStatus == OperationalStatus.Up;
        bool hasGateway = isUp && nic.GetIPProperties().GatewayAddresses
            .Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
        long totalBytes = 0;
        if (isUp && nic.Supports(NetworkInterfaceComponent.IPv4))
        {
            IPv4InterfaceStatistics statistics = nic.GetIPv4Statistics();
            totalBytes = statistics.BytesReceived + statistics.BytesSent;
        }

        return new InterfaceInfo(
            nic.Id,
            isUp,
            IsLoopbackOrTunnel: nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel,
            hasGateway,
            totalBytes);
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemTimes(out long idleTime, out long kernelTime, out long userTime);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }
}
