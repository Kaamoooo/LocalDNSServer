namespace LocalDNSServer
{
    class DNSRequest : DNSMessage
    {

        //解析收到的DNS请求，将其拆解到DNSRequest类中
        public static DNSRequest Parse(byte[] data)
        {
            DNSRequest request = new DNSRequest();
            request.ByteArray = data;
            // ID, 16bits
            request.ID = (ushort)((data[0] << 8) | data[1]);

            //Flag, 16bits
            DNSFlags flags = (DNSFlags)((data[2] << 8) | data[3]);
            request.Flags = flags;

            //QuestionCount, 16bits
            ushort questionCount = (ushort)((data[4] << 8) | data[5]);
            request.Questions = new DNSQuestion[questionCount];


            //查询问题区域从第13个字节开始
            int offset = 12;
            for (int i = 0; i < questionCount; i++)
            {
                DNSQuestion question = new DNSQuestion();
                question.Name = DNSServer.ReadName(data, ref offset);
                question.Type = (DNSRecordType)((data[offset] << 8) | data[offset + 1]);
                offset += 4;
                request.Questions[i] = question;
            }

            return request;
        }



    }
}
