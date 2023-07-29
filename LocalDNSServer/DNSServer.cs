using System.Net.Sockets;
using System.Net;
using System.Text;

namespace LocalDNSServer
{
    class DNSServer
    {
        private const int DNSPort = 53;
        private const int SenderPort = 55555;
        private const int DefaultTTL = 10;
        private const int DefaultCacheSize = 3;
        private const string GatewayIPAddress = "192.168.1.1";
        
        private Dictionary<string, DNSResponse> dnsCache;

        
        private UdpClient udpListener;
        private UdpClient udpListener1;

        //记录发送DNS请求的本地应用端口
        private Dictionary<string, IPEndPoint> dictionary=new Dictionary<string, IPEndPoint>();

        public DNSServer()
        {
            dnsCache = new Dictionary<string, DNSResponse>();

            //创建UDP监听器，绑定监听的端口
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, DNSPort);
            IPEndPoint localEndPoint1 = new IPEndPoint(IPAddress.Any,SenderPort);
            udpListener = new UdpClient(localEndPoint);
            udpListener1 = new UdpClient(localEndPoint1);
            Console.WriteLine($"DNS服务器已启动，监听端口:{DNSPort}，发送端口:{SenderPort}\n");
        }

        public void Start()
        {
            // 53端口,udpListener，面向本地应用
            Task.Run(() =>
            {
                while (true)
                {
                    IPEndPoint senderEndPoint = new IPEndPoint(IPAddress.Any, 0);
                    DNSRequest request;
                    DNSMessage receivedMessage;
                    try
                    {
                        receivedMessage= StartReceive(ref senderEndPoint,udpListener);
                    }
                    catch (SocketException e)
                    {
                        Console.WriteLine(e);
                        continue;
                    }
                    if (receivedMessage.IsRequest)
                    {
                        request = (DNSRequest)receivedMessage;
                        
                        //暂存的IPEndPoint,用于接收到消息后回复给发送端口
                        string name = request.Questions[0].Name;
                        if (dictionary.ContainsKey(name))
                        {
                            dictionary.Remove(name);
                        }
                        dictionary.Add(name,senderEndPoint);
                        Console.WriteLine($"收到请求，Hostname:{request.Questions[0].Name}\n");

                        DNSResponse response;
                        if (dnsCache.TryGetValue(name,out response))
                        {
                            Console.WriteLine("查询到DNS缓存中存在相应回答，直接返回");
                            Reply(response);
                        }
                        else
                        {
                            StartSend(request);
                        }
                        
                    }
                }
            });
            
            // 55555端口，udpListener1，面向路由
            Task.Run(() =>
            {
                while (true)
                {
                    IPEndPoint senderEndPoint = new IPEndPoint(IPAddress.Any, 0);
                    DNSResponse response;
                    DNSMessage receivedMessage;
                    try
                    {
                        receivedMessage = StartReceive(ref senderEndPoint,udpListener1);

                    }
                    catch (SocketException e)
                    {
                        Console.WriteLine(e);
                        continue;
                    }
                    
                    if (!receivedMessage.IsRequest)
                    {
                        response = (DNSResponse) receivedMessage;
                        Reply(response);

                        //将回复放到DNS缓存中
                        string domainName = response.Questions[0].Name;
                        if (!dnsCache.ContainsKey(domainName))
                        {
                            //缓冲区已达到最大限制，移除第一条记录
                            if (dnsCache.Count>=DefaultCacheSize)
                            {
                                //哈希表的删除操作比较麻烦，不知道这里有没有更优的方案
                                foreach (var key in dnsCache.Keys)
                                {
                                    dnsCache.Remove(key);
                                    Console.WriteLine($"域名为{key}的记录因 缓冲区溢出 从缓冲区移除, 当前缓冲区 {dnsCache.Count}/{DefaultCacheSize}\n");
                                    break;
                                }
                            }
                            dnsCache.Add(domainName,response);
                            Console.WriteLine($"域名为{domainName}的记录加入到了缓冲区, 当前缓冲区 {dnsCache.Count}/{DefaultCacheSize}\n");

                            //此处TTL模拟，直接开启一个新线程来计时，效率同样底下
                            Task.Run(() =>
                            {
                                //等待TTL时长
                                Thread.Sleep(DefaultTTL*1000);
                                //如果没有因为缓冲区溢出而移除该条记录，则此处因TTL截止而移除记录
                                if (dnsCache.ContainsKey(domainName))
                                {
                                    dnsCache.Remove(domainName);
                                    Console.WriteLine($"域名为{domainName}的记录因 TTL截止 从缓冲区移除, 当前缓冲区 {dnsCache.Count}/{DefaultCacheSize}\n");
                                }
                            });
                        }
                        

                    }
                }
            });
            Console.ReadLine();
        }

        //处理回复到本地应用的逻辑
        private void Reply(DNSResponse response)
        {
            StringBuilder stringBuilder=new StringBuilder($"收到回复，Hostname:{response.Answers[0].Name}\n");
            foreach (var a in response.Answers)
            {
                if (a.Type==DNSRecordType.CNAME)
                {
                    string str = $"\tName:{a.Name} \t CNAME:{a.CNAME}\n";
                    stringBuilder.Append(str);
                }
                else if (a.Type == DNSRecordType.A){
                    string str = $"\tName:{a.Name} \t IP:{a.IPAddress}\n";
                    stringBuilder.Append(str);
                }else if (a.Type== DNSRecordType.AAAA)
                {
                    string str = $"\tName:{a.Name} \t 查询IPV6，忽略\n";
                    stringBuilder.Append(str);
                }
            }
            Console.WriteLine(stringBuilder);
            StartSend(response);
        }
        private DNSMessage StartReceive(ref IPEndPoint senderEndPoint, UdpClient client)
        {
            byte[] responseData;
            //为什么会偶发性的报错 远程主机强制关闭了一个现有的连接 不知道原因
            try
            {
                responseData = client.Receive(ref senderEndPoint);
                DNSResponse response = DNSResponse.Parse(responseData);
                DNSRequest request;
                if (response.Answers.Count==0)
                {
                    request = DNSRequest.Parse(responseData);
                    request.IsRequest = true;
                    return request;
                }
                response.IsRequest = false;
                return response;
            }
            catch (SocketException e)
            {
                Console.WriteLine(e);
                throw e;
            }

            return null;

        }
  
        private void StartSend(DNSMessage message)
        {
            if (message.IsRequest)
            {
                DNSRequest request = (DNSRequest) message;
                // 获取原始DNS请求的字节数组
                byte[] requestData = request.ByteArray;

                // 向路由器的dns服务器发送查询请求
                IPAddress upstreamIP = IPAddress.Parse(GatewayIPAddress);
                int upstreamPort = 53;
                IPEndPoint routerIpEndPoint = new IPEndPoint(upstreamIP, upstreamPort);
                udpListener1.Send(requestData, requestData.Length,routerIpEndPoint);
            }
            else
            {
                DNSResponse response = (DNSResponse) message;
                // 获取原始DNS应答的字节数组
                byte[] responseByte = response.ByteArray;

                // 向本机发送DNS应答
                IPEndPoint senderIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
                if (dictionary.TryGetValue(response.Questions[0].Name,out senderIpEndPoint))
                {
                    udpListener.Send(responseByte, responseByte.Length,senderIpEndPoint);
                }

            }
            
            
        }
        

        //问题部分由三个部分组成，名称，查询类型，查询类
        //名称长度不固定，例如baidu.com: 5 b a i d u 3 c o m 0
        public static string ReadName(byte[] data, ref int offset)
        {
            List<byte> nameBytes = new List<byte>();
            int nameLength = data[offset];
            while (nameLength > 0)
            {
                //0xC0: 1100 0000，如果计数位的前两位为11，则代表为压缩指针，其实际记录的值为相对DNS报文头的偏移值
                if ((nameLength & 0xC0) == 0xC0)
                {
                    // 压缩指针有16位，占两个字节，读取指针实际存储的值，0x3F: 0011 1111
                    int pointer = ((nameLength & 0x3F) << 8) | data[offset + 1];
                    int savedOffset = offset;
                    offset = pointer;

                    //递归读取指针指向的域名
                    nameBytes.AddRange(Encoding.UTF8.GetBytes(ReadName(data, ref offset)));
                    offset = savedOffset + 2;
                    break;
                }
                else
                {
                    //正常读取域名
                    nameBytes.AddRange(data.Skip(offset + 1).Take(nameLength));
                    offset += nameLength + 1;
                    nameLength = data[offset];
                    if (nameLength > 0)
                        nameBytes.Add((byte)'.');
                    else
                    {
                        offset++;
                    }
                }
            }
            return Encoding.ASCII.GetString(nameBytes.ToArray());
        }
    }

}
