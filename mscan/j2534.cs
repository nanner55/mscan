using System;

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace mscan
{
    class j2534
    {
        [DllImport("j2534.dll", CallingConvention = CallingConvention.Cdecl)] public static extern bool init();
        [DllImport("j2534.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void setDllName(String name);
        [DllImport("j2534.dll", CallingConvention = CallingConvention.Cdecl)] public static extern bool valid();
        [DllImport("j2534.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void debug(bool enable);
        [DllImport("j2534.dll", CallingConvention = CallingConvention.Cdecl)] public static extern String getLastError();

        [DllImport("j2534.dll", CallingConvention = CallingConvention.Cdecl)] public static extern Int32 PassThruOpen(IntPtr pName, ref UInt32 pDeviceID);
        [DllImport("j2534.dll", CallingConvention = CallingConvention.Cdecl)] public static extern Int32 PassThruClose(UInt32 DeviceID);
        [DllImport("j2534.dll", CallingConvention = CallingConvention.Cdecl)] public static extern Int32 PassThruConnect(UInt32 DeviceID, UInt32 ProtocolID, UInt32 Flags, UInt32 Baudrate, ref UInt32 pChannelID);
        [DllImport("j2534.dll", CallingConvention = CallingConvention.Cdecl)] public static extern Int32 PassThruDisconnect(UInt32 ChannelID);
        [DllImport("j2534.dll", CallingConvention = CallingConvention.Cdecl)] public static extern Int32 PassThruReadMsgs(UInt32 ChannelID, IntPtr pMsg, ref UInt32 pNumMsgs, UInt32 Timeout);
        [DllImport("j2534.dll", CallingConvention = CallingConvention.Cdecl)] public static extern Int32 PassThruWriteMsgs(UInt32 ChannelID, IntPtr pMsg, ref UInt32 pNumMsgs, UInt32 Timeout);
        [DllImport("j2534.dll", CallingConvention = CallingConvention.Cdecl)] public static extern Int32 PassThruStartPeriodicMsg(UInt32 ChannelID, IntPtr pMsg, ref UInt32 pMsgID, UInt32 TimeInterval);
        [DllImport("j2534.dll", CallingConvention = CallingConvention.Cdecl)] public static extern Int32 PassThruStopPeriodicMsg(UInt32 ChannelID, UInt32 MsgID);
        [DllImport("j2534.dll", CallingConvention = CallingConvention.Cdecl)] public static extern Int32 PassThruStartMsgFilter(UInt32 ChannelID, UInt32 FilterType, IntPtr pMaskMsg, IntPtr pPatternMsg, IntPtr pFlowControlMsg, ref UInt32 pMsgID);
        [DllImport("j2534.dll", CallingConvention = CallingConvention.Cdecl)] public static extern Int32 PassThruStopMsgFilter(UInt32 ChannelID, UInt32 MsgID);
        [DllImport("j2534.dll", CallingConvention = CallingConvention.Cdecl)] public static extern Int32 PassThruSetProgrammingVoltage(UInt32 DeviceID, UInt32 Pin, UInt32 Voltage);
        [DllImport("j2534.dll", CallingConvention = CallingConvention.Cdecl)] public static extern Int32 PassThruReadVersion(Byte[] pApiVersion, Byte[] pDllVersion, Byte[] pFirmwareVersion, UInt32 DeviceID);
        [DllImport("j2534.dll", CallingConvention = CallingConvention.Cdecl)] public static extern Int32 PassThruGetLastError(Byte[] pErrorDescription);
        [DllImport("j2534.dll", CallingConvention = CallingConvention.Cdecl)] public static extern Int32 PassThruIoctl(UInt32 ChannelID, UInt32 IoctlID, IntPtr pInput, IntPtr pOutput);
        [DllImport("j2534.dll", CallingConvention = CallingConvention.Cdecl)] public static extern Int32 PassThru5BaudInit(UInt32 ChannelID, Byte InitID);

        ////////////////
        // Protocol IDs
        ////////////////

        // J2534-1
        public enum eProtocol1 : uint
        {
            J1850VPW = 1,
            J1850PWM = 2,
            ISO9141 = 3,
            ISO14230 = 4,
            CAN = 5,
            ISO15765 = 6,
            SCI_A_ENGINE = 7,   // OP2.0: Not supported
            SCI_A_TRANS = 8,    // OP2.0: Not supported
            SCI_B_ENGINE = 9,   // OP2.0: Not supported
            SCI_B_TRANS = 10,   // OP2.0: Not supported
        };

        // J2534-2
        public enum eProtocol2 : uint
        {
            CAN_CH1 = 0x00009000,
            J1850VPW_CH1 = 0x00009080,
            J1850PWM_CH1 = 0x00009160,
            ISO9141_CH1 = 0x00009240,
            ISO9141_CH2 = 0x00009241,
            ISO9141_CH3 = 0x00009242,
            ISO9141_K = ISO9141_CH1,
            ISO9141_L = ISO9141_CH2,        // OP2.0: Support for ISO9141 communications over the L line
            ISO9141_INNO = ISO9141_CH3,     // OP2.0: Support for RS-232 receive-only communications via the 2.5mm jack
            ISO14230_CH1 = 0x00009320,
            ISO14230_CH2 = 0x00009321,
            ISO14230_K = ISO14230_CH1,
            ISO14230_L = ISO14230_CH2,  // OP2.0: Support for ISO14230 communications over the L line
            ISO15765_CH1 = 0x00009400,
        };

        /////////////
        // IOCTL IDs
        /////////////

        // J2534-1
        public enum eIoctl1 : uint
        {
            GET_CONFIG = 0x01,
            SET_CONFIG = 0x02,
            READ_VBATT = 0x03,
            FIVE_BAUD_INIT = 0x04,
            FAST_INIT = 0x05,
            CLEAR_TX_BUFFER = 0x07,
            CLEAR_RX_BUFFER = 0x08,
            CLEAR_PERIODIC_MSGS = 0x09,
            CLEAR_MSG_FILTERS = 0x0A,
            CLEAR_FUNCT_MSG_LOOKUP_TABLE = 0x0B,    // OP2.0: Not yet supported
            ADD_TO_FUNCT_MSG_LOOKUP_TABLE = 0x0C,   // OP2.0: Not yet supported
            DELETE_FROM_FUNCT_MSG_LOOKUP_TABLE = 0x0D,  // OP2.0: Not yet supported
            READ_PROG_VOLTAGE = 0x0E,   // OP2.0: Several pins are supported
        };

        // J2534-2
        public enum eIoctl2 : uint
        {
            SW_CAN_NS = 0x8000, // OP2.0: Not supported
            SW_CAN_HS = 0x8001, // OP2.0: Not supported
        };

        // Tactrix specific IOCTLs
        public enum eIoctlT : uint
        {
            TX_IOCTL_BASE = 0x70000,
            // OP2.0: The IOCTL below supports application-specific functions
            // that can be built into the hardware
            TX_IOCTL_APP_SERVICE = (TX_IOCTL_BASE + 0),
            TX_IOCTL_SET_DLL_DEBUG_FLAGS = (TX_IOCTL_BASE + 1),
            TX_IOCTL_DLL_DEBUG_FLAG_J2534_CALLS = 0x00000001,
            TX_IOCTL_DLL_DEBUG_FLAG_ALL_DEV_COMMS = 0x00000002,
            TX_IOCTL_SET_DEV_DEBUG_FLAGS = (TX_IOCTL_BASE + 2),
            TX_IOCTL_DEV_DEBUG_FLAG_USB_COMMS = 0x00000001,
            TX_IOCTL_SET_DLL_STATUS_CALLBACK = (TX_IOCTL_BASE + 3),
            TX_IOCTL_GET_DEVICE_INSTANCES = (TX_IOCTL_BASE + 4),
        };

        /////////////////
        // Pin numbering
        /////////////////

        public enum ePin : uint
        {
            AUX_PIN = 0,    // aux jack	OP2.0: Supports GND and adj. voltage
            J1962_PIN_1 = 1,    //			OP2.0: Supports GND and adj. voltage
            J1962_PIN_2 = 2,    // J1850P	OP2.0: Supports 5V and 8V
            J1962_PIN_3 = 3,    //			OP2.0: Supports GND and adj. voltage
            J1962_PIN_4 = 4,    // GND
            J1962_PIN_5 = 5, // GND
            J1962_PIN_6 = 6,    // CAN
            J1962_PIN_7 = 7,    // K		OP2.0: Supports GND
            J1962_PIN_8 = 8,    //			OP2.0: Supports reading voltage
            J1962_PIN_9 = 9,    //			OP2.0: Supports GND and adj. voltage
            J1962_PIN_10 = 10,  // J1850M	OP2.0: Supports GND
            J1962_PIN_11 = 11,  //			OP2.0: Supports GND and adj. voltage
            J1962_PIN_12 = 12,  //			OP2.0: Supports GND and adj. voltage
            J1962_PIN_13 = 13,  //			OP2.0: Supports GND and adj. voltage
            J1962_PIN_14 = 14,  // CAN
            J1962_PIN_15 = 15,  // L		OP2.0: Supports GND
            J1962_PIN_16 = 16,  // VBAT		OP2.0: Supports reading voltage
            PIN_VADJ = 17,  // internal	OP2.0: Supports reading voltage
        };

        ////////////////////////////////
        // Special pin voltage settings
        ////////////////////////////////
        public enum eVolt : uint
        {
            SHORT_TO_GROUND = 0xFFFFFFFE,
            VOLTAGE_OFF = 0xFFFFFFFF,
        };

        /////////////////////////////////////////
        // GET_CONFIG / SET_CONFIG Parameter IDs
        /////////////////////////////////////////

        // J2534-1
        public enum eConfig1 : uint
        {
            DATA_RATE = 0x01,
            LOOPBACK = 0x03,
            NODE_ADDRESS = 0x04, // OP2.0: Not yet supported
            NETWORK_LINE = 0x05, // OP2.0: Not yet supported
            P1_MIN = 0x06, // J2534 says this may not be changed
            P1_MAX = 0x07,
            P2_MIN = 0x08, // J2534 says this may not be changed
            P2_MAX = 0x09, // J2534 says this may not be changed
            P3_MIN = 0x0A,
            P3_MAX = 0x0B, // J2534 says this may not be changed
            P4_MIN = 0x0C,
            P4_MAX = 0x0D, // J2534 says this may not be changed
            W0 = 0x19,
            W1 = 0x0E,
            W2 = 0x0F,
            W3 = 0x10,
            W4 = 0x11,
            W5 = 0x12,
            TIDLE = 0x13,
            TINIL = 0x14,
            TWUP = 0x15,
            PARITY = 0x16,
            BIT_SAMPLE_POINT = 0x17, // OP2.0: Not yet supported
            SYNC_JUMP_WIDTH = 0x18, // OP2.0: Not yet supported
            T1_MAX = 0x1A,
            T2_MAX = 0x1B,
            T3_MAX = 0x24,
            T4_MAX = 0x1C,
            T5_MAX = 0x1D,
            ISO15765_BS = 0x1E,
            ISO15765_STMIN = 0x1F,
            DATA_BITS = 0x20,
            FIVE_BAUD_MOD = 0x21,
            BS_TX = 0x22,
            STMIN_TX = 0x23,
            ISO15765_WFT_MAX = 0x25,
        };

        // J2534-2
        public enum eConfig2 : uint
        {
            CAN_MIXED_FORMAT = 0x8000,
            J1962_PINS = 0x8001, // OP2.0: Not supported
            SW_CAN_HS_DATA_RATE = 0x8010, // OP2.0: Not supported
            SW_CAN_SPEEDCHANGE_ENABLE = 0x8011, // OP2.0: Not supported
            SW_CAN_RES_SWITCH = 0x8012, // OP2.0: Not supported
            ACTIVE_CHANNELS = 0x8020, // OP2.0: Not supported
            SAMPLE_RATE = 0x8021, // OP2.0: Not supported
            SAMPLES_PER_READING = 0x8022, // OP2.0: Not supported
            READINGS_PER_MSG = 0x8023, // OP2.0: Not supported
            AVERAGING_METHOD = 0x8024, // OP2.0: Not supported
            SAMPLE_RESOLUTION = 0x8025, // OP2.0: Not supported
            INPUT_RANGE_LOW = 0x8026, // OP2.0: Not supported
            INPUT_RANGE_HIGH = 0x8027, // OP2.0: Not supported
        };

        // Tactrix specific parameter IDs
        public enum eConfigT : uint
        {
            TX_PARAM_BASE = 0x9000,
            TX_PARAM_STOP_BITS = (TX_PARAM_BASE + 0),
        };

        //////////////////////
        // PARITY definitions
        //////////////////////

        public enum eParity : uint
        {
            NO_PARITY = 0,
            ODD_PARITY = 1,
            EVEN_PARITY = 2,
        };

        ////////////////////////////////
        // CAN_MIXED_FORMAT definitions
        ////////////////////////////////

        public enum eCanMixed : uint
        {
            CAN_MIXED_FORMAT_OFF = 0,
            CAN_MIXED_FORMAT_ON = 1,
            CAN_MIXED_FORMAT_ALL_FRAMES = 2,
        };

        /////////////
        // Error IDs
        /////////////

        // J2534-1
        public enum eError1 : uint
        {
            ERR_SUCCESS = 0x00,
            STATUS_NOERROR = 0x00,
            ERR_NOT_SUPPORTED = 0x01,
            ERR_INVALID_CHANNEL_ID = 0x02,
            ERR_INVALID_PROTOCOL_ID = 0x03,
            ERR_NULL_PARAMETER = 0x04,
            ERR_INVALID_IOCTL_VALUE = 0x05,
            ERR_INVALID_FLAGS = 0x06,
            ERR_FAILED = 0x07,
            ERR_DEVICE_NOT_CONNECTED = 0x08,
            ERR_TIMEOUT = 0x09,
            ERR_INVALID_MSG = 0x0A,
            ERR_INVALID_TIME_INTERVAL = 0x0B,
            ERR_EXCEEDED_LIMIT = 0x0C,
            ERR_INVALID_MSG_ID = 0x0D,
            ERR_DEVICE_IN_USE = 0x0E,
            ERR_INVALID_IOCTL_ID = 0x0F,
            ERR_BUFFER_EMPTY = 0x10,
            ERR_BUFFER_FULL = 0x11,
            ERR_BUFFER_OVERFLOW = 0x12,
            ERR_PIN_INVALID = 0x13,
            ERR_CHANNEL_IN_USE = 0x14,
            ERR_MSG_PROTOCOL_ID = 0x15,
            ERR_INVALID_FILTER_ID = 0x16,
            ERR_NO_FLOW_CONTROL = 0x17,
            ERR_NOT_UNIQUE = 0x18,
            ERR_INVALID_BAUDRATE = 0x19,
            ERR_INVALID_DEVICE_ID = 0x1A,
        };

        // OP2.0 Tactrix specific
        public enum eErrorT : uint
        {
            ERR_OEM_VOLTAGE_TOO_LOW = 0x78, // OP2.0: the requested output voltage is lower than the OP2.0 capabilities
            ERR_OEM_VOLTAGE_TOO_HIGH = 0x77, // OP2.0: the requested output voltage is higher than the OP2.0 capabilities
        };

        /////////////////////////
        // PassThruConnect flags
        /////////////////////////

        public enum eConnect : uint
        {
            CAN_29BIT_ID = 0x00000100,
            ISO9141_NO_CHECKSUM = 0x00000200,
            CAN_ID_BOTH = 0x00000800,
            ISO9141_K_LINE_ONLY = 0x00001000,
            SNIFF_MODE = 0x10000000, // OP2.0: listens to a bus (e.g. CAN) without acknowledging
        };

        //////////////////
        // RxStatus flags
        //////////////////

        public enum eRxStatus : uint
        {
            TX_MSG_TYPE = 0x00000001,
            START_OF_MESSAGE = 0x00000002,
            ISO15765_FIRST_FRAME = 0x00000002,
            RX_BREAK = 0x00000004,
            TX_DONE = 0x00000008,
            ISO15765_PADDING_ERROR = 0x00000010,
            ISO15765_EXT_ADDR = 0x00000080,
            ISO15765_ADDR_TYPE = 0x00000080,
            //CAN_29BIT_ID			=			0x00000100, // (already defined above)
        };

        //////////////////
        // TxStatus flags
        //////////////////

        public enum eTxStatus : uint
        {
            ISO15765_FRAME_PAD = 0x00000040,
            //ISO15765_ADDR_TYPE	=			0x00000080, // (already defined above)
            //CAN_29BIT_ID	=					0x00000100, // (already defined above)
            WAIT_P3_MIN_ONLY = 0x00000200,
            SW_CAN_HV_TX = 0x00000400, // OP2.0: Not supported
            SCI_MODE = 0x00400000, // OP2.0: Not supported
            SCI_TX_VOLTAGE = 0x00800000, // OP2.0: Not supported
        };

        ////////////////
        // Filter types
        ////////////////

        public enum eFilter : uint
        {
            PASS_FILTER = 0x00000001,
            BLOCK_FILTER = 0x00000002,
            FLOW_CONTROL_FILTER = 0x00000003,
        };

        /////////////////
        // Message struct
        /////////////////
        public const int MAX_MSG_SIZE = 4128;

        [StructLayout(LayoutKind.Sequential)]
        public class PASSTHRU_MSG
        {
            public UInt32 ProtocolID;
            public UInt32 RxStatus;
            public UInt32 TxFlags;
            public UInt32 Timestamp;
            public UInt32 DataSize;
            public UInt32 ExtraDataIndex;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_MSG_SIZE)]
            public Byte[] Data = new byte[MAX_MSG_SIZE];
        };

        ////////////////
        // IOCTL structs
        ////////////////

        [StructLayout(LayoutKind.Sequential)]
        public class SCONFIG
        {
            public uint Parameter;
            public uint Value;
        };

        [StructLayout(LayoutKind.Sequential)]
        public class SCONFIG_LIST
        {
            public uint NumOfParams;
            public IntPtr ConfigPtr;
        };

        [StructLayout(LayoutKind.Sequential)]
        public class SBYTE_ARRAY
        {
            public uint NumOfBytes;
            public IntPtr BytePtr;
        };

    }
}
