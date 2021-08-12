﻿// This code is part of the Impressions Resolution Customiser project
//
// The license for it may be found here:
// https://github.com/XJDHDR/impressions-resolution-customiser/blob/main/LICENSE
//

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace Emperor
{
	/// <summary>
	/// Code used to resize the background images and maps that the game uses to fit new resolutions.
	/// </summary>
	class Emperor_ResizeImages
	{
		/// <summary>
		/// Root function that calls the other functions in this class.
		/// </summary>
		/// <param name="EmperorExeLocation">String that contains the location of Emperor.exe</param>
		/// <param name="ResWidth">The width value of the resolution inputted into the UI.</param>
		/// <param name="ResHeight">The height value of the resolution inputted into the UI.</param>
		/// <param name="PatchedFilesFolder">String which specifies the location of the "patched_files" folder.</param>
		internal static void CreateResizedImages(string EmperorExeLocation, ushort ResWidth, ushort ResHeight, string PatchedFilesFolder)
		{
			string _emperorDataFilesFolderLocation = EmperorExeLocation.Remove(EmperorExeLocation.Length - 11) + @"DATA\";
			_fillImageArrays(out string[] _imagesToResize);
			_resizeCentredImages(_emperorDataFilesFolderLocation, _imagesToResize, ResWidth, ResHeight, PatchedFilesFolder);
		}

		/// <summary>
		/// Resizes the maps and other images used by the game to the correct size.
		/// </summary>
		/// <param name="_emperorDataFolderLocation">String that contains the location of Emperor's "DATA" folder.</param>
		/// <param name="_centredImages">String array that contains a list of the images that need to be resized.</param>
		/// <param name="_resWidth">The width value of the resolution inputted into the UI.</param>
		/// <param name="_resHeight">The height value of the resolution inputted into the UI.</param>
		/// <param name="_patchedFilesFolder">String which specifies the location of the "patched_files" folder.</param>
		private static void _resizeCentredImages(string _emperorDataFolderLocation, string[] _centredImages, ushort _resWidth, ushort _resHeight, string _patchedFilesFolder)
		{
			ImageCodecInfo _jpegCodecInfo = null;
			ImageCodecInfo[] _allImageCodecs = ImageCodecInfo.GetImageEncoders();
			for (int _j = 0; _j < _allImageCodecs.Length; _j++)
			{
				if (_allImageCodecs[_j].MimeType == "image/jpeg")
				{
					_jpegCodecInfo = _allImageCodecs[_j];
					break;
				}
			}

			if (_jpegCodecInfo != null)
			{
				EncoderParameters _encoderParameters = new EncoderParameters(1);
				_encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 85L);

				Directory.CreateDirectory(_patchedFilesFolder + @"\DATA");
				string _emperorMapTemplateImageLocation = AppDomain.CurrentDomain.BaseDirectory + @"ocean_pattern\ocean_pattern.png";

				if (!File.Exists(_emperorMapTemplateImageLocation))
				{
					MessageBox.Show("Could not find \"ocean_pattern\\ocean_pattern.png\". A fallback colour will be used to create the maps instead. " +
						"Please check if the ocean_pattern image was successfully extracted from this program's downloaded archive and is in the correct place.");
				}

				Parallel.For(0, _centredImages.Length, _i =>
				{
					if (File.Exists(_emperorDataFolderLocation + _centredImages[_i]))
					{
						using (Bitmap _oldImage = new Bitmap(_emperorDataFolderLocation + _centredImages[_i]))
						{
							bool _currentImageIsMap = false;
							ushort _newImageWidth;
							ushort _newImageHeight;
							if (Regex.IsMatch(_centredImages[_i], "^China_MapOfChina0[1-4].jpg$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
							{
								// Map images need to have the new images sized to fit the game's viewport.
								_currentImageIsMap = true;
								_newImageWidth = (ushort)(_resWidth - 180);
								_newImageHeight = (ushort)(_resHeight - 30);
							}
							else
							{
								_newImageWidth = _resWidth;
								_newImageHeight = _resHeight;
							}

							using (Bitmap _newImage = new Bitmap(_newImageWidth, _newImageHeight))
							{
								using (Graphics _newImageGraphics = Graphics.FromImage(_newImage))
								{
									// Note to self: Don't simplify the DrawImage calls. Specifying the old image's width and height is required
									// to work around a bug/quirk where the image's DPI is scaled to the screen's before insertion:
									// https://stackoverflow.com/a/41189062
									if (_currentImageIsMap)
									{
										// This file is one of the maps. Must be placed in the top-left corner of the new image.
										// Also create the background colour that will be used to fill the spaces not taken by the original image.
										if (File.Exists(_emperorMapTemplateImageLocation))
										{
											// Note to self: Don't try and make this more efficient. Only one thread can access a bitmap at a time, even if just reading.
											using (Bitmap _mapBackgroundImage = new Bitmap(_emperorMapTemplateImageLocation))
											{
												_newImageGraphics.DrawImage(_mapBackgroundImage, 0, 0, _mapBackgroundImage.Width, _mapBackgroundImage.Height);
											}
										}
										else
										{
											_newImageGraphics.Clear(Color.FromArgb(255, 35, 88, 120));
										}

										_newImageGraphics.DrawImage(_oldImage, 0, 0, _oldImage.Width, _oldImage.Height);
									}
									else
									{
										// A non-map image. Must be placed in the centre of the new image with a black background.
										_newImageGraphics.Clear(Color.Black);

										_newImageGraphics.DrawImage(_oldImage, (_newImageWidth - _oldImage.Width) / 2,
											(_newImageHeight - _oldImage.Height) / 2, _oldImage.Width, _oldImage.Height);
									}

									_newImage.Save(_patchedFilesFolder + @"\DATA\" + _centredImages[_i], _jpegCodecInfo, _encoderParameters);
								}
							}
						}
					}
					else
					{
						MessageBox.Show("Could not find the image located at: " + _emperorDataFolderLocation + _centredImages[_i]);
					}
				});
			}
			else
			{
				MessageBox.Show("Could not resize any of the game's images because the program could not find a JPEG Encoder available on your PC. Since Windows comes " +
					"with such a codec by default, this could indicate a serious problem with your PC that can only be fixed by reinstalling Windows.");
			}
		}

		/// <summary>
		/// Fills a string array with a list of the images that need to be resized.
		/// </summary>
		/// <param name="_imagesToResize">String array that contains a list of the images that need to be resized.</param>
		private static void _fillImageArrays(out string[] _imagesToResize)
		{
			_imagesToResize = new string[]
			{
				"China_Defeat.jpg",
				"China_editor_splash.jpg",
				"China_FE_CampaignSelection.jpg",
				"China_FE_ChooseGame.jpg",
				"China_FE_HighScores.jpg",
				"China_FE_MainMenu.jpg",
				"China_FE_MissionIntroduction.jpg",
				"China_FE_OpenPlay.jpg",
				"China_FE_Registry.jpg",
				"China_FE_tutorials.jpg",
				"China_Load1.jpg",
				"China_Load2.jpg",
				"China_Load3.jpg",
				"China_Load4.jpg",
				"China_Load5.jpg",
				"China_MapOfChina01.jpg",
				"China_MapOfChina02.jpg",
				"China_MapOfChina03.jpg",
				"China_MapOfChina04.jpg",
				"China_Victory.jpg",
				"scoreb.jpg"
			};
		}
	}
}
