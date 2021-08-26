#include "pch.h"

#include "j2534_wrapper.h"

static J2534 s_j2534;

bool init() { return s_j2534.init(); }
void setDllName(const char* name) { s_j2534.setDllName(name); }
bool valid() { return s_j2534.valid(); }
void debug(bool enable) { s_j2534.debug(enable); }
char* getLastError() { return s_j2534.getLastError(); }

long PassThruOpen(const void* pName, unsigned long* pDeviceID) { return s_j2534.PassThruOpen(pName, pDeviceID); }
long PassThruClose(unsigned long DeviceID) { return s_j2534.PassThruClose(DeviceID); }
long PassThruConnect(unsigned long DeviceID, unsigned long ProtocolID, unsigned long Flags, unsigned long Baudrate, unsigned long* pChannelID) { return s_j2534.PassThruConnect(DeviceID, ProtocolID, Flags, Baudrate, pChannelID); }
long PassThruDisconnect(unsigned long ChannelID) { return s_j2534.PassThruDisconnect(ChannelID); }
long PassThruReadMsgs(unsigned long ChannelID, PASSTHRU_MSG* pMsg, unsigned long* pNumMsgs, unsigned long Timeout) { return s_j2534.PassThruReadMsgs(ChannelID, pMsg, pNumMsgs, Timeout); }
long PassThruWriteMsgs(unsigned long ChannelID, const PASSTHRU_MSG* pMsg, unsigned long* pNumMsgs, unsigned long Timeout) { return s_j2534.PassThruWriteMsgs(ChannelID, pMsg, pNumMsgs, Timeout); }
long PassThruStartPeriodicMsg(unsigned long ChannelID, const PASSTHRU_MSG* pMsg, unsigned long* pMsgID, unsigned long TimeInterval) { return s_j2534.PassThruStartPeriodicMsg(ChannelID, pMsg, pMsgID, TimeInterval); }
long PassThruStopPeriodicMsg(unsigned long ChannelID, unsigned long MsgID) { return s_j2534.PassThruStopPeriodicMsg(ChannelID, MsgID); }
long PassThruStartMsgFilter(unsigned long ChannelID,
	unsigned long FilterType, const PASSTHRU_MSG* pMaskMsg, const PASSTHRU_MSG* pPatternMsg,
	const PASSTHRU_MSG* pFlowControlMsg, unsigned long* pMsgID) { return s_j2534.PassThruStartMsgFilter(ChannelID, FilterType, pMaskMsg, pPatternMsg, pFlowControlMsg, pMsgID); }
long PassThruStopMsgFilter(unsigned long ChannelID, unsigned long MsgID) { return s_j2534.PassThruStopMsgFilter(ChannelID, MsgID); }
long PassThruSetProgrammingVoltage(unsigned long DeviceID, unsigned long Pin, unsigned long Voltage) { return s_j2534.PassThruSetProgrammingVoltage(DeviceID, Pin, Voltage); }
long PassThruReadVersion(char* pApiVersion, char* pDllVersion, char* pFirmwareVersion, unsigned long DeviceID) { return s_j2534.PassThruReadVersion(pApiVersion, pDllVersion, pFirmwareVersion, DeviceID); }
long PassThruGetLastError(char* pErrorDescription) { return s_j2534.PassThruGetLastError(pErrorDescription); }
long PassThruIoctl(unsigned long ChannelID, unsigned long IoctlID, const void* pInput, void* pOutput) { return s_j2534.PassThruIoctl(ChannelID, IoctlID, pInput, pOutput); }
