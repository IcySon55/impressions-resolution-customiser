﻿// This file is or was originally a part of the Impressions Resolution Customiser project, which can be found here:
// https://github.com/XJDHDR/impressions-resolution-customiser
//
// The license for it may be found here:
// https://github.com/XJDHDR/impressions-resolution-customiser/blob/main/LICENSE
//

using System;
using static Emperor.non_UI_code.EmperorExeDefinitions;

namespace Emperor.non_UI_code
{
	/// <summary>
	/// Holds code used to set the resolution that the game must run at and resize various elements of the game to account for the new resolution.
	/// </summary>
	internal static class EmperorResolutionEdits
	{
		/// <summary>
		/// Patches the various offsets in Emperor.exe to run at the desired resolution and scale various UI elements
		/// to fit the new resolution.
		/// </summary>
		/// <param name="_ResWidth_">The width value of the resolution inputted into the UI.</param>
		/// <param name="_ResHeight_">The height value of the resolution inputted into the UI.</param>
		/// <param name="_ExeAttributes_">Struct that specifies various details about the detected Emperor.exe</param>
		/// <param name="_EmperorExeData_">Byte array that contains the binary data contained within the supplied Emperor.exe</param>
		/// <param name="_ViewportWidth_">The width of the city viewport calculated by the resolution editing code.</param>
		/// <param name="_ViewportHeight_">The height of the city viewport calculated by the resolution editing code.</param>
		internal static void _hexEditExeResVals(ushort _ResWidth_, ushort _ResHeight_, ExeAttributes _ExeAttributes_,
			ref byte[] _EmperorExeData_, out ushort _ViewportWidth_, out ushort _ViewportHeight_)
		{
			_ViewportWidth_ = 0;
			_ViewportHeight_ = 0;
			if (_FillResHexOffsetTable(_ExeAttributes_, out ResHexOffsetTable _resHexOffsetTable_))
			{
				byte[] _resWidthBytes_ = BitConverter.GetBytes(_ResWidth_);
				byte[] _resHeightBytes_ = BitConverter.GetBytes(_ResHeight_);

				// These two offsets set the game's resolution to the desired amount
				_EmperorExeData_[_resHexOffsetTable_._ResWidth + 0] = _resWidthBytes_[0];
				_EmperorExeData_[_resHexOffsetTable_._ResWidth + 1] = _resWidthBytes_[1];
				_EmperorExeData_[_resHexOffsetTable_._ResHeight + 0] = _resHeightBytes_[0];
				_EmperorExeData_[_resHexOffsetTable_._ResHeight + 1] = _resHeightBytes_[1];

				// These two offsets correct the game's main menu viewport to use the new resolution values.
				// Without this fix, the game will not accept main menu images where either dimension is larger
				// than either 1024x768 or custom resolutions with either dimension smaller than 1024x768.
				// In turn, any image smaller than those dimensions will be put in the top-left corner and
				// black bars will fill the remaining space on the bottom and right.
				// This is all despite the fact that buttons will be in the correct locations.
				_EmperorExeData_[_resHexOffsetTable_._MainMenuViewportWidth + 0] = _resWidthBytes_[0];
				_EmperorExeData_[_resHexOffsetTable_._MainMenuViewportWidth + 1] = _resWidthBytes_[1];
				_EmperorExeData_[_resHexOffsetTable_._MainMenuViewportHeight + 0] = _resHeightBytes_[0];
				_EmperorExeData_[_resHexOffsetTable_._MainMenuViewportHeight + 1] = _resHeightBytes_[1];

				// This offset corrects the position of the money, population and zodiac info in the top menu bar.
				// Without this patch, that text will be drawn too far to the left.
				_EmperorExeData_[_resHexOffsetTable_._FixMoneyPopDateTextPosWidth + 0] = _resWidthBytes_[0];
				_EmperorExeData_[_resHexOffsetTable_._FixMoneyPopDateTextPosWidth + 1] = _resWidthBytes_[1];

				// This offset corrects the position of the top menu bar containing the above text.
				// Without this patch, that background will be drawn too far to the left.
				_EmperorExeData_[_resHexOffsetTable_._FixTopMenuBarBackgroundPosWidth + 0] = _resWidthBytes_[0];
				_EmperorExeData_[_resHexOffsetTable_._FixTopMenuBarBackgroundPosWidth + 1] = _resWidthBytes_[1];

				// Set main game's viewport to the correct width.
				// This means the width that will be taken by both the city view's "camera" and the right sidebar containing the city's info and
				// buttons to build and demolish buildings and other functions.
				// Without this patch, the view of your city will be rendered in a small square placed at the top-left corner of the main viewing area.
				_EmperorExeData_[_resHexOffsetTable_._ViewportWidth + 0] = _resWidthBytes_[0];
				_EmperorExeData_[_resHexOffsetTable_._ViewportWidth + 1] = _resWidthBytes_[1];

				// These next two offsets are used to determine the size of the city view's "camera".
				// However, the game doesn't allow specifying a size. Only a multiplier can be used.
				// These multipliers are, in turn, used to calculate the size in pixels that the city's viewport should be.
				// Like his Zeus guide, I found that Mario's calculations were off again so had to redo them once more.
				//
				// After some trial and error, I found that the formulae this game uses to calculate these are:
				//     Height = (Height_Multiplier - 1) * 20
				//     Width  =  Width_Multiplier  * 80 - 2
				//
				// For the height, the first row of pixels used to draw the city's viewport will be the window's 41st row (the row immediately after the top menu bar).
				// Interestingly, I noticed that in both Zeus and Emperor, each height step appears to be half the width of the top menu bar.
				// For the width, the first row will be the window's 1st column of pixels (right against the left edge).
				//
				// If either multiplier is too small, the rendered city's viewport will be noticeably smaller, with gaps between
				// this viewport and the UI. Artifacts from previous background images will appear in these gaps.
				// If the width multiplier is too big, the city view will overlap the right side bar.
				// If the height multiplier is too big, the viewport will extend beyond the bottom of the screen.
				//
				// As a result, appropriate multipliers will be ones that come as close as possible to the bottom edge/sidebar without overlapping.
				// To do this, we simply reverse the equations:
				//     Height_Multiplier = Height / 20 + 1
				//     Width_Multiplier  = (Width + 2) / 80
				//
				// When using selected resolution in the calculations, the size of the UI elements need to accounted for:
				//     40px for the top menubar.
				//     222px for the right sidebar. Even though the sidebar's actual width is 226px, the last 4 pixels can be pushed
				//         off the screen without any problem. Thus, I'll use this fact to get a little bit more space for the city view.
				//
				// After that, we also need to round our final figure down to the nearest integer.
				// Finally, these values are signed 8-bit integers and so, must be capped at 127.
				byte _resHeightMult_;
				// 2560 plugged into the formula below is equal to 127. Thus, this and any higher number must use a capped multiplier.
				if (_ResHeight_ >= 2560)
				{
					_resHeightMult_ = 127;
				}
				else
				{
					_resHeightMult_ = (byte)Math.Floor(((_ResHeight_ - 40) / 20f) + 1); // fs are required. Otherwise, compiler error CS0121 occurs.
				}
				byte _resWidthMult_;
				// 10380 plugged into the formula below is equal to 127. Thus, this and any higher number must use a capped multiplier.
				if (_ResWidth_ >= 10380)
				{
					_resWidthMult_ = 127;
				}
				else
				{
					_resWidthMult_ = (byte)Math.Floor((_ResWidth_ - 222 + 2) / 80f); // fs are required. Otherwise, compiler error CS0121 occurs.
				}
				_EmperorExeData_[_resHexOffsetTable_._ViewportHeightMult] = _resHeightMult_;
				_EmperorExeData_[_resHexOffsetTable_._ViewportWidthMult]  = _resWidthMult_;

				// This offset partially corrects the position of the game's sidebar to align with the new viewport render limit
				// Without this change, the sidebar is drawn against the left edge of the screen and clips with the city view
				_EmperorExeData_[_resHexOffsetTable_._SidebarRenderLimitWidth + 0] = _resWidthBytes_[0];
				_EmperorExeData_[_resHexOffsetTable_._SidebarRenderLimitWidth + 1] = _resWidthBytes_[1];

				// This offset corrects the position of the rotate city view button's interaction point
				// Without this change, the interaction point is placed too high on the sidebar (under the city map in my case).
				_EmperorExeData_[_resHexOffsetTable_._FixSidebarCityMapRotateButton + 0] = _resWidthBytes_[0];
				_EmperorExeData_[_resHexOffsetTable_._FixSidebarCityMapRotateButton + 1] = _resWidthBytes_[1];

				// This offset corrects the position of the rotate city view button's icon
				// Without this change, the icon is drawn too high on the sidebar (under the city map in my case).
				_EmperorExeData_[_resHexOffsetTable_._FixSidebarCityMapRotateIcon + 0] = _resWidthBytes_[0];
				_EmperorExeData_[_resHexOffsetTable_._FixSidebarCityMapRotateIcon + 1] = _resWidthBytes_[1];

				// This offset corrects the position of the interaction points for the other 4 buttons below the city map
				// Without this change, the interaction points are placed too high on the sidebar (inside the city map in my case).
				_EmperorExeData_[_resHexOffsetTable_._FixSidebarCityMapGoalsOverviewWorldMapMessagesIcons + 0] = _resWidthBytes_[0];
				_EmperorExeData_[_resHexOffsetTable_._FixSidebarCityMapGoalsOverviewWorldMapMessagesIcons + 1] = _resWidthBytes_[1];

				// This offset corrects the position of the icons for the other 4 buttons below the city map
				// Without this change, the icons are drawn too high on the sidebar (inside the city map in my case).
				_EmperorExeData_[_resHexOffsetTable_._FixSidebarCityMapGoalsOverviewWorldMapMessagesButtons + 0] = _resWidthBytes_[0];
				_EmperorExeData_[_resHexOffsetTable_._FixSidebarCityMapGoalsOverviewWorldMapMessagesButtons + 1] = _resWidthBytes_[1];

				// This next offset is used to determine which column of pixels in the game's window will be used as the left edge of the right sidebar.
				// The original game uses the calculation "ResolutionWidth - 226" to find this column. However, this causes a problem.
				// The original game used a horizontal resolution of 1024. When used in the formula to calculate a width multiplier (with a sidebar width of 186px),
				// the result is exactly 14, without any decimals.
				//
				// However, the fact that the city view's "camera" uses a multiplier to draw the scene in 60 pixel increments means that
				// just using the original formula to find this number will mean that most resolutions will have a gap
				// between the right edge of the city view and the left edge of the sidebar.
				//
				// To alleviate that problem, my solution is to use the formula mentioned above to calculate the width of the city view using the
				// appropriate multiplier calculated above. This figure is then used to designate where the left edge of the sidebar starts.
				// This means that the sidebar will be shifted left to be next to the city view.
				_ViewportWidth_ = Convert.ToUInt16((_resWidthMult_ * 80) - 2);
				byte[] _viewportWidthBytes_ = BitConverter.GetBytes(_ViewportWidth_);
				_EmperorExeData_[_resHexOffsetTable_._SidebarLeftEdgeStartWidth + 0] = _viewportWidthBytes_[0];
				_EmperorExeData_[_resHexOffsetTable_._SidebarLeftEdgeStartWidth + 1] = _viewportWidthBytes_[1];

				// I don't know what this offset does. JackFuste's patches have it changed but I haven't seen the effect anywhere.
				_EmperorExeData_[_resHexOffsetTable_._UnknownWidth + 0] = _resWidthBytes_[0];
				_EmperorExeData_[_resHexOffsetTable_._UnknownWidth + 1] = _resWidthBytes_[1];

				// I don't know what this offset does. JackFuste's patches have it changed but I haven't seen the effect anywhere.
				_EmperorExeData_[_resHexOffsetTable_._UnknownHeight + 0] = _resHeightBytes_[0];
				_EmperorExeData_[_resHexOffsetTable_._UnknownHeight + 1] = _resHeightBytes_[1];

				// Next, the bar forming the city viewport's bottom border needs to be repositioned at the bottom edge's actual position
				// as well as extend it to fit the entire length of that edge. To do this, I'm inserting some new assembly code into the
				// EXE that overwrites the code currently in place.
				//
				// That said, this step can be skipped if the viewport's height is equal to the game's resolution,
				// since there is nothing that needs to be drawn.
				_ViewportHeight_ = Convert.ToUInt16(((_resHeightMult_ -1) * 20) + 40);
				byte[] _viewportHeightBytes_ = BitConverter.GetBytes(_ViewportHeight_);
				// First, insert the new code.
				for (byte _i_ = 0; _i_ < _resHexOffsetTable_._FixBottomBarLengthNewCode.Length; _i_++)
				{
					_EmperorExeData_[_resHexOffsetTable_._FixBottomBarLengthNewCodeInsertPoint + _i_] = _resHexOffsetTable_._FixBottomBarLengthNewCode[_i_];
				}
				// Next, the starting point of the border drawing code needs to be set to the width of the city viewport.
				for (byte _i_ = 0; _i_ < _viewportWidthBytes_.Length; _i_++)
				{
					_EmperorExeData_[_resHexOffsetTable_._FixBottomBarLengthNewCodeInsertPoint + 1 + _i_] = _viewportWidthBytes_[_i_];
				}
				// Next, calculate where the bottom edge of the city viewport is and set the bars' vertical position there.
				for (byte _i_ = 0; _i_ < _viewportHeightBytes_.Length; _i_++)
				{
					_EmperorExeData_[_resHexOffsetTable_._FixBottomBarLengthNewCodeInsertPoint + 23 + _i_] = _viewportHeightBytes_[_i_];
				}
				// Next, calculate where the first drawing function is relative to the inserted code so that it's address can be set correctly.
				int _firstDrawingFunctionRelativePos_ = _resHexOffsetTable_._DrawFunction1Address - (_resHexOffsetTable_._FixBottomBarLengthNewCodeInsertPoint + 38);
				byte[] _firstDrawingFunctionRelativePosBytes_ = BitConverter.GetBytes(_firstDrawingFunctionRelativePos_);
				for (byte _i_ = 0; _i_ < _firstDrawingFunctionRelativePosBytes_.Length; _i_++)
				{
					_EmperorExeData_[_resHexOffsetTable_._FixBottomBarLengthNewCodeInsertPoint + 34 + _i_] = _firstDrawingFunctionRelativePosBytes_[_i_];
				}
				// Next, do the same for the second drawing function.
				int _secondDrawingFunctionRelativePos_ = _resHexOffsetTable_._DrawFunction2Address - (_resHexOffsetTable_._FixBottomBarLengthNewCodeInsertPoint + 44);
				byte[] _secondDrawingFunctionRelativePosBytes_ = BitConverter.GetBytes(_secondDrawingFunctionRelativePos_);
				for (byte _i_ = 0; _i_ < _secondDrawingFunctionRelativePosBytes_.Length; _i_++)
				{
					_EmperorExeData_[_resHexOffsetTable_._FixBottomBarLengthNewCodeInsertPoint + 40 + _i_] = _secondDrawingFunctionRelativePosBytes_[_i_];
				}
				// Finally, correctly set the final jump's relative position to the place what the original drawing code was originally going
				// to run after executing.
				byte _finalJumpDestRelPos_ = Convert.ToByte(_resHexOffsetTable_._FixBottomBarLengthFinalJumpDest -
				                                            (_resHexOffsetTable_._FixBottomBarLengthNewCodeInsertPoint +
				                                             _resHexOffsetTable_._FixBottomBarLengthNewCode.Length));
				_EmperorExeData_[_resHexOffsetTable_._FixBottomBarLengthNewCodeInsertPoint + 58] = _finalJumpDestRelPos_;

				// Finally, some code is needed to fill in gaps in the UI caused by changing the resolution. This is a gap in the
				// menubar between the right edge of the original extent of it and the sidebar's left edge.
				// There is also a gap to the right and bottom of the sidebar and underneath the viewport.
				// Thus, new code needs to be injected into the EXE to draw UI elements to fill these gaps.
				// The bottom border code above already contains the needed jump into this new code.
				//
				// First, fill in the correct figure for the jump to where the new UI code is located.
				int _jumpToNewUiCodeLocation_ = _resHexOffsetTable_._NewCodeForUiInsertionLocation - (_resHexOffsetTable_._NewCodeForUiJumpLocation + 5);
				byte[] _jumpToNewUiCodeLocationBytes_ = BitConverter.GetBytes(_jumpToNewUiCodeLocation_);
				_EmperorExeData_[_resHexOffsetTable_._NewCodeForUiJumpLocation] = 0xE9;
				for (byte _i_ = 0; _i_ < _jumpToNewUiCodeLocationBytes_.Length; _i_++)
				{
					_EmperorExeData_[_resHexOffsetTable_._NewCodeForUiJumpLocation + 1 + _i_] = _jumpToNewUiCodeLocationBytes_[_i_];
				}
				_EmperorExeData_[_resHexOffsetTable_._NewCodeForUiJumpLocation + 5] = 0x90;
				// Next, insert the new code.
				for (ushort _i_ = 0; _i_ < _resHexOffsetTable_._NewCodeForUiBytes.Length; _i_++)
				{
					_EmperorExeData_[_resHexOffsetTable_._NewCodeForUiInsertionLocation + _i_] = _resHexOffsetTable_._NewCodeForUiBytes[_i_];
				}
				// Next, set the initial value for the menubar height counter.
				byte[] _menubarHeightInitialValue_ = BitConverter.GetBytes(_ResHeight_ - 39);
				for (byte _i_ = 0; _i_ < _menubarHeightInitialValue_.Length; _i_++)
				{
					_EmperorExeData_[_resHexOffsetTable_._NewCodeForUiInsertionLocation + _i_ + 1] = _menubarHeightInitialValue_[_i_];
				}
				// Next, set the initial position for the menubar's left edge for each row.
				for (byte _i_ = 0; _i_ < _viewportWidthBytes_.Length; _i_++)
				{
					_EmperorExeData_[_resHexOffsetTable_._NewCodeForUiInsertionLocation + _i_ + 6] = _viewportWidthBytes_[_i_];
				}
				// Next, calculate where the first drawing function is relative to the inserted code so that it's address can be set correctly.
				_firstDrawingFunctionRelativePos_ = _resHexOffsetTable_._DrawFunction1Address - (_resHexOffsetTable_._NewCodeForUiInsertionLocation + 44);
				_firstDrawingFunctionRelativePosBytes_ = BitConverter.GetBytes(_firstDrawingFunctionRelativePos_);
				for (byte _i_ = 0; _i_ < _firstDrawingFunctionRelativePosBytes_.Length; _i_++)
				{
					_EmperorExeData_[_resHexOffsetTable_._NewCodeForUiInsertionLocation + _i_ + 40] = _firstDrawingFunctionRelativePosBytes_[_i_];
				}
				// Next, do the same for the second drawing function.
				_secondDrawingFunctionRelativePos_ = _resHexOffsetTable_._DrawFunction2Address - (_resHexOffsetTable_._NewCodeForUiInsertionLocation + 50);
				_secondDrawingFunctionRelativePosBytes_ = BitConverter.GetBytes(_secondDrawingFunctionRelativePos_);
				for (byte _i_ = 0; _i_ < _secondDrawingFunctionRelativePosBytes_.Length; _i_++)
				{
					_EmperorExeData_[_resHexOffsetTable_._NewCodeForUiInsertionLocation + _i_ + 46] = _secondDrawingFunctionRelativePosBytes_[_i_];
				}
				// Next, set the final position for the menubar's top edge for below the city viewport.
				byte[] _viewportBottomForMenubarBytes_ = BitConverter.GetBytes(_ViewportHeight_ - 39 + 9);
				for (byte _i_ = 0; _i_ < _viewportBottomForMenubarBytes_.Length; _i_++)
				{
					_EmperorExeData_[_resHexOffsetTable_._NewCodeForUiInsertionLocation + _i_ + 59] = _viewportBottomForMenubarBytes_[_i_];
				}
				// Next, set the initial position for the sidebar filler to the right of the city viewport
				for (byte _i_ = 0; _i_ < _viewportWidthBytes_.Length; _i_++)
				{
					_EmperorExeData_[_resHexOffsetTable_._NewCodeForUiInsertionLocation + _i_ + 103] = _viewportWidthBytes_[_i_];
				}
				// Next, calculate where the first drawing function is relative to the inserted code so that it's address can be set correctly.
				_firstDrawingFunctionRelativePos_ = _resHexOffsetTable_._DrawFunction1Address - (_resHexOffsetTable_._NewCodeForUiInsertionLocation + 140);
				_firstDrawingFunctionRelativePosBytes_ = BitConverter.GetBytes(_firstDrawingFunctionRelativePos_);
				for (byte _i_ = 0; _i_ < _firstDrawingFunctionRelativePosBytes_.Length; _i_++)
				{
					_EmperorExeData_[_resHexOffsetTable_._NewCodeForUiInsertionLocation + _i_ + 136] = _firstDrawingFunctionRelativePosBytes_[_i_];
				}
				// Next, do the same for the second drawing function.
				_secondDrawingFunctionRelativePos_ = _resHexOffsetTable_._DrawFunction2Address - (_resHexOffsetTable_._NewCodeForUiInsertionLocation + 146);
				_secondDrawingFunctionRelativePosBytes_ = BitConverter.GetBytes(_secondDrawingFunctionRelativePos_);
				for (byte _i_ = 0; _i_ < _secondDrawingFunctionRelativePosBytes_.Length; _i_++)
				{
					_EmperorExeData_[_resHexOffsetTable_._NewCodeForUiInsertionLocation + _i_ + 142] = _secondDrawingFunctionRelativePosBytes_[_i_];
				}
				// Next, fill in the final position checker for the sidebar drawing code at the window's right edge/
				for (byte _i_ = 0; _i_ < _resWidthBytes_.Length; _i_++)
				{
					_EmperorExeData_[_resHexOffsetTable_._NewCodeForUiInsertionLocation + _i_ + 156] = _resWidthBytes_[_i_];
				}
				// Finally, set the bottom limit for the sidebar drawing code as the window height - UI element's height (310)
				byte[] _sidebarDrawingBottomLimitBytes_ = BitConverter.GetBytes(_ResWidth_ - 310);
				for (byte _i_ = 0; _i_ < _sidebarDrawingBottomLimitBytes_.Length; _i_++)
				{
					_EmperorExeData_[_resHexOffsetTable_._NewCodeForUiInsertionLocation + _i_ + 163] = _sidebarDrawingBottomLimitBytes_[_i_];
				}
				// Next, calculate where the final jump must point to move execution to the bottom border drawing code.
				int _jumpToBottomBorderDrawingCode_ = _resHexOffsetTable_._NewCodeForUiJumpLocation + 6 - (_resHexOffsetTable_._NewCodeForUiInsertionLocation + 195);
				byte[] _jumpToBottomBorderDrawingCodeBytes_ = BitConverter.GetBytes(_jumpToBottomBorderDrawingCode_);
				for (byte _i_ = 0; _i_ < _jumpToBottomBorderDrawingCodeBytes_.Length; _i_++)
				{
					_EmperorExeData_[_resHexOffsetTable_._NewCodeForUiInsertionLocation + _i_ + 191] = _jumpToBottomBorderDrawingCodeBytes_[_i_];
				}
			}
		}
	}
}

// ReSharper disable CommentTypo
/*
	The above code I wrote was mainly thanks to a post on the Widescreen Gaming Forum written by Mario: https://www.wsgf.org/phpBB3/viewtopic.php?p=173006#p173006
	Inside the zip file he provided, he included a guide for hex-editing Emperor.exe to make changes to the game's "1024x768" resolution option.

	Here are the contents of that guide:
	====================================
	.- Emperor HEX Changes -.
	.-=====================-.

	[!] Follow slowly and step by step (modify and run the game to check for changes)
	[!] The game crashes if the viewport is bigger than the resolution. Check that first!!!
	[!] These are based on Jackfuste work (the 'exe' is not the gog version but it still works)
	[!] Since the 'exe' file is not the gog one you will need to do the photoshop trick (draw the missing parts on the previous screen)
	to fix that version (or this version if you are trying to do 4k resolution)
	[!] You need to select 1024x768 resolution in game for this to work
	...
	[!] These images need to have the same size as the resolution:
	{
	scoreb.jpg
	China_FE_HighScores
	China_FE_OpenPlay
	China_FE_CampaignSelection.jpg
	China_FE_MissionIntroduction.jpg
	China_Defeat.jpg
	China_Victory.jpg
	China_FE_Registry.jpg
	China_FE_ChooseGame.jpg
	China_FE_MainMenu.jpg
	China_FE_tutorials.jpg
	}


	----[Resolution Width & Height]----

	addr 12b668: f8 03 75 12 be [ww ww] 00 00 b9 [hh hh] 00 00 89 35
	(gog addr 12aa60)


	----[Fix Opening Screen]----

	addr 125f28: 81 fe [ww ww] 00 00 75 10
	addr 125f40: 81 fb [hh hh] 00 00 75 09
	(gog addr 125328) & (gog addr 125340)


	----[Fix Menu]----

	addr 1b6868: 2a 3d [ww ww] 00 00 75 23  (fix menu text)
	(gog addr 1b5c68)
	addr 1beb70: [ww ww] 00 00 0f 85 be 00  (fix menu background)
	(gog addr 1bdf70)


	----[Fix Bottom Borders]----

	// the bottom borders height is 8px
	// for 1080p => 1080 - 8 = 1072 = 0x0430 => [yy yy] = [30 04]
	// for 1440p => 1440 - 8 = 1432 = 0x0598 => [yy yy] = [98 05]

	addr 1beba8: [yy yy] 00 00 6a 00 68 d6  (fix first bottom border position)
	(gog addr 1bdfa8)

	addr 3a8c60: 68 [yy yy] 00 00 68 [xx xx]  (fix second bottom border position)
	// exactly after the first border 798px => [xx xx] = [1e 03]
	addr 1bebd8: fc ff ff 68 [yy yy] 00 00  (fix third bottom border position)

	[!] if you don't like these bottom borders just render them off screen (yy position same as resolution height)


	----[Fix Sidebar Bottom Buttons]----

	addr 13b1d8: 81 f9 [ww ww] 00 00 89 1d  (fix rotate map interaction)
	(gog addr 13a5d8)
	addr 13b2c8: 81 f9 [ww ww] 00 00 75 05  (fix rotate map position)
	(gog addr 13a6c8)
	addr 13b608: 81 f9 [ww ww] 00 00 75 05  (fix goals/overview/map buttons position)
	(gog addr 13aa08)
	addr 13b638: 81 f9 [ww ww] 00 00 75 05  (fix goals/overview/map buttons interaction)
	(gog addr 13aa38)
	addr 089d78: c4 08 81 f9 [ww ww] 00 00 8d 78  (fix empire map buttons interaction)
	(gog addr 89178)
	addr 1b85e0: 10 33 f6 3d [ww ww] 00 00 75 05  (fix empire map buttons position)
	(gog addr 1b79e0)


	----[Main Viewport]----

	addr 13ca88: 00 00 74 11 3d [ww ww] 00  (render limit)
	(gog addr 13be88)

	[!] You can't have any width or height, you only provide a multiplier (mw, mh)

	> for height: mh = round((hh - 30) * 0.05)
	// example for 1080p: (1080 - 30) * 0.05 = 1050 * 0.05 = 52.5 => 53 for multiplier => 0x35
	// 30 is the height of the top menu
	// 0.05 = 1/20 where 20 is the tile height

	> for width: starting_mw = round(ww * 0.0125)
	// example for 1920p: 1920 * 0.0125 = 24 for multiplier => 0x18
	// 0.0125 = 1/80 where 80 is the tile width

	[!] This will fill the whole screen over the sidebar and we don't want that.
	But at the same time it gives us a starting value for the width multiplier.
	From here we just lower the value one by one until it looks like the sidebar fits.
	That's why on some resolutions you either cut the sidebar to get fullscreen or don't cut the sidebar and have a small background area to the right.

	addr 13ca98: [mh] 6a [mw] eb 08 6a 28 6a
	(gog addr 13be98)
	// [mh]=[25]; [mw]=[0a] for 768x1024
	// [mh]=[35]; [mw]=[15] for 1080x1920
	// [mh]=[47]; [mw]=[1d] for 1440x2560

	[!] After it looks that the sidebar will fit you must measure the viewport width (vw value)(just printscreen and paste it in a photo editor).
	This will give you the sidebar left position value.


	----[Sidebar Left Position]----

	addr 1b5a20: c3 3d [ww ww] 00 00 75 14  (render limit)
	(gog addr 1b4e20)

	addr 1b5a28: c7 05 0c 8e 3f 01 [vw vw]  (viewport width)
	(gog addr 1b4e28)
	// [1e 03] =>  798px for 1024p (sidebar width is 226px)
	// [8e 06] => 1678px for 1920p
	// [0e 09] => 2318px for 2560p


	----[The Three Top Menu Background Rectangles Left Position]----

	// the width of the rectangles is 798px

	addr 3a8cd0: [xx xx] 00 00 68 7e 02 00  (first rectangle)
	// after the game's base rectangle 798px => [xx xx] = [1e 03]

	addr 3a8d90: 00 68 [xx xx] 00 00 68 7e  (second rectangle)
	addr 3a8db0: 6A 00 6A 00 6A 00 68 [xx xx] 00 00 68 7E 02 00 00  (third rectangle)
	// for 2560p: vw - 798 = 2138 - 798 = 1520px => [xx xx] = [f0 05]


	----[The Three Bottom Right Rectangles XY Position]----

	// rectangle height is 310px; sidebar height is 768px

	addr 3a8ca0: 00 6a 00 6a 00 68 [yy yy] 00 00 68 [vw vw] 00 00 68  (first rectangle)
	// for 1080p top 738px => [yy yy] = [e2 02]

	addr 3a8d68: [yy yy] 00 00 68 [vw vw] 00 00 68 74 02 00 00 B9 30  (second rectangle)
	addr 3a8dd8: 00 68 [yy yy] 00 00 68 [vw vw] 00 00 68 74 02 00 00  (third rectangle)
	// for 1080p: top = 1080 - 310 = 720px => [yy yy] = [02 03];


	----[The Three Right Edge Vertical Rectangles XY Position]----

	// the width is 16px and the height is 600px

	addr 3a8cf0: 6a 00 68 [yy yy] 00 00 68 [xx xx] 00 00 68 81 02 00  (second rectangle)

	addr 3a8d18: 6a 00 68 [yy yy] 00 00 68 [xx xx] 00 00 68 81 02 00  (third rectangle)

	addr 3a8d40: 6A 00 6A [??] 68 [xx xx] 00 00  (first rectangle)

	// for 1920p: 1920 - 16 = 1904 = 0x770 => [xx xx] = [70 07]
	// for 2560p: 2560 - 16 = 2544 = 0x9f0 => [xx xx] = [f0 09]
	// for 1080p: top 512px => [yy yy] = [00 02]

	// [??] was 12 for 2560p but don't know why because it looks like it should be [00] (it changes the height when the height should be 0px)


	----[World Map Images]----

	> The Height = hh - 30 (height of the screen - the top menu)
	> The Width = the width of the viewport (vw value)
	> Maps: China_MapOfChina01...04
	// you don't just resize these images
	// you either cut them if you need smaller images or use the canvas tool and clone the surroundings for bigger images


	----[Optional]----
	// don't know what these do or if they are needed

	addr 12b950: e0 0d 01 [ww ww] 00 00 c7 05 18 e0 0d 01 [hh hh] 00
	(gog addr 12ad50)
	addr 1beb68: 3d [21] 03 00 00 74 79 3d
	(gog addr 1bdf68)
	// [20] is original value; why is changed with [21] for 2560p i have no clue
 */
// ReSharper restore CommentTypo
