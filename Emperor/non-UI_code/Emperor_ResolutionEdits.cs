﻿// This code is part of the Impressions Resolution Customiser project
//
// The license for it may be found here:
// https://github.com/XJDHDR/impressions-resolution-customiser/blob/main/LICENSE
//

using System;
using static Emperor.Emperor_ExeDefinitions;

namespace Emperor
{
	/// <summary>
	/// Holds code used to set the resolution that the game must run at and resize various elements of the game to account for the new resolution.
	/// </summary>
	class Emperor_ResolutionEdits
	{
		/// <summary>
		/// Patches the various offsets in Emperor.exe to run at the desired resolution and scale various UI elements 
		/// to fit the new resolution.
		/// </summary>
		/// <param name="ResWidth">The width value of the resolution inputted into the UI.</param>
		/// <param name="ResHeight">The height value of the resolution inputted into the UI.</param>
		/// <param name="ExeAttributes">Struct that specifies various details about the detected Emperor.exe</param>
		/// <param name="EmperorExeData">Byte array that contains the binary data contained within the supplied Emperor.exe</param>
		internal static void _hexEditExeResVals(ushort ResWidth, ushort ResHeight, ExeAttributes ExeAttributes, ref byte[] EmperorExeData)
		{
			if (FillResHexOffsetTable(ExeAttributes, out ResHexOffsetTable _resHexOffsetTable))
			{
				byte[] _resWidthBytes = BitConverter.GetBytes(ResWidth);
				byte[] _resHeightBytes = BitConverter.GetBytes(ResHeight);

				// These two offsets set the game's resolution to the desired amount
				EmperorExeData[_resHexOffsetTable._resWidth + 0] = _resWidthBytes[0];
				EmperorExeData[_resHexOffsetTable._resWidth + 1] = _resWidthBytes[1];
				EmperorExeData[_resHexOffsetTable._resHeight + 0] = _resHeightBytes[0];
				EmperorExeData[_resHexOffsetTable._resHeight + 1] = _resHeightBytes[1];

				// These two offsets correct the game's main menu viewport to use the new resolution values.
				// Without this fix, the game will not accept main menu images where either dimension is larger
				// than either 1024x768 or custom resolutions with either dimension smaller than 1024x768.
				// In turn, any image smaller than those dimensions will be put in the top-left corner and
				// black bars will fill the remaining space on the bottom and right.
				// This is all despite the fact that buttons will be in the correct locations.
				EmperorExeData[_resHexOffsetTable._mainMenuViewportWidth + 0] = _resWidthBytes[0];
				EmperorExeData[_resHexOffsetTable._mainMenuViewportWidth + 1] = _resWidthBytes[1];
				EmperorExeData[_resHexOffsetTable._mainMenuViewportHeight + 0] = _resHeightBytes[0];
				EmperorExeData[_resHexOffsetTable._mainMenuViewportHeight + 1] = _resHeightBytes[1];

				// This offset corrects the position of the money, population and zodiac info in the top menu bar.
				// Without this patch, that text will be drawn too far to the left.
				EmperorExeData[_resHexOffsetTable._fixMoneyPopDateTextPosWidth + 0] = _resWidthBytes[0];
				EmperorExeData[_resHexOffsetTable._fixMoneyPopDateTextPosWidth + 1] = _resWidthBytes[1];

				// This offset corrects the position of the top menu bar containing the above text.
				// Without this patch, that background will be drawn too far to the left.
				EmperorExeData[_resHexOffsetTable._fixTopMenuBarBackgroundPosWidth + 0] = _resWidthBytes[0];
				EmperorExeData[_resHexOffsetTable._fixTopMenuBarBackgroundPosWidth + 1] = _resWidthBytes[1];

				// Set main game's viewport to the correct width.
				// This means the width that will be taken by both the city view's "camera" and the right sidebar containing the city's info and 
				// buttons to build and demolish buildings and other functions.
				// Without this patch, the view of your city will be rendered in a small square placed at the top-left corner of the main viewing area.
				EmperorExeData[_resHexOffsetTable._viewportWidth + 0] = _resWidthBytes[0];
				EmperorExeData[_resHexOffsetTable._viewportWidth + 1] = _resWidthBytes[1];

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
				// Finally, we also need to round our final figure down to the nearest integer.
				byte _resHeightMult = (byte)Math.Floor((ResHeight - 40) / 20f + 1);	// fs are required. Otherwise,
				byte _resWidthMult  = (byte)Math.Floor((ResWidth - 222 + 2) / 80f);	// compiler error CS0121 occurrs.
				EmperorExeData[_resHexOffsetTable._viewportHeightMult] = _resHeightMult;
				EmperorExeData[_resHexOffsetTable._viewportWidthMult]  = _resWidthMult;

				// This offset partially corrects the position of the game's sidebar to align with the new viewport render limit
				// Without this change, the sidebar is drawn against the left edge of the screen and clips with the city view
				EmperorExeData[_resHexOffsetTable._sidebarRenderLimitWidth + 0] = _resWidthBytes[0];
				EmperorExeData[_resHexOffsetTable._sidebarRenderLimitWidth + 1] = _resWidthBytes[1];

				// This offset corrects the position of the rotate city view button's interaction point
				// Without this change, the interaction point is placed too high on the sidebar (under the city map in my case).
				EmperorExeData[_resHexOffsetTable._fixSidebarCityMap_RotateButton + 0] = _resWidthBytes[0];
				EmperorExeData[_resHexOffsetTable._fixSidebarCityMap_RotateButton + 1] = _resWidthBytes[1];

				// This offset corrects the position of the rotate city view button's icon
				// Without this change, the icon is drawn too high on the sidebar (under the city map in my case).
				EmperorExeData[_resHexOffsetTable._fixSidebarCityMap_RotateIcon + 0] = _resWidthBytes[0];
				EmperorExeData[_resHexOffsetTable._fixSidebarCityMap_RotateIcon + 1] = _resWidthBytes[1];

				// This offset corrects the position of the interaction points for the other 4 buttons below the city map
				// Without this change, the interaction points are placed too high on the sidebar (inside the city map in my case).
				EmperorExeData[_resHexOffsetTable._fixSidebarCityMap_GoalsOverviewWorldMapMessagesIcons + 0] = _resWidthBytes[0];
				EmperorExeData[_resHexOffsetTable._fixSidebarCityMap_GoalsOverviewWorldMapMessagesIcons + 1] = _resWidthBytes[1];

				// This offset corrects the position of the icons for the other 4 buttons below the city map
				// Without this change, the icons are drawn too high on the sidebar (inside the city map in my case).
				EmperorExeData[_resHexOffsetTable._fixSidebarCityMap_GoalsOverviewWorldMapMessagesButtons + 0] = _resWidthBytes[0];
				EmperorExeData[_resHexOffsetTable._fixSidebarCityMap_GoalsOverviewWorldMapMessagesButtons + 1] = _resWidthBytes[1];

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
				byte[] _viewportWidthBytes = BitConverter.GetBytes(Convert.ToUInt16(_resWidthMult * 80 - 2));
				EmperorExeData[_resHexOffsetTable._sidebarLeftEdgeStartWidth + 0] = _viewportWidthBytes[0];
				EmperorExeData[_resHexOffsetTable._sidebarLeftEdgeStartWidth + 1] = _viewportWidthBytes[1];

				// This offset replaces a JNZ call with two NOPs
				// It causes the bottom bar to be drawn all the way and connect to the sidebar instead of stopping at 798px in length.
				// It does this by drawing the bar graphic twice.
				EmperorExeData[_resHexOffsetTable._fixBottomBarLength + 0] = 0x90;
				EmperorExeData[_resHexOffsetTable._fixBottomBarLength + 1] = 0x90;

				// This offset moves the first bar created above to the bottom left corner of the window.
				byte[] _bottomBarRowTopEdgePosBytes = BitConverter.GetBytes(Convert.ToUInt16(ResHeight - 8));
				EmperorExeData[_resHexOffsetTable._unknownBottomBarTweak1 + 0] = _bottomBarRowTopEdgePosBytes[0];
				EmperorExeData[_resHexOffsetTable._unknownBottomBarTweak1 + 1] = _bottomBarRowTopEdgePosBytes[1];

				// This offset moves the second bar created above next to the first bar.
				EmperorExeData[_resHexOffsetTable._unknownBottomBarTweak2 + 0] = _bottomBarRowTopEdgePosBytes[0];
				EmperorExeData[_resHexOffsetTable._unknownBottomBarTweak2 + 1] = _bottomBarRowTopEdgePosBytes[1];

				// I don't know what this offset does. JackFuste's patches have it changed but I haven't seen the effect anywhere.
				EmperorExeData[_resHexOffsetTable._unknownWidth + 0] = _resWidthBytes[0];
				EmperorExeData[_resHexOffsetTable._unknownWidth + 1] = _resWidthBytes[1];

				// I don't know what this offset does. JackFuste's patches have it changed but I haven't seen the effect anywhere.
				EmperorExeData[_resHexOffsetTable._unknownHeight + 0] = _resHeightBytes[0];
				EmperorExeData[_resHexOffsetTable._unknownHeight + 1] = _resHeightBytes[1];


				// At this point, the only outstanding issue to be fixed is that there are two gaps in the UI that just
				// show the last thing drawn in that area. The first is a small gap between the right edge of the top menubar
				// and left edge of the right sidebar. The second is a large gap on the right and bottom of the sidebar.
				//
				// JackFuste fixed these in his patch by inserting some new code into the EXE that extends the current UI into
				// the areas where these gaps existed. The last portion of this function will be used to replicate this insertion.
				//
				// First, modify a function call into a jump to our inserted code.
				for (byte _i = 0; _i < _resHexOffsetTable._extendOriginalUiNewCodeJumpData.Length; _i++)
				{
					EmperorExeData[_resHexOffsetTable._extendOriginalUiNewCodeJumpOffset + _i] = _resHexOffsetTable._extendOriginalUiNewCodeJumpData[_i];
				}
				// After that, insert our new code.
				for (byte _i = 0; _i < _resHexOffsetTable._extendOriginalUiNewCodeData.Length; _i++)
				{
					EmperorExeData[_resHexOffsetTable._extendOriginalUiNewCodeOffset + _i] = _resHexOffsetTable._extendOriginalUiNewCodeData[_i];
				}
			}
		}
	}
}

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

	[!] After it looks that the sidebar will fit you must mesure the viewport width (vw value)(just printscreen and paste it in a photo editor).
	This will give you the sidebar left postion value.


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
