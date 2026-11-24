using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace EdgeHub.SystemStats;

/// <summary>
/// Reads CPU, memory, network and disks from Windows, without admin rights or drivers. A reading that fails (an adapter
/// that disappears, a disk that is ejected) never throws: the last known value, or nothing, is returned instead.
/// </summary>
public sealed partial class WindowsSystemReadings : ISystemReadings
{
    // Any public IPv4 address works: Windows only looks up which interface would carry traffic there. Nothing is sent.
    private const uint RouteProbeAddress = 0x08080808;

    private CpuTimes _lastCpu;
    private MemoryReading _lastMemory;

    public CpuTimes ReadCpuTimes()
    {
        if (GetSystemTimes(out long idle, out long kernel, out long user))
        {
            _lastCpu = new CpuTimes(idle, kernel, user);
        }

        return _lastCpu;
    }

    public MemoryReading ReadMemory()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (GlobalMemoryStatusEx(ref status))
        {
            _lastMemory = new MemoryReading((long)status.TotalPhys, (long)status.AvailPhys);
        }

        return _lastMemory;
    }

    public NetworkReading? ReadNetwork()
    {
        try
        {
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            NetworkInterface? chosen = RoutedInterface(interfaces) ?? PickedInterface(interfaces);
            if (chosen is null)
            {
                return null;
            }

            IPv4InterfaceStatistics statistics = chosen.GetIPv4Statistics();
            return new NetworkReading(chosen.Name, statistics.BytesReceived, statistics.BytesSent);
        }
        catch (NetworkInformationException)
        {
            // The adapter went away between listing and reading it (VPN, virtual switch).
            return null;
        }
    }

    public IReadOnlyList<DiskUsage> ReadDisks()
    {
        var disks = new List<DiskUsage>();
        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType == DriveType.Fixed && drive.IsReady)
                {
                    disks.Add(new DiskUsage(drive.Name.TrimEnd('\\'), drive.TotalSize, drive.AvailableFreeSpace));
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // The drive stopped being ready between the checks: skip it this time.
            }
        }

        return disks;
    }

    /// <summary>The interface Windows routes internet traffic through, when it can tell.</summary>
    private static NetworkInterface? RoutedInterface(NetworkInterface[] interfaces)
    {
        if (GetBestInterface(RouteProbeAddress, out uint index) != 0)
        {
            return null;
        }

        return interfaces.FirstOrDefault(nic =>
            nic.OperationalStatus == OperationalStatus.Up
            && nic.Supports(NetworkInterfaceComponent.IPv4)
            && nic.GetIPProperties().GetIPv4Properties()?.Index == index);
    }

    /// <summary>Fallback when the route lookup fails: see <see cref="ActiveInterface.Pick"/>.</summary>
    private static NetworkInterface? PickedInterface(NetworkInterface[] interfaces)
    {
        InterfaceInfo? picked = ActiveInterface.Pick(interfaces.Select(Describe).ToList());
        return picked is null ? null : interfaces.First(nic => nic.Id == picked.Id);
    }

    private static InterfaceInfo Describe(NetworkInterface nic)
    {
        bool isUp = nic.OperationalStatus == OperationalStatus.Up;
        bool isLoopbackOrTunnel = nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel;
        try
        {
            bool hasGateway = isUp && nic.GetIPProperties().GatewayAddresses
                .Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
            long totalBytes = 0;
            if (isUp && nic.Supports(NetworkInterfaceComponent.IPv4))
            {
                IPv4InterfaceStatistics statistics = nic.GetIPv4Statistics();
                totalBytes = statistics.BytesReceived + statistics.BytesSent;
            }

            return new InterfaceInfo(nic.Id, isUp, isLoopbackOrTunnel, hasGateway, totalBytes);
        }
        catch (NetworkInformationException)
        {
            return new InterfaceInfo(nic.Id, IsUp: false, isLoopbackOrTunnel, HasIPv4Gateway: false, TotalBytes: 0);
        }
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemTimes(out long idleTime, out long kernelTime, out long userTime);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [LibraryImport("iphlpapi.dll")]
    private static partial uint GetBestInterface(uint destinationAddress, out uint bestInterfaceIndex);

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
