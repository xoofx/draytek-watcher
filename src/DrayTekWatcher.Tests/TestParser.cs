using NUnit.Framework;
using DrayTekWatcher.Core.DrayTek;
using System;

namespace DrayTekWatcher.Tests
{
    public class TestParser
    {
        [Test]
        public void WanStatus()
        {
            var statusText = @"WAN1: Online, stall=N
 Mode: DHCP Client, Up Time=04:55:57
 IP=107.79.1.108, GW IP=107.127.255.7
 TX Packets=1627523, TX Rate(bps)=25640, RX Packets=2816935, RX Rate(bps)=21944
 Primary DNS=8.8.8.8, Secondary DNS=8.8.4.4

WAN2: Offline, stall=Y
 Mode: DHCP Client, Up Time=00:00:00
 IP=---, GW IP=---
 TX Packets=0, TX Rate(bps)=0, RX Packets=1179, RX Rate(bps)=0
 Primary DNS=0.0.0.0, Secondary DNS=0.0.0.0

USB_WAN3: Offline, stall=N
 Mode: ---, Up Time=00:00:00
 IP=---, GW IP=---
 TX Packets=0, TX Rate(bps)=0, RX Packets=0, RX Rate(bps)=0
 Primary DNS=0.0.0.0, Secondary DNS=0.0.0.0

USB_WAN4: Offline, stall=N
 Mode: ---, Up Time=00:00:00
 IP=---, GW IP=---
 TX Packets=0, TX Rate(bps)=0, RX Packets=0, RX Rate(bps)=0
 Primary DNS=0.0.0.0, Secondary DNS=0.0.0.0

--- MORE ---   ['q': Quit, 'Enter': New Lines, 'Space Bar': Next Page] ---

PVC_WAN5: Offline, stall=N
 Mode: ---, Up Time=00:00:00
 IP=---, GW IP=---
 TX Packets=0, TX Rate(bps)=0, RX Packets=0, RX Rate(bps)=0
 Primary DNS=0.0.0.0, Secondary DNS=0.0.0.0

PVC_WAN6: Offline, stall=N
 Mode: ---, Up Time=00:00:00
 IP=---, GW IP=---
 TX Packets=0, TX Rate(bps)=0, RX Packets=0, RX Rate(bps)=0
 Primary DNS=0.0.0.0, Secondary DNS=0.0.0.0

PVC_WAN7: Offline, stall=N
 Mode: ---, Up Time=00:00:00
 IP=---, GW IP=---
 TX Packets=0, TX Rate(bps)=0, RX Packets=0, RX Rate(bps)=0

PVC_WAN8: Offline, stall=N
 Mode: ---, Up Time=00:00:00
 IP=---, GW IP=---
 TX Packets=0, TX Rate(bps)=0, RX Packets=0, RX Rate(bps)=0

: Offline, stall=N
--- MORE ---   ['q': Quit, 'Enter': New Lines, 'Space Bar': Next Page] ---

 Mode: ---, Up Time=00:00:00
 IP=---, GW IP=---
 TX Packets=0, TX Rate(bps)=0, RX Packets=0, RX Rate(bps)=0";

            var list = DrayTekCliParser.ParseWanStatus(statusText);
            foreach (var item in list)
            {
                Console.WriteLine($"{item}");
            }
        }
    }
}