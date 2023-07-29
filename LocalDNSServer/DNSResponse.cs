using System.Net;
using System.Text;

namespace LocalDNSServer
{
    public class DNSResponse : DNSMessage
    {
        
        public byte[] GetBytes()
        {
            return ByteArray;
        }

        // 辅助方法，用于将域名转换为DNS报文中的字节表示
        private static byte[] EncodeName(string name)
        {
            List<byte> nameBytes = new List<byte>();
            string[] labels = name.Split('.');
            foreach (string label in labels)
            {
                nameBytes.Add((byte)label.Length);
                nameBytes.AddRange(Encoding.ASCII.GetBytes(label));
            }
            nameBytes.Add(0); // 结束标记
            return nameBytes.ToArray();
        }

        public static DNSResponse Parse(byte[] data)
        {
            DNSResponse response = new DNSResponse();
            response.ByteArray = data;
            // 解析DNS响应的字节数组
            response.ID = (ushort)((data[0] << 8) | data[1]);

            DNSFlags flags = (DNSFlags)((data[2] << 8) | data[3]);
            response.Flags = flags;

            ushort questionCount = (ushort)((data[4] << 8) | data[5]);
            response.Questions = new DNSQuestion[questionCount];

            ushort answerCount = (ushort)((data[6] << 8) | data[7]);
            response.Answers = new List<DNSRecord>();

            int offset = 12;

            // 解析问题部分
            for (int i = 0; i < questionCount; i++)
            {
                DNSQuestion question = new DNSQuestion();
                question.Name = DNSServer.ReadName(data, ref offset);
                question.Type = (DNSRecordType)((data[offset] << 8) | data[offset + 1]);
                offset += 4;
                response.Questions[i] = question;
            }

            // 解析回答部分
            for (int i = 0; i < answerCount; i++)
            {
                DNSRecord answer = new DNSRecord(
                    DNSServer.ReadName(data, ref offset),
                    (DNSRecordType)((data[offset] << 8) | data[offset + 1]),
                    ((data[offset + 4] << 24) | (data[offset + 5] << 16) | (data[offset + 6] << 8) | data[offset + 7])
                );
                offset += 8;
                
                ushort dataLength = (ushort)((data[offset] << 8) | data[offset + 1]);
                offset += 2;

                if (answer.Type==DNSRecordType.CNAME)
                {
                    answer.CNAME = DNSServer.ReadName(data, ref offset);
                }else if (answer.Type==DNSRecordType.A)
                {
                    byte[] ipBytes = new byte[dataLength];
                    Array.Copy(data, offset, ipBytes, 0, dataLength);
                    int ip1 = ipBytes[0] ;
                    int ip2 = ipBytes[1] ;
                    int ip3 = ipBytes[2] ;
                    int ip4 = ipBytes[3] ;
                    IPAddress IP = IPAddress.Parse($"{ip1}.{ip2}.{ip3}.{ip4}");
                    answer.IPAddress = IP;
                    offset += dataLength;
                }

                response.Answers.Add(answer);
            }
            
            return response;
        }
    }
}
