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

int main()
{
	char selfdir[MAX_PATH] = { 0 };
	GetModuleFileNameA(NULL, selfdir, MAX_PATH);
	PathRemoveFileSpecA(selfdir);
	PathAppendA(selfdir, "ME3TweaksModManager.exe");
	if (isFileExist(selfdir))
	{
		system(selfdir);
	}
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
