#include <filesystem>
#include <iostream>
#include <cstring>
#include <Windows.h>
#include <string>
#include <chrono>
#include <thread>
#include <tlhelp32.h>

void RunProcessWithArgs(LPCSTR lpApplicationName, LPSTR commandLine)
{
	// additional information
	STARTUPINFOA si;
	PROCESS_INFORMATION pi;

	// set the size of the structures
	ZeroMemory(&si, sizeof(si));
	si.cb = sizeof(si);
	ZeroMemory(&pi, sizeof(pi));

	// start the program up
	CreateProcessA
	(
		lpApplicationName,   // the path
		commandLine,                // Command line
		NULL,                   // Process handle not inheritable
		NULL,                   // Thread handle not inheritable
		FALSE,                  // Set handle inheritance to FALSE
		CREATE_NEW_CONSOLE,     // Opens file in a separate console
		NULL,           // Use parent's environment block
		NULL,           // Use parent's starting directory 
		&si,            // Pointer to STARTUPINFO structure
		&pi           // Pointer to PROCESS_INFORMATION structure
	);
	// Close process and thread handles. 
	CloseHandle(pi.hProcess);
	CloseHandle(pi.hThread);
}

void killME3TweaksModManager()
{
	PROCESSENTRY32 entry;
	entry.dwSize = sizeof(PROCESSENTRY32);

	HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, NULL);

	if (Process32First(snapshot, &entry) == TRUE)
	{
		while (Process32Next(snapshot, &entry) == TRUE)
		{
			if (_stricmp(entry.szExeFile, "ME3TweaksModManager.exe") == 0)
			{
				HANDLE hProcess = OpenProcess(PROCESS_ALL_ACCESS, FALSE, entry.th32ProcessID);
				if (hProcess)
				{
					TerminateProcess(hProcess, 0);
					CloseHandle(hProcess);
				}
				// Do stuff..
			}
		}
	}

	CloseHandle(snapshot);
}

int main(int argc, char* argv[])
{
	char* updatingFrom = NULL;
	char* updateDestination = NULL;
	char* updateSource = NULL;
	for (size_t i = 0; i < argc; i++)
	{
		if (i == 0) continue;
		if (strcmp(argv[i], "--update-from") == 0) {
			if (argc >= i + 1) {
				updatingFrom = argv[i + 1];
				i++;
				std::cout << "Updating from " << updatingFrom << '\n';
			}
			else {
				std::cout << "ERROR: No argument for --update-from.";
				return 1;
			}
		}
		else if (strcmp(argv[i], "--update-source-path") == 0) {
			if (argc >= i + 1) {
				updateSource = argv[i + 1];
				i++;
				std::cout << "Update source " << updateSource << '\n';

				if (!std::filesystem::exists(updateSource)) {
					std::cout << "ERROR: --update-source-path file does not exist.";
					return 1;
				}
			}
			else {
				std::cout << "ERROR: No argument for --update-source-path.";
				return 1;
			}
		}
		else if (strcmp(argv[i], "--update-dest-path") == 0) {
			if (argc >= i + 1) {
				updateDestination = argv[i + 1];
				i++;
				std::cout << "Update destination: " << updateDestination << '\n';
			}
			else {
				std::cout << "ERROR: No argument for --update-dest-path.";
				return 1;
			}
		}
		else {
			std::cout << argv[i] << '\n';
		}
	}
	if (!updateSource || !updateDestination) {
		std::cout << "ERROR: --update-source-path and --update-dest-path are required.";
		return 1;
	}

	//Fix for prerelease 104 hangs when shutting down in c#
	killME3TweaksModManager();

	bool updateInstalled = false;
	for (int currentRetry = 0; currentRetry < 10; currentRetry++) {
		try
		{
			std::cout << "Applying update, attempt #" << currentRetry << '\n';
			std::filesystem::copy_file(updateSource, updateDestination, std::filesystem::copy_options::overwrite_existing);
			updateInstalled = true;
			break;
		}
		catch (const std::exception & e)
		{
			std::cout << "Could not copy file to destination: " << e.what() << '\n';
		}
		std::this_thread::sleep_for(std::chrono::seconds(1));
	}
	if (!updateInstalled) {
		std::cout << "ERROR: Unable to copy file to the destination after 10 attempts. We are giving up.";
		return 1;
	}
	//Update OK
	std::string rebootArgs = "--completing-update";
	if (updatingFrom) {
		rebootArgs += " --update-from " + std::string(updatingFrom);
	}

	std::cout << "Booting update: " << updateDestination << " " << rebootArgs;
	LPSTR rArgs = const_cast<char*>(rebootArgs.c_str());
	const char* cUpdateDestination = updateDestination;
	RunProcessWithArgs(cUpdateDestination, rArgs);
	return 0;


	//std::ifstream  src("from.ogv", std::ios::binary);
	//std::ofstream  dst("to.ogv", std::ios::binary);

	//dst << src.rdbuf();
}

