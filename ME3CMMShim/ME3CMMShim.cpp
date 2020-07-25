#include <iostream>
#include <Windows.h>
#include <Shlwapi.h>

bool isFileExist(char fileName[]) {
	FILE* fp = fopen(fileName, "r");
	if (fp) {
		fclose(fp);
		return true;
	}
	return errno != ENOENT;
}

int WINAPI wWinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, PWSTR pCmdLine, int nCmdShow)
{
	char selfdir[MAX_PATH] = { 0 };
	GetModuleFileNameA(NULL, selfdir, MAX_PATH);
	PathRemoveFileSpecA(selfdir);
	PathAppendA(selfdir, "ME3TweaksModManager.exe");
	if (isFileExist(selfdir))
	{
		STARTUPINFOA si;
		PROCESS_INFORMATION pi;

		// set the size of the structures
		ZeroMemory(&si, sizeof(si));
		si.cb = sizeof(si);
		ZeroMemory(&pi, sizeof(pi));

		// start the program up
		CreateProcessA
		(
			selfdir,   // the path
			NULL,                // Command line
			NULL,                   // Process handle not inheritable
			NULL,                   // Thread handle not inheritable
			FALSE,                  // Set handle inheritance to FALSE
			CREATE_NEW_CONSOLE,     // Opens file in a separate console
			NULL,           // Use parent's environment block
			NULL,           // Use parent's starting directory 
			&si,            // Pointer to STARTUPINFO structure
			&pi           // Pointer to PROCESS_INFORMATION structure
		);
		return 0;
	}
	return 1;
}

// Run program: Ctrl + F5 or Debug > Start Without Debugging menu
// Debug program: F5 or Debug > Start Debugging menu

// Tips for Getting Started: 
//   1. Use the Solution Explorer window to add/manage files
//   2. Use the Team Explorer window to connect to source control
//   3. Use the Output window to see build output and other messages
//   4. Use the Error List window to view errors
//   5. Go to Project > Add New Item to create new code files, or Project > Add Existing Item to add existing code files to the project
//   6. In the future, to open this project again, go to File > Open > Project and select the .sln file
