using LocalDNSServer;
using System.Net;


public class DNSQuestion
{
    public string Name { get; set; }
    public DNSRecordType Type { get; set; }
}

public class DNSRecord
{
    public string Name { get; set; }
    public DNSRecordType Type { get; set; }
    public IPAddress IPAddress { get; set; }
    public string CNAME="";

    public DNSRecord(string name, DNSRecordType type, int ttl)
    {
        Name = name;
        Type = type;
    }
}

public enum DNSRecordType
{
    A = 1,
    CNAME = 5,
    MX = 15,
    AAAA = 28
}

[Flags]
public enum DNSFlags
{
    None = 0,
    Response = 0x8000, //1bit, 查询报文为0，响应报文为1
    OpcodeMask = 0x7800, //4bit, 0表示标准查询，1表示反向查询，2表示服务器状态请求
    Authoritative = 0x0400, //1bit, 仅当响应报文时有效，1表示响应的服务器是否为权威服务器
    Truncated = 0x0200, //1bit, 1表示响应已超过512字节，被截断
    RecursionDesired = 0x0100,//1bit, 1表示客户端期望服务器递归查询, 0表示迭代查询
    RecursionAvailable = 0x0080,//1bit, 1表示服务器支持递归查询
    Reserved = 0x0040,//1bit, 保留位
    AuthenticatedData = 0x0020,//1bit, 1表示响应报文中所有数据都已过验证
    CheckingDisabled = 0x0010,//1bit, 1表示客户端请求服务器不要执行DNSSEC验证
    ResponseCodeMask = 0x000F//4bit, 0表示没有错误，1表示格式错误，2表示服务器错误，3表示名字错误……
}

class Program
{
    static void Main()
    {
        DNSServer dnsesServer = new DNSServer();

        dnsesServer.Start();
    }
}
