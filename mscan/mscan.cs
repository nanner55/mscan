using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Xml;

namespace mscan
{

    public partial class mscan : Form
    {

        static int MAX_PARAM = 24;
        static uint TIMEOUT = 100;
        static uint LOG_MSG_SIZE = 0x33;
        static Byte TRAILER_VALUE = 0x0D;

        enum eLogMsgType : Byte
        {
            LOG_24x2B = 0x81,
            LOG_24x1B = 0x82,
            LOG_5x2B = 0x83,
            LOG_24x2B_LT = 0x84,
            LOG_2x2B_6x1B_LT = 0x85,
            LOG_5x2B_LT = 0x86,
            LOG_COPY = 0x87,
            LOG_INV = 0x88,
            LOG_LT = 0x89,
        }

        struct sLogType
        {
            public Byte ReqHeader;
            public double Rate;
            public Byte RespHeader;
            public List<Tuple<int, int>> Elem;
            public int RespSize;
        }

        static Dictionary<eLogMsgType, sLogType> mLogMsgInfo = new Dictionary<eLogMsgType, sLogType>
        {
            { eLogMsgType.LOG_24x2B, new sLogType{ ReqHeader = (Byte)eLogMsgType.LOG_24x2B, Rate = 10, RespHeader = 0x01, Elem = new List<Tuple<int, int>>{ Tuple.Create(24, 2) }, RespSize = 0x33 } },
            { eLogMsgType.LOG_24x1B, new sLogType{ ReqHeader = (Byte)eLogMsgType.LOG_24x1B, Rate = 5, RespHeader = 0x02, Elem = new List<Tuple<int, int>>{ Tuple.Create(24, 1) }, RespSize = 0x1B } },
            { eLogMsgType.LOG_5x2B, new sLogType{ ReqHeader = (Byte)eLogMsgType.LOG_5x2B, Rate = 2.5, RespHeader = 0x03, Elem = new List<Tuple<int, int>>{ Tuple.Create(5, 2) }, RespSize = 0x0D } },
            { eLogMsgType.LOG_24x2B_LT, new sLogType{ ReqHeader = (Byte)eLogMsgType.LOG_24x2B_LT, Rate = 10, RespHeader = 0x04, Elem = new List<Tuple<int, int>>{ Tuple.Create(24, 2) }, RespSize = 0x33 } },
            { eLogMsgType.LOG_2x2B_6x1B_LT, new sLogType{ ReqHeader = (Byte)eLogMsgType.LOG_2x2B_6x1B_LT, Rate = 2.5, RespHeader = 0x05, Elem = new List<Tuple<int, int>>{ Tuple.Create(2, 2), Tuple.Create(6, 1) }, RespSize = 0x0B } },
            { eLogMsgType.LOG_5x2B_LT, new sLogType{ ReqHeader = (Byte)eLogMsgType.LOG_5x2B_LT, Rate = 2.5, RespHeader = 0x06, Elem = new List<Tuple<int, int>>{ Tuple.Create(5, 2) }, RespSize = 0x0D } },
            // other types are non-periodic responses with no logging
        };



        class CircularBuffer<type>
        {
            public CircularBuffer(int size) { mSize = size; mData = new type[size]; mHead = 0; mLength = 0; }

            public void Enqueue(type elem) {
                if (Length == mData.Length) throw (new Exception("enqueue: overflow"));
                mData[mHead + mLength++] = elem;
            }
            public type Dequeue() {
                if (Length == 0) throw (new Exception("dequeue: underflow"));
                var head = mHead;
                mHead = (mHead + 1) % mData.Length;
                mLength--;
                return mData[head];
            }
            public void Enqueue(type[] data, int size = 0) {
                if (size == 0) size = data.Length;
                if (mLength + size > mData.Length) throw (new Exception("enqueue: overflow"));
                var start = (mHead + mLength) % mData.Length;
                var copyElem = Math.Min(size, mData.Length - start);
                Buffer.BlockCopy(data, 0, mData, start, copyElem);
                Buffer.BlockCopy(data, copyElem, mData, 0, size - copyElem);
                mLength += size;
            }
            public type[] Dequeue(int size)
            {
                if (mLength < size) throw (new Exception("dequeue: underflow"));
                type[] data = null;
                if (size > 0)
                {
                    data = new type[size];
                    var copyElem = Math.Min(size, mData.Length - mHead);
                    Buffer.BlockCopy(mData, mHead, data, 0, copyElem);
                    Buffer.BlockCopy(mData, 0, data, copyElem, size - copyElem);
                    mHead = (mHead + size) % mData.Length;
                    mLength -= size;
                }
                return data;
            }

            public void Clear()
            {
                mHead = 0;
                mLength = 0;
            }

            type[] mData;
            int mHead;
            int mLength;
            int mSize;

            public int Length { get => mLength; }
            public int Size { get => mSize; }
        };

        // variables
        eLogMsgType mLogMsgType;
        System.Timers.Timer mEnqueueTimer;
        System.Timers.Timer mDequeueTimer;
        CircularBuffer<Byte> mBuffer = new CircularBuffer<Byte>(j2534.MAX_MSG_SIZE * 16);

        uint mDeviceId = uint.MaxValue;
        uint mChannelId = uint.MaxValue;
        uint mFilterId = uint.MaxValue;

        uint[] mData = new uint[MAX_PARAM];

        // Params
        XmlDocument mParamXml = null;
        DataTable mDataTable = new DataTable();
        struct sParamInfo {
            public string mDisplayName;
            public string mLogName;
            public string mRequestId;
            public string mUnitName;
            public string mEvalExpr;
            public double mChartMin;
            public double mChartMax;
        }
        Dictionary<string, sParamInfo> mParamInfo = new Dictionary<string, sParamInfo>();
        Dictionary<string, int> mParamIndex = new Dictionary<string, int>();
        System.Windows.Forms.DataVisualization.Charting.Chart[] mCharts;

        // ROM
        Byte[] mROMFile = null;
        XmlDocument mMetadataXml = null;
        Dictionary<string, XmlDocument> mMetadataInfo = new Dictionary<string, XmlDocument>();

        public mscan()
        {
            InitializeComponent();

            dataGridViewParam.Rows.Add(MAX_PARAM);

            mCharts = new System.Windows.Forms.DataVisualization.Charting.Chart[] { chart1, chart2, chart3, chart4, chart5, chart6, };

            buttonConnect.Enabled = false;

            this.Height = dataGridViewParam.Height + buttonConnect.Height;

            for (int i = 0; i < mData.Length; i++) mData[i] = 0;
        }

        private void buttonConnect_Click(object sender, EventArgs e)
        {
            j2534.PASSTHRU_MSG msg = new j2534.PASSTHRU_MSG();
            IntPtr pMsg = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(j2534.PASSTHRU_MSG)));
            j2534.eError1 ret;
            uint numMsgs = 1;

            if (mDeviceId != uint.MaxValue && mChannelId != uint.MaxValue)
            {
                buttonConnect.Enabled = false;
                buttonConnect.Update();

                // stop the queueing threads to avoid threading issues with accessing PassThru library
                mEnqueueTimer.Stop();
                mEnqueueTimer.Dispose();
                //mDequeueTimer.Stop();
                //mDequeueTimer.Dispose();

                // write logging cancel - NOTE: this hangs with a nonzero timeout
                msg.ProtocolID = (uint)j2534.eProtocol1.ISO9141;
                msg.RxStatus = 0;
                msg.TxFlags = (uint)j2534.eTxStatus.WAIT_P3_MIN_ONLY;
                msg.Timestamp = 0;
                msg.ExtraDataIndex = 0;
                msg.DataSize = LOG_MSG_SIZE;
                for (uint i = 0; i < msg.DataSize; i++) msg.Data[i] = 0x00;
                Marshal.StructureToPtr(msg, pMsg, false);
                numMsgs = 1;
                ret = (j2534.eError1)j2534.PassThruWriteMsgs(mChannelId, pMsg, ref numMsgs, TIMEOUT);
                //if (ret != j2534.eError1.ERR_SUCCESS) MessageBox.Show("PassThruWriteMsgs: cancel logging" + ret.ToString());

                //ret = (j2534.eError1)j2534.PassThruIoctl(mChannelId, (uint)j2534.eIoctl1.CLEAR_MSG_FILTERS, IntPtr.Zero, IntPtr.Zero);
                //if (ret != j2534.eError1.ERR_SUCCESS) MessageBox.Show("PassThruIoctl: CLEAR_MSG_FILTERS " + ret.ToString());

                // restart the enqueue timer to drain remaining log data
                mEnqueueTimer = new System.Timers.Timer();
                mEnqueueTimer.Interval = mLogMsgInfo[mLogMsgType].Rate * 2;
                mEnqueueTimer.AutoReset = true;
                mEnqueueTimer.Elapsed += HandleEnqueue;
                mEnqueueTimer.Start();

                // drain reads
                Thread.Sleep(5000);

                // make sure all timers are stopped and events quiesced
                mEnqueueTimer.Stop();
                mEnqueueTimer.Dispose();
                mDequeueTimer.Stop();
                mDequeueTimer.Dispose();
                // empty out receive buffer
                mBuffer.Clear();

                // 5 baud init test - NOTE: this fails, but that's ok.  It clears the DMA state machine.
                ret = (j2534.eError1)j2534.PassThru5BaudInit(mChannelId, 0x00);

                // sleep to wait for tactrix to power up again
                Thread.Sleep(4500);

                // disconnect
                ret = (j2534.eError1)j2534.PassThruDisconnect(mChannelId);
                if (ret != j2534.eError1.ERR_SUCCESS) MessageBox.Show("PassThruDisconnect: " + ret.ToString());
                ret = (j2534.eError1)j2534.PassThruClose(mDeviceId);
                if (ret != j2534.eError1.ERR_SUCCESS) MessageBox.Show("PassThruClose: " + ret.ToString());

                buttonConnect.Text = "Connect";
                buttonConnect.Enabled = true;
                buttonLoadParam.Enabled = true;
                buttonLoadROM.Enabled = true;
                dataGridViewParam.EditMode = DataGridViewEditMode.EditOnEnter;

                mDeviceId = uint.MaxValue;
                mChannelId = uint.MaxValue;
            }
            else
            {
                buttonConnect.Enabled = false;
                buttonConnect.Update();
                buttonLoadParam.Enabled = true;
                buttonLoadROM.Enabled = true;

                j2534.init();

                mDeviceId = uint.MaxValue;
                ret = (j2534.eError1)j2534.PassThruOpen(IntPtr.Zero, ref mDeviceId);
                if (ret != j2534.eError1.ERR_SUCCESS) MessageBox.Show("PassThruOpen: " + ret.ToString());

                byte[] apiVersion = new byte[256], dllVersion = new byte[256], firmwareVersion = new byte[256];
                ret = (j2534.eError1)j2534.PassThruReadVersion(apiVersion, dllVersion, firmwareVersion, mDeviceId);
                if (ret != j2534.eError1.ERR_SUCCESS) MessageBox.Show("PassThruReadVersion: " + ret.ToString());

                mChannelId = uint.MaxValue;
                ret = (j2534.eError1)j2534.PassThruConnect(mDeviceId, (uint)j2534.eProtocol1.ISO9141, (uint)(j2534.eConnect.ISO9141_K_LINE_ONLY | j2534.eConnect.ISO9141_NO_CHECKSUM), 62500, ref mChannelId);
                if (ret != j2534.eError1.ERR_SUCCESS) MessageBox.Show("PassThruConnect: " + ret.ToString());

                // clear all message filters
                ret = (j2534.eError1)j2534.PassThruIoctl(mChannelId, (uint)j2534.eIoctl1.CLEAR_MSG_FILTERS, IntPtr.Zero, IntPtr.Zero);
                ret = (j2534.eError1)j2534.PassThruIoctl(mChannelId, (uint)j2534.eIoctl1.CLEAR_RX_BUFFER, IntPtr.Zero, IntPtr.Zero);
                //ret = (j2534.eError1)j2534.PassThruIoctl(mChannelId, (uint)j2534.eIoctl1.CLEAR_TX_BUFFER, IntPtr.Zero, IntPtr.Zero);

                // setup timing config
                j2534.SCONFIG cfg = new j2534.SCONFIG();
                j2534.SCONFIG_LIST cfgList = new j2534.SCONFIG_LIST();
                IntPtr pCfg = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(j2534.SCONFIG)));
                IntPtr pCfgList = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(j2534.PASSTHRU_MSG)));
                // provide time to send logging cancel write
                cfg.Parameter = (uint)j2534.eConfig1.P3_MIN;
                cfg.Value = 0x4; // res 0.5ms
                cfgList.NumOfParams = 1;
                cfgList.ConfigPtr = pCfg;
                Marshal.StructureToPtr(cfg, pCfg, false);
                Marshal.StructureToPtr(cfgList, pCfgList, false);
                ret = (j2534.eError1)j2534.PassThruIoctl(mChannelId, (uint)j2534.eIoctl1.SET_CONFIG, pCfgList, IntPtr.Zero);
                // FIXME: try to decrease timeouts to get smaller messages
                cfg.Parameter = (uint)j2534.eConfig1.P2_MIN;
                cfg.Value = 0x1; // res 0.5ms
                Marshal.StructureToPtr(cfg, pCfg, false);
                //ret = (j2534.eError1)j2534.PassThruIoctl(mChannelId, (uint)j2534.eIoctl1.SET_CONFIG, pCfgList, IntPtr.Zero);
                cfg.Parameter = (uint)j2534.eConfig1.P2_MAX;
                cfg.Value = 0x1; // res 25ms
                Marshal.StructureToPtr(cfg, pCfg, false);
                //ret = (j2534.eError1)j2534.PassThruIoctl(mChannelId, (uint)j2534.eIoctl1.SET_CONFIG, pCfgList, IntPtr.Zero);
                Marshal.FreeHGlobal(pCfg);
                Marshal.FreeHGlobal(pCfgList);

                // setup filter to pass all.  the response message format is some what arbitrary
                msg.ProtocolID = (uint)j2534.eProtocol1.ISO9141;
                msg.RxStatus = 0;
                msg.TxFlags = 0;
                msg.Timestamp = 0;
                msg.ExtraDataIndex = 0;
                msg.DataSize = 1;
                for (uint i = 0; i < msg.DataSize; i++) msg.Data[i] = 0x0;

                Marshal.StructureToPtr(msg, pMsg, false);
                ret = (j2534.eError1)j2534.PassThruStartMsgFilter(mChannelId, (uint)j2534.eFilter.PASS_FILTER, pMsg, pMsg, IntPtr.Zero, ref mFilterId);

                // FIXME: hard code 24x2B for now
                mLogMsgType = eLogMsgType.LOG_24x2B;

                if (mDeviceId != uint.MaxValue && mChannelId != uint.MaxValue)
                {
                    // setup timers - logging is already enabled
                    // FIXME: temporary rate hacks because otherwise queue is overflowed
                    mDequeueTimer = new System.Timers.Timer();
                    mDequeueTimer.Interval = mLogMsgInfo[mLogMsgType].Rate / 2;
                    mDequeueTimer.AutoReset = true;
                    mDequeueTimer.Elapsed += HandleDequeue;
                    mDequeueTimer.Start();

                    mEnqueueTimer = new System.Timers.Timer();
                    mEnqueueTimer.Interval = mLogMsgInfo[mLogMsgType].Rate * 2;
                    mEnqueueTimer.AutoReset = true;
                    mEnqueueTimer.Elapsed += HandleEnqueue;
                    mEnqueueTimer.Start();
                }

                // write log message
                msg.ProtocolID = (uint)j2534.eProtocol1.ISO9141;
                msg.RxStatus = 0;
                msg.TxFlags = 0;
                msg.Timestamp = 0;
                msg.ExtraDataIndex = 0;
                msg.DataSize = LOG_MSG_SIZE;
                for (uint i = 0; i < msg.DataSize; i++) msg.Data[i] = 0x0;

                // header
                msg.Data[0x00] = mLogMsgInfo[mLogMsgType].ReqHeader;
                // data based on params
                for (int i = 0; i < MAX_PARAM; i++)
                {
                    var valueString = dataGridViewParam.Rows[i].Cells[1].Value?.ToString();
                    UInt16 value = 0;
                    try { value = Convert.ToUInt16(valueString, 16); } catch { }
                    msg.Data[2 * i + 1] = (Byte)((value >> 0) & 0xFF);
                    msg.Data[2 * i + 2] = (Byte)((value >> 8) & 0xFF);
                }
                // checksum
                for (uint i = 0; i < LOG_MSG_SIZE - 2; i++) msg.Data[LOG_MSG_SIZE - 2] += msg.Data[i];
                // trailer
                msg.Data[LOG_MSG_SIZE - 1] = TRAILER_VALUE;

                numMsgs = 1;
                Marshal.StructureToPtr(msg, pMsg, false);
                ret = (j2534.eError1)j2534.PassThruWriteMsgs(mChannelId, pMsg, ref numMsgs, TIMEOUT);
                if (ret != 0 || numMsgs != 1) MessageBox.Show("PassThruWriteMsgs: " + ret.ToString() + ", " + numMsgs.ToString());

                // NOTE: let timer thread read the message start
                
                if (mDeviceId != uint.MaxValue && mChannelId != uint.MaxValue)
                {
                    buttonConnect.Text = "Disconnect";
                    buttonLoadParam.Enabled = false;
                    buttonLoadROM.Enabled = false;
                    dataGridViewParam.EditMode = DataGridViewEditMode.EditProgrammatically;

                    // let it read the buffer before supporting the disconnect - this can lead to double presses for some reason?
                    //Thread.Sleep(1000);
                }

                buttonConnect.Enabled = true;
            }

            Marshal.FreeHGlobal(pMsg);
        }

        j2534.PASSTHRU_MSG rmsg = new j2534.PASSTHRU_MSG();
        IntPtr rpMsg = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(j2534.PASSTHRU_MSG)));
        void HandleEnqueue(object sender, EventArgs e)
        {
            // need to lock buffer for read since we only have one msg data structure and PassThruReadMsgs probably not thread safe
            lock (mBuffer)
            {
                // try to read data from serial
                rmsg.ProtocolID = (uint)j2534.eProtocol1.ISO9141;
                rmsg.RxStatus = 0;
                rmsg.TxFlags = 0;
                rmsg.Timestamp = 0;
                rmsg.ExtraDataIndex = 0;
                rmsg.DataSize = 0;

                uint numMsgs = 1;
                Marshal.StructureToPtr(rmsg, rpMsg, false);
                // FIXME: why does it buffer 0xFE0 worth of data?
                var ret = (j2534.eError1)j2534.PassThruReadMsgs(mChannelId, rpMsg, ref numMsgs, 0);
                Marshal.PtrToStructure(rpMsg, rmsg);

                try
                {
                    if (ret == j2534.eError1.ERR_SUCCESS && rmsg.DataSize > 0 && numMsgs > 0) mBuffer.Enqueue(rmsg.Data, (int)rmsg.DataSize);
                }
                catch (Exception x)
                {
                    MessageBox.Show("read: " + x.ToString());
                }
            }
        }

        void HandleDequeue(object sender, EventArgs e)
        {
            Byte[] buffer = null;
            lock (mBuffer)
            {
                int size = 0;
                if (mLogMsgInfo[mLogMsgType].RespSize <= mBuffer.Length) size = mLogMsgInfo[mLogMsgType].RespSize;
                if (size * 2 <= mBuffer.Length) size *= 2;
                buffer = mBuffer.Dequeue(size);
            }

            if (buffer != null && buffer[0x00] == mLogMsgInfo[mLogMsgType].RespHeader && buffer[mLogMsgInfo[mLogMsgType].RespSize - 1] == TRAILER_VALUE)
            {
                int index = 1;
                foreach (var elemInfo in mLogMsgInfo[mLogMsgType].Elem)
                {
                    for (int i = 0; i < elemInfo.Item1; i++, index += elemInfo.Item2) mData[i] = (uint)((elemInfo.Item2 == 1 ? 0 : (buffer[index + 1] << 8)) | (buffer[index] << 0));
                }

                this.BeginInvoke(new Action(() => RefreshParams()));
            }
        }

        void RefreshParams()
        {
            if (Monitor.TryEnter(dataGridViewTable))
            {
                try
                {
                    // update param and charts
                    int count = 0;

                    foreach (DataGridViewRow row in dataGridViewParam.Rows)
                    {
                        var paramName = (row.Cells[0] as DataGridViewComboBoxCell).Value?.ToString();

                        if (paramName != null && paramName != "")
                        {
                            var data = Convert.ToDouble(mDataTable.Compute(mParamInfo[paramName].mEvalExpr.Replace("x", mData[count].ToString()), String.Empty));
                            (row.Cells[2] as DataGridViewTextBoxCell).Value = data;

                            if (count < mCharts.Length)
                            {
                                mCharts[count].Series[0].Points.Add(data);
                                if (mCharts[count].Series[0].Points.Count > 20) mCharts[count].Series[0].Points.RemoveAt(0);

                                if (data < mCharts[count].ChartAreas[0].AxisY.Minimum) mCharts[count].ChartAreas[0].AxisY.Minimum = data;
                                if (data > mCharts[count].ChartAreas[0].AxisY.Maximum) mCharts[count].ChartAreas[0].AxisY.Maximum = data;
                                mCharts[count].Update();
                            }
                        }

                        count++;
                    }

                    RefreshTable();
                }
                finally
                {
                    Monitor.Exit(dataGridViewTable);
                }
            }
        }

        private void RefreshTable()
        {
            dataGridViewTable.ClearSelection();
            if (mParamIndex.ContainsKey("Engine Load") && mParamIndex.ContainsKey("RPM") && dataGridViewTable.Rows.Count > 0 && dataGridViewTable.Columns.Count > 0)
            {
                var load = Convert.ToDouble(mData[mParamIndex["Engine Load"]]);
                var rpm = Convert.ToDouble(mData[mParamIndex["RPM"]]);

                double prevLoad = 0;
                int loadIndex = -1;
                bool loadLeft = false;
                bool loadRight = false;
                foreach (DataGridViewColumn col in dataGridViewTable.Columns)
                {
                    var currLoad = Convert.ToDouble(col.HeaderCell.Value);

                    if (load < currLoad)
                    {
                        // left of current analyzed cell
                        var left = Math.Abs(load - prevLoad);
                        var right = Math.Abs(currLoad - load);

                        if (left < right)
                        {
                            loadLeft = left / (currLoad - prevLoad) < 0.8;
                            loadRight = right / (currLoad - prevLoad) < 0.8;
                            break;
                        }
                    }

                    prevLoad = currLoad;
                    loadIndex++;
                }
                loadIndex = Math.Max(0, loadIndex);

                double prevRpm = 0;
                int rpmIndex = -1;
                bool rpmLeft = false;
                bool rpmRight = false;
                foreach (DataGridViewRow row in dataGridViewTable.Rows)
                {
                    var currRpm = Convert.ToDouble(row.HeaderCell.Value);

                    if (rpm < currRpm)
                    {
                        // left of current analyzed cell
                        var left = Math.Abs(rpm - prevRpm);
                        var right = Math.Abs(currRpm - rpm);

                        if (left < right)
                        {
                            // closer to previous cell than current cell
                            rpmLeft = left / (currRpm - prevRpm) < 0.8;
                            rpmRight = right / (currRpm - prevRpm) < 0.8;
                            break;
                        }
                    }

                    prevRpm = currRpm;
                    rpmIndex++;
                }
                rpmIndex = Math.Max(0, rpmIndex);

                if (loadLeft && rpmLeft) dataGridViewTable[loadIndex, rpmIndex].Selected = true;
                if (loadRight && rpmLeft) dataGridViewTable[loadIndex + 1, rpmIndex].Selected = true;
                if (loadLeft && rpmRight) dataGridViewTable[loadIndex, rpmIndex + 1].Selected = true;
                if (loadRight && rpmRight) dataGridViewTable[loadIndex + 1, rpmIndex + 1].Selected = true;
            }
        }

        private void buttonLoadParam_Click(object sender, EventArgs e)
        {
            OpenFileDialog fd = new OpenFileDialog { Filter = "xml files (*.xml)|*.xml|all files (*.*)|*.*", Title = "Load EvoScan Param XML", DefaultExt = "xml" };

            if (fd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    mParamXml = new XmlDocument();
                    mParamXml.Load(fd.FileName);

                    LoadECU();
                    buttonConnect.Enabled = true;
                }
                catch (Exception x)
                {
                    mParamXml = null;
                    buttonConnect.Enabled = false;
                    MessageBox.Show("error: paramFileName " + fd.FileName + " " + x.ToString());
                }
            }
        }

        private void LoadECU()
        {
            var ecuNodes = mParamXml.SelectNodes("/EvoScanDataLogger/vehicle/ecu");

            comboBox1.Items.Clear();
            foreach (XmlNode ecuNode in ecuNodes)
            {
                if (ecuNode.Attributes["name"].InnerText != "EFI") continue;

                comboBox1.Items.Add(ecuNode.Attributes["name"].InnerText);
            }

            foreach (var tableItem in comboBox1.Items)
            {
                var width = Convert.ToInt32(comboBox1.CreateGraphics().MeasureString(tableItem.ToString(), comboBox1.Font).Width);
                comboBox1.DropDownWidth = Math.Max(comboBox1.DropDownWidth, width);
            }
            if (comboBox1.Items.Count > 0) comboBox1.SelectedIndex = 0;
            LoadParams();
        }

        private void LoadParams()
        {
            var ecuNodes = mParamXml.SelectNodes("/EvoScanDataLogger/vehicle/ecu");

            foreach (XmlNode ecuNode in ecuNodes)
            {
                if (ecuNode.Attributes["name"].InnerText == comboBox1.SelectedItem.ToString())
                {
                    XmlNode modeNode = ecuNode.SelectSingleNode("Mode2");

                    mParamInfo.Clear();
                    mParamIndex.Clear();
                    List<string> values = new List<string>();
                    values.Add("");

                    foreach (XmlNode paramNode in modeNode)
                    {

                        // test eval
                        try
                        {
                            var logName = paramNode.Attributes["LogReference"].InnerText;
                            var requestId = paramNode.Attributes["RequestID"].InnerText;
                            var evalExpr = paramNode.Attributes["Eval"].InnerText;

                            // TODO: handle indirection
                            Convert.ToUInt16(requestId, 16);
                            Convert.ToDouble(mDataTable.Compute(evalExpr.Replace("x", 1.ToString()), String.Empty));
                            // add elements
                            values.Add(logName);

                            mParamInfo.Add(paramNode.Attributes["LogReference"].InnerText, new sParamInfo
                            {
                                mDisplayName = paramNode.Attributes["Display"].InnerText,
                                mLogName = paramNode.Attributes["LogReference"].InnerText,
                                mRequestId = paramNode.Attributes["RequestID"].InnerText,
                                mUnitName = paramNode.Attributes["Unit"].InnerText,
                                mEvalExpr = paramNode.Attributes["Eval"].InnerText,
                                mChartMin = Convert.ToDouble(paramNode.Attributes["ChartMin"].InnerText),
                                mChartMax = Convert.ToDouble(paramNode.Attributes["ChartMax"].InnerText),
                            });

                        }
                        catch
                        {
                            // do nothing
                        }
                    }

                    int count = 0;
                    foreach (DataGridViewRow row in dataGridViewParam.Rows)
                    {
                        if (count < mParamInfo.Count)
                        {
                            var dgc = (row.Cells[0] as DataGridViewComboBoxCell);
                            dgc.DataSource = values;
                            var paramName = values[count++ + 1];
                            dgc.Value = paramName;
                            LoadParam(row, paramName);
                        }
                    }

                    int maxWidth = 0;
                    foreach (var paramInfo in mParamInfo.Keys) maxWidth = Math.Max(maxWidth, Convert.ToInt32(dataGridViewParam.CreateGraphics().MeasureString(paramInfo, dataGridViewParam.Font).Width));
                    maxWidth += 16;
                    foreach (DataGridViewRow row in dataGridViewParam.Rows) (row.Cells[0] as DataGridViewComboBoxCell).DropDownWidth = maxWidth;

                    foreach (var tableItem in comboBox1.Items)
                    {
                        var width = Convert.ToInt32(comboBox1.CreateGraphics().MeasureString(tableItem.ToString(), comboBox1.Font).Width);
                        comboBox1.DropDownWidth = Math.Max(comboBox1.DropDownWidth, width);
                    }
                }
            }
        }

        private void LoadParam(DataGridViewRow row, string paramName)
        {
            paramName = paramName == null ? "" : paramName;
            (row.Cells[1] as DataGridViewTextBoxCell).Value = paramName == "" ? "" : mParamInfo[paramName].mRequestId;
            (row.Cells[2] as DataGridViewTextBoxCell).Value = "";
            (row.Cells[3] as DataGridViewTextBoxCell).Value = paramName == "" ? "" : mParamInfo[paramName].mUnitName;
            (row.Cells[4] as DataGridViewTextBoxCell).Value = paramName == "" ? "" : mParamInfo[paramName].mEvalExpr;

            if (row.Index < mCharts.Length)
            {
                mCharts[row.Index].Titles[0].Text = paramName == "" ? "" : mParamInfo[paramName].mDisplayName;
                mCharts[row.Index].ChartAreas[0].AxisY.Minimum = paramName == "" ? 0 : mParamInfo[paramName].mChartMin;
                mCharts[row.Index].ChartAreas[0].AxisY.Maximum = paramName == "" ? 100 : mParamInfo[paramName].mChartMax;
                mCharts[row.Index].Update();
            }

            // redo the indexing.  this is done in order to enforce the top to bottom priority and simplify multiple params
            // matching the key words
            mParamIndex.Clear();

            foreach (DataGridViewRow irow in dataGridViewParam.Rows)
            {
                var iparamName = (irow.Cells[0] as DataGridViewComboBoxCell).Value?.ToString();
                if (iparamName == "ECULoad" && !mParamIndex.ContainsKey("Engine Load")) mParamIndex["Engine Load"] = irow.Index;
                else if (iparamName == "Load1B" && !mParamIndex.ContainsKey("Engine Load")) mParamIndex["Engine Load"] = irow.Index;
                else if (iparamName == "RPM" && !mParamIndex.ContainsKey("RPM")) mParamIndex["RPM"] = irow.Index;
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadParams();
        }

        private void dataGridViewParam_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            var ec = dataGridViewParam.EditingControl as DataGridViewComboBoxEditingControl;
            if (ec != null) ec.DroppedDown = true;
        }

        private void dataGridViewParam_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (dataGridViewParam.CurrentCell.ColumnIndex == 0 && e.Control is ComboBox)
            {
                var cb = e.Control as ComboBox;
                cb.SelectedIndexChanged -= LoadParamEvent;
                cb.SelectedIndexChanged += LoadParamEvent;
            }
        }

        private void LoadParamEvent(object sender, EventArgs e)
        {
            var cell = dataGridViewParam.CurrentCellAddress;
            var cb = sender as DataGridViewComboBoxEditingControl;
            LoadParam(dataGridViewParam.Rows[cell.Y], cb.EditingControlFormattedValue?.ToString());
        }

        private void buttonLoadROM_Click(object sender, EventArgs e)
        {
            OpenFileDialog fd = new OpenFileDialog { Filter = "rom files (*.bin,*.hex,*.srf)|*.bin;*.hex;*.srf|all files (*.*)|*.*", Title = "Load ROM"  };

            if (fd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    mROMFile = System.IO.File.ReadAllBytes(fd.FileName);

                    // drop SRF format header
                    int headerSize = mROMFile.Length % 0x1000;
                    if (System.IO.Path.GetExtension(fd.FileName).ToLower() == ".srf" && headerSize != 0)
                    {
                        Buffer.BlockCopy(mROMFile, headerSize, mROMFile, 0, mROMFile.Length - headerSize);
                        mROMFile = mROMFile.Take(mROMFile.Length - headerSize).ToArray();
                    }

                    // read out ROM ID from file
                    uint romId = ((uint)mROMFile[0xF52] << 24) | ((uint)mROMFile[0xF53] << 16) | ((uint)mROMFile[0xF54] << 8) | ((uint)mROMFile[0xF55] << 0);
                    string romIdName = Convert.ToString(romId, 16);

                    LoadMetadataDirectory(System.IO.Path.GetDirectoryName(fd.FileName) + "/metadata");

                    LoadMetadata(romIdName);

                    LoadTables();
                }
                catch (Exception x)
                {
                    MessageBox.Show("error: ROM " + fd.FileName + " " + x.ToString());

                    mMetadataXml = null;
                    mROMFile = null;
                }
            }
        }

        private void LoadMetadataDirectory(string dirName)
        {
            mMetadataInfo.Clear();

            // load all XML files, get their ROMID to XmlDocument mapping (TODO: move this to a general Metadata directory)
            foreach (var fileName in System.IO.Directory.GetFiles(dirName))
            {
                if (System.IO.Path.GetExtension(fileName) != ".xml") continue;

                try
                {
                    XmlDocument doc = new XmlDocument();
                    doc.Load(fileName);
                    mMetadataInfo[doc.SelectSingleNode("/rom/romid/xmlid").InnerText] = doc;
                }
                catch
                {

                }
            }
        }

        private void LoadMetadata(string romIdName)
        {
            mMetadataXml = new XmlDocument();

            // read and merge the XML files
            Queue<string> workQueue = new Queue<string>();
            Dictionary<string, XmlNode> existingNodes = new Dictionary<string, XmlNode>();

            workQueue.Enqueue(romIdName);
            while (workQueue.Count > 0)
            {
                // load the doc
                var docName = workQueue.Dequeue();
                var doc = mMetadataInfo[docName];

                if (mMetadataXml.SelectNodes("/rom").Count == 0) mMetadataXml.AppendChild(mMetadataXml.ImportNode(doc.SelectSingleNode("/rom"), false));

                // iterate through the key nodes
                foreach (var nodeName in new string[] { "scaling", "table" })
                {
                    foreach (XmlNode node in doc.SelectNodes("/rom/" + nodeName))
                    {
                        string nodePath = nodeName + "/" + node.Attributes["name"].InnerText;
                        if (existingNodes.ContainsKey(nodePath))
                        {
                            Queue<Tuple<XmlNode, XmlNode>> nodeWorkQueue = new Queue<Tuple<XmlNode, XmlNode>>();

                            nodeWorkQueue.Enqueue(Tuple.Create(existingNodes[nodePath], node));

                            while (nodeWorkQueue.Count > 0)
                            {
                                var pair = nodeWorkQueue.Dequeue();
                                XmlNode existingNode = pair.Item1;
                                XmlNode neighborNode = pair.Item2;

                                // add missing attributes
                                if (neighborNode.Attributes != null)
                                {
                                    foreach (XmlAttribute attribute in neighborNode.Attributes)
                                    {
                                        var a = existingNode.Attributes.GetNamedItem(attribute.Name);
                                        if (a == null) existingNode.Attributes.Append((XmlAttribute)mMetadataXml.ImportNode(attribute, true));
                                    }
                                }

                                if (neighborNode.ChildNodes.Count > 0)
                                {
                                    if (existingNode.ChildNodes.Count == 0)
                                    {
                                        for (int i = 0; i < neighborNode.ChildNodes.Count; i++) existingNode.AppendChild(mMetadataXml.ImportNode(neighborNode, true));
                                    }
                                    else if (existingNode.ChildNodes.Count == neighborNode.ChildNodes.Count)
                                    {
                                        // FIXME: need to walk across children
                                        for (int i = 0; i < existingNode.ChildNodes.Count; i++) nodeWorkQueue.Enqueue(Tuple.Create(existingNode.ChildNodes[i], neighborNode.ChildNodes[i]));
                                    }
                                }
                            }
                        }
                        else
                        {
                            // copy node
                            existingNodes[nodePath] = mMetadataXml.ImportNode(node, true);
                            mMetadataXml.SelectSingleNode("/rom").AppendChild(existingNodes[nodePath]);
                        }
                    }

                }

                foreach (XmlNode node in doc.SelectNodes("/rom/include")) workQueue.Enqueue(node.InnerText);
            }
        }

        private void LoadTables()
        {
            comboBox2.Items.Clear();
            foreach (XmlNode tableNode in mMetadataXml.SelectNodes("/rom/table"))
            {
                if (   tableNode.ChildNodes.Count == 2
                    && tableNode.ChildNodes[0].Attributes["name"].InnerText.Contains("Load")
                    && tableNode.ChildNodes[1].Attributes["name"].InnerText.Contains("RPM")
                    )
                {
                    comboBox2.Items.Add(tableNode.Attributes["name"].InnerText);
                }
            }

            foreach (var tableItem in comboBox2.Items) {
                var width = Convert.ToInt32(comboBox2.CreateGraphics().MeasureString(tableItem.ToString(), comboBox2.Font).Width);
                comboBox2.DropDownWidth = Math.Max(comboBox2.DropDownWidth, width);
            }

            comboBox2.SelectedIndex = 0;
            LoadTable();
        }

        private void LoadTable()
        {
            lock (dataGridViewTable)
            {
                if (comboBox2.Items.Count > 0)
                {
                    var tableName = comboBox2.SelectedItem.ToString();

                    foreach (XmlNode tableNode in mMetadataXml.SelectNodes("/rom/table"))
                    {
                        if (tableNode.Attributes["name"].InnerText == tableName)
                        {
                            UInt16 min, max;

                            dataGridViewTable.Rows.Clear();
                            dataGridViewTable.Columns.Clear();

                            // TODO: support 1D, 2D, 3D tables
                            string category;
                            int tableAddress, loadAddress, loadElem, rpmAddress, rpmElem;
                            try
                            {
                                category = tableNode.Attributes["category"].InnerText;
                                tableAddress = Convert.ToInt32(tableNode.Attributes["address"].InnerText, 16);
                                loadAddress = Convert.ToInt32(tableNode.ChildNodes[0].Attributes["address"].InnerText, 16);
                                loadElem = Convert.ToInt32(tableNode.ChildNodes[0].Attributes["elements"].InnerText);
                                rpmAddress = Convert.ToInt32(tableNode.ChildNodes[1].Attributes["address"].InnerText, 16);
                                rpmElem = Convert.ToInt32(tableNode.ChildNodes[1].Attributes["elements"].InnerText);
                            }
                            catch
                            {
                                continue;
                            }

                            // create columns
                            min = UInt16.MaxValue;
                            max = 0;
                            for (int i = 0; i < loadElem; i++)
                            {
                                var value = Convert.ToUInt16((((UInt16)mROMFile[loadAddress + i * 2 + 0] << 8) | ((UInt16)mROMFile[loadAddress + i * 2 + 1] << 0)) * 10 / 32);
                                dataGridViewTable.Columns.Add(value.ToString(), value.ToString());
                                dataGridViewTable.Columns[i].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                                dataGridViewTable.Columns[i].HeaderCell.Style.SelectionBackColor = Color.Yellow;
                                dataGridViewTable.Columns[i].SortMode = DataGridViewColumnSortMode.NotSortable;

                                min = Math.Min(min, value);
                                max = Math.Max(max, value);
                            }

                            // color cells
                            foreach (DataGridViewColumn col in dataGridViewTable.Columns)
                            {
                                var value = Convert.ToInt32((Convert.ToDouble(col.HeaderCell.Value) - (double)min) / ((double)max - (double)min) * 255.0);
                                col.HeaderCell.Style.BackColor = Color.FromArgb(value, 96, 255 - value);
                            }

                            // create rows
                            min = UInt16.MaxValue;
                            max = 0;
                            for (int i = 0; i < rpmElem; i++)
                            {
                                var value = Convert.ToUInt16((((UInt16)mROMFile[rpmAddress + i * 2 + 0] << 8) | ((UInt16)mROMFile[rpmAddress + i * 2 + 1] << 0)) * 1000 / 256);
                                dataGridViewTable.Rows.Add();
                                dataGridViewTable.Rows[i].HeaderCell.Value = value.ToString();
                                dataGridViewTable.Rows[i].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                                dataGridViewTable.Rows[i].HeaderCell.Style.SelectionBackColor = Color.Yellow;

                                min = Math.Min(min, value);
                                max = Math.Max(max, value);
                            }
                            dataGridViewTable.AutoResizeRowHeadersWidth(DataGridViewRowHeadersWidthSizeMode.AutoSizeToAllHeaders);

                            // color cells
                            foreach (DataGridViewRow row in dataGridViewTable.Rows)
                            {
                                var value = Convert.ToInt32((Convert.ToDouble(row.HeaderCell.Value) - (double)min) / ((double)max - (double)min) * 255.0);
                                row.HeaderCell.Style.BackColor = Color.FromArgb(value, 96, 255 - value);
                            }

                            // add values
                            double dmin = Double.MaxValue;
                            double dmax = 0;
                            for (int r = 0; r < dataGridViewTable.Rows.Count; r++)
                            {
                                for (int c = 0; c < dataGridViewTable.Columns.Count; c++)
                                {
                                    var raw = tableNode.Attributes["category"].InnerText == "Fuel" ? (int)(Byte)mROMFile[tableAddress + c * dataGridViewTable.Rows.Count + r] : (int)(SByte)mROMFile[tableAddress + c * dataGridViewTable.Rows.Count + r];
                                    var value = tableNode.Attributes["category"].InnerText == "Fuel" ? Convert.ToDouble(String.Format("{0:0.0}", (14.7 * 128.0 / Convert.ToDouble(raw)))) : Convert.ToDouble(raw);

                                    dataGridViewTable[c, r].Value = value;

                                    dmin = Math.Min(dmin, value);
                                    dmax = Math.Max(dmax, value);
                                }
                            }

                            // formatting
                            for (int r = 0; r < dataGridViewTable.Rows.Count; r++)
                            {
                                for (int c = 0; c < dataGridViewTable.Columns.Count; c++)
                                {
                                    var value = dmax == 0.0 ? 0 : Convert.ToInt32((Convert.ToDouble(dataGridViewTable[c, r].Value) - dmin) / (dmax - dmin) * 255.0);
                                    dataGridViewTable[c, r].Style.BackColor = Color.FromArgb(value, 96, 255 - value);
                                }
                            }

                            int colWidth = (dataGridViewTable.Parent.Width - dataGridViewTable.RowHeadersWidth) / loadElem;
                            foreach (DataGridViewColumn col in dataGridViewTable.Columns) col.Width = colWidth;
                            int delta = dataGridViewTable.Parent.Width - (colWidth * loadElem + dataGridViewTable.RowHeadersWidth);
                            if (delta > 0 && delta < 32) dataGridViewTable.RowHeadersWidth += delta;

                            break;
                        }
                    }
                }
            }
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadTable();
        }

        private void dataGridViewTable_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.ColumnIndex == -1 && e.RowIndex > -1)
            {
                e.PaintBackground(e.CellBounds, false);

                Point p = new Point(0, 0);
                p.Offset(e.CellBounds.Location);
                p.Offset(e.CellBounds.Width, e.CellBounds.Height / 2);
                TextRenderer.DrawText(e.Graphics, e.Value.ToString(), e.CellStyle.Font, p, e.CellStyle.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);

                e.Handled = true;
            }
        }
    }
}
