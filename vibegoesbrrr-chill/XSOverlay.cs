using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace CVRGoesBrrr
{
    class XSNotify
    {
        private int mPort;
        private string mIcon;

        private struct XSOMessage
        {
            public int MessageType { get; set; }
            public int Index { get; set; }
            public float Volume { get; set; }
            public string AudioPath { get; set; }
            public float Timeout { get; set; }
            public string Title { get; set; }
            public string Content { get; set; }
            public string Icon { get; set; }
            public float Height { get; set; }
            public float Opacity { get; set; }
            public bool UseBase64Icon { get; set; }
            public string SourceApp { get; set; }
        }

        public static XSNotify Create(int port = 42069)
        {
            if (Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Xiexe\XSOverlay") != null)
            {
                return new XSNotify(port);
            }
            else
            {
                return null;
            }
        }

        private XSNotify(int port)
        {
            mPort = port;

            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("rrrr.png"))
            {
                var buffer = new byte[stream.Length];
                stream.Read(buffer, 0, (int)stream.Length);
                mIcon = Convert.ToBase64String(buffer);
            }
        }

        public void Notify(string title, string message = "", float timeout = 2)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), mPort);

            XSOMessage msg = new XSOMessage();
            msg.MessageType = 1;
            msg.Title = title;
            msg.Content = message;
            msg.Height = 140f;
            msg.SourceApp = "VibeGoesBrrr";
            msg.Timeout = timeout;
            msg.Volume = 0.25f;
            msg.AudioPath = "default";
            msg.Opacity = 1f;
            msg.UseBase64Icon = true;
            msg.Icon = mIcon;

            var json = JsonConvert.SerializeObject(msg);
            socket.SendTo(Encoding.UTF8.GetBytes(json), endpoint);
        }
    }
}