namespace SSDP
{
    using System;
    using System.Net.Sockets;
    using System.Net;
    using System.Text;
    using System.Collections.Generic;
    using System.Linq;
    using System.Timers;
    using System.Threading;
    using System.Threading.Tasks;
    public class MSearch
    {
        public Socket udp_socket = null;
        public byte[] msearch_query = Encoding.UTF8.GetBytes("M-SEARCH * HTTP/1.1\nHOST:239.255.255.250:1900\nMAN:\"ssdp:discover\"\nST: ssdp:all\nMX:3\n\n");
        public byte[] rbuffer = new byte[1024 * 64];
        public string str_resp = String.Empty;
        public List<string> str_resp_raw = new List<string>();
        public EndPoint remote_endpoint = (EndPoint)(new IPEndPoint(IPAddress.Any, 0));
        public string response = String.Empty;
        public SocketAsyncEventArgs sendEvent = new SocketAsyncEventArgs();
        public Dictionary<string, string> dict_resp = new Dictionary<string, string>();
        public Dictionary<string, object> all_results = new Dictionary<string, object>();
        public System.Timers.Timer timer = null;
        public bool receive = true;
        public Int32 response_index = 0;
        public MSearch()
        {
            this.Init_UdpSocket();
            this.send_receive(true);
        }
        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            this.close_udp_socket();
        }
        public void Init_UdpSocket()
        {
            this.udp_socket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Dgram,
                ProtocolType.Udp
            );
            this.udp_socket.SetSocketOption(
                SocketOptionLevel.Socket,
                SocketOptionName.ReuseAddress,
                true
            );
            this.udp_socket.Bind(new IPEndPoint(IPAddress.Any, 0));
            this.udp_socket.SetSocketOption(
                SocketOptionLevel.IP,
                SocketOptionName.MulticastTimeToLive,
                2
            );
            this.udp_socket.SetSocketOption(
                SocketOptionLevel.IP,
                SocketOptionName.MulticastLoopback,
                true
            );
            this.udp_socket.SetSocketOption(
                SocketOptionLevel.IP,
                SocketOptionName.MulticastInterface,
                IPAddress.Parse("239.255.255.250").GetAddressBytes()
            );
        }
        public void send_receive(bool send_to = false, SocketAsyncEventArgs e = null)
        {
            if (send_to)
            {
                using (this.timer = new System.Timers.Timer())
                {
                    this.sendEvent = new SocketAsyncEventArgs();
                    this.sendEvent.RemoteEndPoint = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);
                    this.sendEvent.SetBuffer(this.msearch_query, 0, this.msearch_query.Length);
                    this.sendEvent.Completed += OnSocketSendEventCompleted;
                    this.timer.Interval = 5000;
                    this.timer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
                    this.timer.Enabled = true;
                    this.udp_socket.SendToAsync(this.sendEvent);
                    this.CloseTimer();
                }
            } else
            {
                using (this.timer = new System.Timers.Timer())
                {
                    e.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                    this.rbuffer = new byte[1024 * 64];
                    this.udp_socket.ReceiveBufferSize = this.rbuffer.Length;
                    e.SetBuffer(this.rbuffer, 0, this.rbuffer.Length);
                    this.timer.Interval = 5000;
                    this.timer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
                    this.timer.Enabled = true;
                    this.receive = this.udp_socket.ReceiveFromAsync(e);
                    this.CloseTimer();
                }
            }
        }
        public void CloseTimer()
        {
            if (this.timer != null)
            {
                this.timer.Stop();
                this.timer.Close();
                this.timer.Dispose();
                this.timer = null;
            }
        }
        public bool check_available()
        {
            DateTime now = DateTime.Now;
            Int32 av = this.udp_socket.Available;
            while (av == 0)
            {
                if (DateTime.Now < now.AddSeconds(5))
                {
                    av = this.udp_socket.Available;
                }
                else
                {
                    av = 1;
                }
            }
            if (this.udp_socket.Available == 0)
            {
                this.response = String.Join("\r\n", this.str_resp_raw);
                return false;
            } else
            {
                return true;
            }
        }
        public void close_udp_socket()
        {
            if (this.udp_socket != null)
            {
                this.udp_socket.Close();
                this.udp_socket.Dispose();
                this.udp_socket = null;
            }
        }
        public void OnSocketSendEventCompleted(object sender, SocketAsyncEventArgs e)
        {
            if(e.SocketError != SocketError.Success)
            {
                this.close_udp_socket();
                return;
            } else
            {
                switch(e.LastOperation)
                {
                    case SocketAsyncOperation.SendTo:
                        this.send_receive(false, e);
                        break;
                    case SocketAsyncOperation.ReceiveFrom:
                        if (this.check_available())
                        {
                            this.parse_response(e);
                            this.send_receive(false, e);
                        } else
                        {
                            this.close_udp_socket();
                        }
                        break;
                    default:
                        break;
                }
            }
        }
        public void parse_response(SocketAsyncEventArgs e)
        {
            this.dict_resp = new Dictionary<string, string>();
            string result = Encoding.UTF8.GetString(e.Buffer, 0, e.BytesTransferred);
            this.str_resp = result;
            this.str_resp_raw.Add(this.str_resp);
            string[] result_lines = this.str_resp.Split((Char)10);
            List<Dictionary<string,string>> one_result = new List<Dictionary<string, string>>();
            for (Int32 n = 0; n < result_lines.Length; n++)
            {
                string line = result_lines[n];
                string key = String.Empty;
                string value = String.Empty;
                if (line.Split((Char)58).Length > 1)
                {
                    key = line.Split((Char)58)[0].Trim();
                    string[] v = new string[(line.Split((Char)58).Length - 1)];
                    Int32 i = 0;
                    for (Int32 a = 1; a < line.Split((Char)58).Length; a++)
                    {
                        v[i] = line.Split((Char)58)[a];
                        i++;
                    }
                    value = String.Join(((Char)58).ToString(), v).Trim();
                }
                else
                {
                    key = line.Split((Char)32)[0].Trim();
                    string[] v = new string[(line.Split((Char)32).Length - 1)];
                    Int32 i = 0;
                    for (Int32 a = 1; a < line.Split((Char)32).Length; a++)
                    {
                        v[i] = line.Split((Char)32)[a];
                        i++;
                    }
                    value = String.Join(((Char)32).ToString(), v).Trim();
                }
                this.dict_resp = new Dictionary<string, string>();
                if (!String.IsNullOrEmpty(key) | !String.IsNullOrEmpty(value))
                {
                    this.dict_resp.Add(key, value);
                    one_result.Add(this.dict_resp);
                }
            }
            if (one_result.Count > 0)
            {
                this.all_results.Add(this.response_index.ToString(),one_result);
                this.response_index++;
            }
        }
    }
}
