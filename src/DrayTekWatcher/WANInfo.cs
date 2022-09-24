// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace DrayTekWatcher.Core;

using System.Diagnostics;
using System.Text;

[DebuggerDisplay("{" + nameof(Name) + "}, Online: {" + nameof(Status) + "}, Properties = {Count}")]
public class WANInfo : Dictionary<string, string>
{
    public WANInfo(string name)
    {
        Name = name;
    }

    public string Name { get; set; }

    public WANStatus Status { get; set; }

    public string? IPAddress { get; set; }

    public string? GatewayIPAddress { get; set; }

    public string ToDisplayText()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{this}");
        builder.AppendLine($"  IP = {IPAddress}");
        builder.AppendLine($"  GW IP = {GatewayIPAddress}");
        foreach (var kv in this.OrderBy(x => x.Key))
        {
            builder.AppendLine($"  {kv.Key} = {kv.Value}");
        }

        return builder.ToString();
    }

    public override string ToString()
    {
        return $"{nameof(Name)}: {Name}, {Status}";
    }
}