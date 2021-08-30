#pragma once

#include "pch.h"

#include "j2534.h"

extern "C" __declspec(dllexport) bool init();
extern "C" __declspec(dllexport) void setDllName(const char* name);
extern "C" __declspec(dllexport) bool valid();
extern "C" __declspec(dllexport) void debug(bool enable);
extern "C" __declspec(dllexport) char* getLastError();

extern "C" __declspec(dllexport) long PassThruOpen(const void* pName, unsigned long* pDeviceID);
extern "C" __declspec(dllexport) long PassThruClose(unsigned long DeviceID);
extern "C" __declspec(dllexport) long PassThruConnect(unsigned long DeviceID, unsigned long ProtocolID, unsigned long Flags, unsigned long Baudrate, unsigned long* pChannelID);
extern "C" __declspec(dllexport) long PassThruDisconnect(unsigned long ChannelID);
extern "C" __declspec(dllexport) long PassThruReadMsgs(unsigned long ChannelID, PASSTHRU_MSG* pMsg, unsigned long* pNumMsgs, unsigned long Timeout);
extern "C" __declspec(dllexport) long PassThruWriteMsgs(unsigned long ChannelID, const PASSTHRU_MSG* pMsg, unsigned long* pNumMsgs, unsigned long Timeout);
extern "C" __declspec(dllexport) long PassThruStartPeriodicMsg(unsigned long ChannelID, const PASSTHRU_MSG* pMsg, unsigned long* pMsgID, unsigned long TimeInterval);
extern "C" __declspec(dllexport) long PassThruStopPeriodicMsg(unsigned long ChannelID, unsigned long MsgID);
extern "C" __declspec(dllexport) long PassThruStartMsgFilter(unsigned long ChannelID,
	unsigned long FilterType, const PASSTHRU_MSG* pMaskMsg, const PASSTHRU_MSG* pPatternMsg,
	const PASSTHRU_MSG* pFlowControlMsg, unsigned long* pMsgID);
extern "C" __declspec(dllexport) long PassThruStopMsgFilter(unsigned long ChannelID, unsigned long MsgID);
extern "C" __declspec(dllexport) long PassThruSetProgrammingVoltage(unsigned long DeviceID, unsigned long Pin, unsigned long Voltage);
extern "C" __declspec(dllexport) long PassThruReadVersion(char* pApiVersion, char* pDllVersion, char* pFirmwareVersion, unsigned long DeviceID);
extern "C" __declspec(dllexport) long PassThruGetLastError(char* pErrorDescription);
extern "C" __declspec(dllexport) long PassThruIoctl(unsigned long ChannelID, unsigned long IoctlID, const void* pInput, void* pOutput);
extern "C" __declspec(dllexport) long PassThru5BaudInit(unsigned long ChannelID, unsigned char InitID);
