namespace LocalDNSServer;

public class DNSMessage
{
    public bool IsRequest=false;
    public ushort ID { get; set; }
    public DNSFlags Flags { get; set; }
    public DNSQuestion[] Questions { get; set; }
    public List<DNSRecord> Answers { get; set; }

    public byte[] ByteArray { get; set; }
}