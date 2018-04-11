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

    class IRRobotFilter : IReceiveFilter<StringPackageInfo>
    {
        private readonly SuperSocket.ProtoBase.SearchMarkState<byte> m_BeginSearchState;
        private readonly SuperSocket.ProtoBase.SearchMarkState<byte> m_EndSearchState;
        private readonly SuperSocket.ProtoBase.SearchMarkState<byte> m_EndSearchState2;
        private bool m_FoundBegin = false;

        public IRRobotFilter()
        {
            m_BeginSearchState = new SuperSocket.ProtoBase.SearchMarkState<byte>(new byte[] {(byte)'>'});
            m_EndSearchState = new SuperSocket.ProtoBase.SearchMarkState<byte>(Encoding.ASCII.GetBytes("!E"));
            m_EndSearchState2 = new SuperSocket.ProtoBase.SearchMarkState<byte>(new byte[] { (byte)'\r' });
        }

        private bool CheckChanged(byte[] oldMark, byte[] newMark)
        {
            if (oldMark.Length != newMark.Length)
                return true;

            for (var i = 0; i < oldMark.Length; i++)
            {
                if (oldMark[i] != newMark[i])
                    return true;
            }

            return false;
        }

        public void ChangeBeginMark(byte[] beginMark)
        {
            if (!CheckChanged(m_BeginSearchState.Mark, beginMark))
                return;

            m_BeginSearchState.Change(beginMark);
        }

        public void ChangeEndMark(byte[] endMark)
        {
            if (!CheckChanged(m_EndSearchState.Mark, endMark))
                return;

            m_EndSearchState.Change(endMark);
        }

        public StringPackageInfo ResolvePackage(IBufferStream bufferStream)
        {
            byte[] bytes = new byte[bufferStream.Length];
            bufferStream.Read(bytes, 0, bytes.Length);
            string str = System.Text.Encoding.ASCII.GetString(bytes);
            AppLog.AddLog("Ir接收:\n" + str);

            string[] str1 = str.Split('\r');
            string[] strArry = str1.ElementAt(str1.Length - 2).Split(' ');
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

        public virtual StringPackageInfo Filter(BufferList data, out int rest)
        {
            rest = 0;

            int searchEndMarkOffset;
            int searchEndMarkLength;

            var currentSegment = data.Last;
            var readBuffer = currentSegment.Array;
            var offset = currentSegment.Offset;
            var length = currentSegment.Count;

            int totalParsed = 0;

            if (!m_FoundBegin)
            {
                int pos = readBuffer.SearchMark(offset, length, m_BeginSearchState, out totalParsed);

                if (pos < 0)
                {
                    //All received data is part of the begin mark
                    if (m_BeginSearchState.Matched > 0 && data.Total == m_BeginSearchState.Matched)
                        return new StringPackageInfo("Err-Package", "", null);

                    //Invalid data, contains invalid data before the regular begin mark
                    State = FilterState.Error;
                    return new StringPackageInfo("Err-Package", "", null);
                }

                //Found the matched begin mark
                if (pos != offset)//But not at the beginning, contains invalid data before the regular begin mark
                {
                    State = FilterState.Error;
                    return new StringPackageInfo("Err-Package", "", null);
                }

                //Found start mark, then search end mark
                m_FoundBegin = true;

                searchEndMarkOffset = offset + totalParsed;

                //Reach end
                if (offset + length <= searchEndMarkOffset)
                    return new StringPackageInfo("Err-Package", "", null);

                searchEndMarkLength = offset + length - searchEndMarkOffset;
            }
            else//Already found begin mark
            {
                searchEndMarkOffset = offset;
                searchEndMarkLength = length;
            }

            while (true)
            {
                var endPos = readBuffer.SearchMark(searchEndMarkOffset, searchEndMarkLength, m_EndSearchState, out int parsedLength);

                //Haven't found end mark
                if (endPos < 0)
                {
                    return new StringPackageInfo("Err-Package", "", null);
                }

                totalParsed += parsedLength; //include begin mark if the mark is found in this round receiving
                rest = length - totalParsed;

                searchEndMarkOffset = offset + totalParsed;
                searchEndMarkLength = offset + length - searchEndMarkOffset;
                //if (rest > 0)
                //    data.SetLastItemLength(totalParsed);

                endPos = readBuffer.SearchMark(searchEndMarkOffset, searchEndMarkLength, m_EndSearchState2, out parsedLength);

                //Haven't found end mark
                if (endPos < 0)
                {
                    return new StringPackageInfo("Err-Package", "", null);
                }

                totalParsed += parsedLength; //include begin mark if the mark is found in this round receiving
                rest = length - totalParsed;

                if (rest > 0)
                    data.SetLastItemLength(totalParsed);

                var packageInfo = ResolvePackage(this.GetBufferStream(data));

                if (!ReferenceEquals(packageInfo, default(StringPackageInfo)))
                {
                    Reset();
                    return packageInfo;
                }

                if (rest > 0)
                {
                    searchEndMarkOffset = endPos + m_EndSearchState.Mark.Length;
                    searchEndMarkLength = rest;
                    continue;
                }

                //Not found end mark
                return new StringPackageInfo("Err-Package", "", null);
            }
        }

        public IReceiveFilter<StringPackageInfo> NextReceiveFilter { get; protected set; }

        public FilterState State { get; protected set; }

        public void Reset()
        {
            m_BeginSearchState.Matched = 0;
            m_EndSearchState.Matched = 0;
            m_FoundBegin = false;
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

        private void OnConnected(Object state, EventArgs e) => AppLog.Info("系统", "成功与Ir控制器建立连接");

        private void OnClosed(Object state, EventArgs e) => AppLog.Info("系统", "与Ir控制器连接断开");

        public bool SendCmd(string cmd)
        {
            irSendBuffer.Enqueue(cmd);
            return true;
        }

        private bool __SendCmd(string cmd)
        {          
            if (!irIsIdle)
            {
                return false;
            }

            string send = cmd;
            if (cmd.IndexOf("\n") != cmd.Length - 1)
                send = cmd + "\n";

            irClient.Send(Encoding.ASCII.GetBytes(send));
            irLastSend = cmd;
            irIsIdle = false;
            return true;
        }

        private void OnRecieve(StringPackageInfo request)
        {
            if (request.Key == "Err-Package")
            {
                return;
            }

            if (request.Key == "ERROR")
            {
                irResetC = 5;
                irNeedReset = true;
                AppLog.Info("系统", "收到错误返回，将会自动Reset");
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
                    __SendCmd("reset");
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
