﻿// This file is or was originally a part of the Impressions Resolution Customiser project, which can be found here:
// https://github.com/XJDHDR/impressions-resolution-customiser
//
// The license for it may be found here:
// https://github.com/XJDHDR/impressions-resolution-customiser/blob/main/LICENSE
//

namespace Emperor.non_UI_code
{
	/// <summary>
	/// Holds code used to fix a quirk that prevents the game from being set to run in windowed mode.
	/// </summary>
	internal static class EmperorWindowFix
	{
		/// <summary>
		/// Gets the offset that needs to be patched to fix the windowed mode quirk then patches it.
		/// </summary>
		/// <param name="_ExeAttributes_">Struct that specifies various details about the detected Emperor.exe</param>
		/// <param name="_EmperorExeData_">Byte array that contains the binary data contained within the supplied Emperor.exe</param>
		internal static void _hexEditWindowFix(ExeAttributes _ExeAttributes_, ref byte[] _EmperorExeData_)
		{
			// At this address, the original code had a conditional jump (jl) that activates if the value stored in the EAX register is less than the value stored in the ECX.
			// This patch changes this byte into an unconditional jump.
			// I have no idea what the values represent, what code runs if the condition is false (EAX is greater than ECX) or why the widescreen mods cause
			// EAX to be greater than ECX. All I know is that it makes Windowed mode work.
			if (EmperorExeDefinitions._IdentifyWinFixOffset(_ExeAttributes_, out int _winFixOffset_))
			{
				_EmperorExeData_[_winFixOffset_] = 0xEB;
			}
		}
	}
}
