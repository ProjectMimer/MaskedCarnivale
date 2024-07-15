#pragma once
#undef UNICODE
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <iostream>

const DWORD sharedBufferSize = 1024; //4096 * 2048 * 4 * sizeof(char);

int CreateSharedMemory(HANDLE *hMapFile, unsigned char **sharedBuffer, int bufferSize, std::string bufferName);
int OpenSharedMemory(HANDLE *hMapFile, unsigned char **sharedBuffer, int bufferSize, std::string bufferName);
void CloseSharedMemory(HANDLE *hMapFile, unsigned char **sharedBuffer);
int MapSharedMemory(HANDLE *hMapFile, unsigned char **sharedBuffer, int bufferSize);

bool CreateExtraMemory(HANDLE *bufferHandle, DWORD *bufferLocation);
void CloseExtraMemory(HANDLE *bufferHandle, DWORD *bufferLocation);
