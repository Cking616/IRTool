using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SuperSocket.ProtoBase;
using SuperSocket.ClientEngine;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace IRTool
{
    public static class AppLog
    {
        private static Queue<string> IrRobotRevLog = new Queue<string>();

        public static void AddLog(string log) { IrRobotRevLog.Enqueue(log); }

        public static void Info(string cls, string log)
        {
            string str = cls + ":" + log;
            IrRobotRevLog.Enqueue(str);
        }

        public static string ReadLog() { return IrRobotRevLog.Dequeue(); }

        public static bool IsEmpty() { return IrRobotRevLog.Count == 0; }

        public static void Error(string err)
        {
            err = "Error:" + err;
            IrRobotRevLog.Enqueue(err);
        }
    }

    class IRRobotFilter : TerminatorReceiveFilter<StringPackageInfo>
    {
        public IRRobotFilter()
            : base(Encoding.ASCII.GetBytes("\r\n\0"))
        {
        }

        public override StringPackageInfo ResolvePackage(IBufferStream bufferStream)
        {
            byte[] bytes = new byte[bufferStream.Length];
            bufferStream.Read(bytes, 0, bytes.Length);
            string str = System.Text.Encoding.ASCII.GetString(bytes);
            str = str.Replace("\0", string.Empty);
            AppLog.AddLog("Ir接收:" + str);

            if(str.StartsWith(">"))
            {
                return new StringPackageInfo("BEGIN", "", null);
            }

            if (str.StartsWith("!"))
            {
                string[] strArry = str.Split();
                string strHead = strArry[1];
                string[] strParam = strArry.Skip(2).ToArray();
                string[] strHeads = strHead.Split('-');
                if (strHeads.Length < 2)
                {
                    return new StringPackageInfo(strHead, "", strParam);
                }
                else
                {
                    string strBody = strHeads[0];
                    strHead = strHeads[1];
                    return new StringPackageInfo(strHead, strBody, strParam);
                }
            }
            else
            {
                string[] strArry = str.Split();
                string strHead = strArry[0];
                string[] strParam = strArry.Skip(2).ToArray();
                string[] strHeads = strHead.Split('-');
                if (strHeads.Length < 2)
                {
                    return new StringPackageInfo(strHead, "", strParam);
                }
                else
                {
                    string strBody = strHeads[0];
                    strHead = strHeads[1];
                    return new StringPackageInfo(strHead, strBody, strParam);
                }
            }
        }
    }

    class IRRobot
    {
        private EasyClient irClient;
        private string irAddress = "";
        private int irPort = 5000;
        private int irResetC = 0;
        private bool irNeedReset = false;
        private bool irIsIdle = true;
        private string irLastSend = "";
        private IRRobotFilter irFilter = new IRRobotFilter();
        private Queue<string> irSendBuffer = new Queue<string>();

        public bool IsConnected { get => irClient.IsConnected; }
        public bool IrNeedReset { get => irNeedReset;}
        public bool IrIsIdle { get => irIsIdle; }


        public IRRobot(string address, int port)
        {
            irAddress = address;
            irPort = port;
            irClient = new EasyClient();

            irClient.Connected += OnConnected;
            irClient.Closed += OnClosed;
            // Initialize the client with the receive filter and request handler
            irClient.Initialize(irFilter, OnRecieve);
            irClient.ConnectAsync(new IPEndPoint(IPAddress.Parse(irAddress), irPort));

            DispatcherTimer timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            timer.Tick += OnTimer;
            timer.IsEnabled = true;
        }

        public bool LearnStation(string station, bool isLow, bool isPerch, int index)
        {
            string cmd = "learn " + station;

            if (isPerch)
            {
                cmd += " perch,";
            }
            else
            {
                cmd += " inside,";
            }

            if (isLow)
            {
                index = -index;
            }

            cmd += string.Format(" index = {0:D},", index);

            irSendBuffer.Enqueue(cmd);
            return true;
        }

        public bool MoveStation(string station, bool isHigh, bool isInside, bool islinear, int index, int speed)
        {
            string cmd = "move " + station;

            if(!isInside)
            {
                cmd += " perch,";
            }
            else
            {
                cmd += " inside,";
            }

            if(!isHigh)
            {
                index = -index;
            }

            cmd += string.Format(" index = {0:D},", index);

            cmd += string.Format(" speed {0:D}", speed);

            if(islinear)
            {
                cmd += ", linear";
            }

            irSendBuffer.Enqueue(cmd);
            return true;
        }

        public bool SendCmd(string cmd)
        {
            irSendBuffer.Enqueue(cmd);
            return true;
        }

        private void OnConnected(Object state, EventArgs e) => AppLog.Info("系统", "成功与Ir控制器建立连接");

        private void OnClosed(Object state, EventArgs e) => AppLog.Info("系统", "与Ir控制器连接断开");

        private bool __SendCmd(string cmd)
        {          
            if (!irIsIdle )
            {
                if(!irNeedReset)
                {
                    return false;
                }
            }

            string send = cmd;
            if (cmd.IndexOf("\n") != cmd.Length - 1)
                send = cmd + "\n";

            irClient.Send(Encoding.ASCII.GetBytes(send));
            irLastSend = cmd.Split()[0].ToUpper();
            irIsIdle = false;
            return true;
        }

        private void OnRecieve(StringPackageInfo request)
        {
            if (request.Key == "BEGIN")
            {
                return;
            }

            if (request.Key == "ERROR")
            {
                irResetC = 15;
                irNeedReset = true;
                AppLog.Info("系统", "收到错误返回，将会自动Reset");
                irSendBuffer.Clear();
                // SendCmd("RESET\n");
            }

            if (request.Key == "RESET" && request.Body == "END")
            {
                AppLog.Info("系统", "Reset成功");
                irNeedReset = false;
                irIsIdle = true;
                return;
            }

            if(request.Key == irLastSend && request.Body == "END")
            {
                irIsIdle = true;
                string msg = request.Key +  "指令执行成功";
                AppLog.Info("系统", msg);
            }
        }

        private void OnTimer(Object state, EventArgs e)
        {
            if (irNeedReset)
            {
                if (irResetC > 0)
                {
                    irResetC--;
                }
                else
                {
                    AppLog.Info("系统", "发送Reset");
                    __SendCmd("reset");
                    irResetC = 00;
                }
                return;
            }

            if (irSendBuffer.Count > 0)
            {
                string cmd = irSendBuffer.ElementAt(0);
                if(__SendCmd(cmd))
                {
                    string msg = "执行指令" + cmd;
                    AppLog.Info("系统", msg);
                    irSendBuffer.Dequeue();
                }
            }
        }
    }
}
