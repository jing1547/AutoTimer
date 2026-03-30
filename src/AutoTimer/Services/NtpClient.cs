using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace AutoTimer.Services;

/// <summary>
/// NTP (UDP 123) 로 밀리초 정밀 시간을 가져온다.
/// RFC 2030 기반. 송수신 RTT 보정 포함.
/// </summary>
public static class NtpClient
{
    private static readonly string[] Servers =
    [
        "time.bora.net",
        "time.google.com",
        "pool.ntp.org"
    ];

    private const int NtpPort = 123;
    private const int NtpPacketSize = 48;
    private const int TimeoutMs = 3000;

    // NTP 에포크: 1900-01-01
    private static readonly DateTime NtpEpoch = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// NTP 서버에서 현재 UTC 시간을 가져온다.
    /// 3개 서버를 순차 시도 (폴백).
    /// </summary>
    public static async Task<NtpResult> GetTimeAsync()
    {
        foreach (var server in Servers)
        {
            try
            {
                var result = await QueryServerAsync(server).ConfigureAwait(false);
                if (result.Success)
                    return result;
            }
            catch { }
        }

        return new NtpResult { Success = false, Error = "All NTP servers failed" };
    }

    private static async Task<NtpResult> QueryServerAsync(string server)
    {
        var ntpData = new byte[NtpPacketSize];
        // LI=0, VN=4, Mode=3 (Client)
        ntpData[0] = 0x23; // 00 100 011

        var addresses = await Dns.GetHostAddressesAsync(server).ConfigureAwait(false);
        if (addresses.Length == 0)
            return new NtpResult { Success = false, Error = $"DNS failed: {server}" };

        var endpoint = new IPEndPoint(addresses[0], NtpPort);

        using var socket = new Socket(endpoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        socket.ReceiveTimeout = TimeoutMs;
        socket.SendTimeout = TimeoutMs;

        // T1: 클라이언트 송신 시각
        var t1 = DateTime.UtcNow;

        await socket.ConnectAsync(endpoint).ConfigureAwait(false);
        await socket.SendAsync(ntpData, SocketFlags.None).ConfigureAwait(false);

        var received = new byte[NtpPacketSize];
        var len = await socket.ReceiveAsync(received, SocketFlags.None).ConfigureAwait(false);

        // T4: 클라이언트 수신 시각
        var t4 = DateTime.UtcNow;

        if (len < NtpPacketSize)
            return new NtpResult { Success = false, Error = "Invalid NTP response" };

        // Bounds check: need 8 bytes from offset 32 and 40
        if (received.Length < 48)
            return new NtpResult { Success = false, Error = "NTP buffer too small" };

        // T2: 서버 수신 시각 (바이트 32-39)
        var t2 = ReadNtpTimestamp(received, 32);
        // T3: 서버 송신 시각 (바이트 40-47)
        var t3 = ReadNtpTimestamp(received, 40);

        // NTP 오프셋 = ((T2 - T1) + (T3 - T4)) / 2
        // 서버 시간 = T4 + offset
        var offset = ((t2 - t1) + (t3 - t4)) / 2;
        var serverNow = DateTime.UtcNow + offset;
        var rtt = (t4 - t1) - (t3 - t2);

        return new NtpResult
        {
            Success = true,
            UtcNow = serverNow,
            Offset = offset,
            RoundTripTime = rtt,
            Server = server
        };
    }

    /// <summary>NTP 64비트 타임스탬프를 DateTime으로 변환 (초.소수초)</summary>
    private static DateTime ReadNtpTimestamp(byte[] buffer, int offset)
    {
        // 상위 32비트: 초 (빅엔디안)
        ulong seconds = (ulong)buffer[offset] << 24
                      | (ulong)buffer[offset + 1] << 16
                      | (ulong)buffer[offset + 2] << 8
                      | buffer[offset + 3];

        // 하위 32비트: 소수초 (빅엔디안)
        ulong fraction = (ulong)buffer[offset + 4] << 24
                       | (ulong)buffer[offset + 5] << 16
                       | (ulong)buffer[offset + 6] << 8
                       | buffer[offset + 7];

        var ms = (double)fraction / 0x100000000L * 1000.0;
        return NtpEpoch.AddSeconds(seconds).AddMilliseconds(ms);
    }
}

public sealed class NtpResult
{
    public bool Success { get; init; }
    public DateTime UtcNow { get; init; }
    public TimeSpan Offset { get; init; }
    public TimeSpan RoundTripTime { get; init; }
    public string Server { get; init; } = "";
    public string Error { get; init; } = "";
}
